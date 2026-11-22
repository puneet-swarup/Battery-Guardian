using System;
using BatteryGuardian.Signals;
using System.Threading;
using System.Threading.Tasks;

namespace BatteryGuardian.SmartPlug
{
 /// <summary>
 /// Stateful orchestrator that turns battery readings into smart-plug commands.
 /// Owns retries, the synchronization gate, and the two facts that are easy to
 /// confuse: what the plug last reported, and what we last asked it to do.
 /// </summary>
 /// <remarks>
 /// Every command goes through <see cref="SetRelayAsync"/>, so there is one set
 /// of rules - one gate, one retry policy, one place that records intent.
 /// <see cref="Status"/>.Relay is only ever written by a successful read;
 /// <see cref="IntendedRelay"/> is only ever written by a send. Keeping them
 /// apart is what stops a command from being mistaken for an observation.
 /// </remarks>
 public sealed class SmartPlugController : IDisposable
 {
  private readonly ISmartPlug _plug;
  private readonly SmartPlugConfig _config;
  private readonly SemaphoreSlim _gate = new(1, 1);
  private readonly int[] _retryBackoffMs;
  private SmartPlugStatus _status;

  /// <summary>
  /// Upper bound on one evaluation. The window refreshes every 30 seconds
  /// (<c>MainWindow.RefreshIntervalSeconds</c>), so this must stay well below
  /// it - an evaluation that outlives the interval makes the next reading skip.
  /// </summary>
  public const int EvaluationTimeoutSeconds = 20;

  public SmartPlugController(ISmartPlug plug, SmartPlugConfig config, int[]? retryBackoffMs = null)
  {
   _plug = plug ?? throw new ArgumentNullException(nameof(plug));
   _config = config ?? throw new ArgumentNullException(nameof(config));
   _retryBackoffMs = retryBackoffMs ?? new[] { 1000, 2000 };
   _status = config.Enabled
    ? new SmartPlugStatus(true, false, RelayState.Unknown, null, SmartPlugError.None, null)
    : SmartPlugStatus.NotConfigured();
  }

  /// <summary>What the plug last reported, and the outcome of the last operation.</summary>
  public SmartPlugStatus Status => _status;

  /// <summary>
  /// The last relay state this app asked for, whether or not it was confirmed.
  /// Never written by a read, never overwritten by one.
  /// </summary>
  public RelayState IntendedRelay { get; private set; } = RelayState.Unknown;

  /// <summary>
  /// Evaluates a battery reading and applies the appropriate command. Never
  /// throws for network failures; they are classified into
  /// <see cref="SmartPlugStatus.LastError"/>.
  /// </summary>
  public async Task<SmartPlugStatus> EvaluateAsync(BatteryBand band, CancellationToken ct = default)
  {
   if (!_config.Enabled)
   {
    _status = _status with { Message = "Disabled" };
    return _status;
   }

   // Skip, never queue. Waiting here let an unreachable plug hold the gate while
   // every 30-second reading queued behind it, so the app acted on readings many
   // minutes old.
   if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false))
   {
    _status = _status with { Message = "Previous check still running; this reading was skipped." };
    return _status;
   }

   using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
   timeout.CancelAfter(TimeSpan.FromSeconds(EvaluationTimeoutSeconds));
   CancellationToken token = timeout.Token;

   try
   {
    // Read, but never let a failed read stop the command: the plug is idle for
    // hours before the low threshold arrives, which is exactly when the read
    // fails, and both commands are idempotent.
    RelayState observed = RelayState.Unknown;
    SmartPlugError readError = SmartPlugError.None;
    string? readMessage = null;

    try
    {
     observed = await GetRelayWithRetryAsync(token).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
     throw;
    }
    catch (Exception ex)
    {
     readError = SmartPlugError.Unreachable;
     readMessage = ex.Message;
    }

    bool readOk = readError == SmartPlugError.None;
    var decision = SmartPlugDecision.Evaluate(band, observed, _config);

    bool commandSent = false;
    if (decision.Action == SmartPlugDecision.Action.TurnOff)
    {
     await SendUnsafeAsync(false, token, _retryBackoffMs).ConfigureAwait(false);
     commandSent = true;
    }
    else if (decision.Action == SmartPlugDecision.Action.TurnOn)
    {
     await SendUnsafeAsync(true, token, _retryBackoffMs).ConfigureAwait(false);
     commandSent = true;
    }

    // Nothing to do and the plug did not answer: report the failure rather than a
    // clean status, so an unreachable plug is never mistaken for a quiet one.
    if (!readOk && !commandSent)
    {
     _status = _status with { Connected = false, LastError = readError, Message = readMessage };
     return _status;
    }

    _status = new SmartPlugStatus(
     true,
     readOk || commandSent,
     readOk ? observed : _status.Relay,   // never a commanded value
     DateTime.UtcNow,
     SmartPlugError.None,
     readOk ? decision.Reason : decision.Reason + " (the plug did not report its state.)");

    return _status;
   }
   catch (TimeoutException ex)
   {
    _status = _status with { Connected = false, LastError = SmartPlugError.Timeout, Message = ex.Message };
    return _status;
   }
   catch (TuyaProtocolException ex)
   {
    _status = _status with { Connected = false, LastError = SmartPlugError.Protocol, Message = ex.Message };
    return _status;
   }
   catch (OperationCanceledException)
   {
    _status = _status with { Connected = false, LastError = SmartPlugError.Timeout, Message = "Gave up waiting for the plug." };
    return _status;
   }
   catch (Exception ex)
   {
    _status = _status with { Connected = false, LastError = SmartPlugError.Unreachable, Message = ex.Message };
    return _status;
   }
   finally
   {
    _gate.Release();
   }
  }

  /// <summary>
  /// The one way to change the relay. Used by the threshold evaluation, by the
  /// sleep cut and by the wake restore, so all three obey the same rules.
  /// </summary>
  /// <param name="on">Switch on, or off.</param>
  /// <param name="timeout">Hard limit for the whole operation.</param>
  /// <param name="verify">
  /// Re-read after sending and record what the plug says. Costs a round trip;
  /// worth it when the machine is awake and the answer matters.
  /// </param>
  /// <param name="waitForGate">
  /// False when the machine is going to sleep: never wait, send or don't.
  /// </param>
  /// <param name="retryBackoffMs">
  /// Null for the controller's policy; an empty array for a single attempt.
  /// </param>
  public async Task<SmartPlugStatus> SetRelayAsync(
   bool on,
   TimeSpan timeout,
   bool verify = false,
   bool waitForGate = true,
   int[]? retryBackoffMs = null)
  {
   using var cts = new CancellationTokenSource(timeout);
   CancellationToken token = cts.Token;

   bool acquired = waitForGate
    ? await _gate.WaitAsync(timeout, token).ConfigureAwait(false)
    : await _gate.WaitAsync(0, token).ConfigureAwait(false);

   if (!acquired)
   {
    _status = _status with { Message = "The plug was busy; the command was not sent." };
    return _status;
   }

   try
   {
    await SendUnsafeAsync(on, token, retryBackoffMs ?? _retryBackoffMs).ConfigureAwait(false);

    if (!verify)
    {
     _status = _status with
     {
      Connected = true,
      LastSuccessUtc = DateTime.UtcNow,
      LastError = SmartPlugError.None,
      Message = on ? "Switched on." : "Switched off."
     };
     return _status;
    }

    try
    {
     RelayState observed = await _plug.GetStatusAsync(token).ConfigureAwait(false);
     _status = new SmartPlugStatus(true, true, observed, DateTime.UtcNow, SmartPlugError.None,
      (on ? "Switched on" : "Switched off") + $", plug reports {observed}.");
    }
    catch (Exception ex)
    {
     _status = _status with
     {
      Connected = true,
      LastSuccessUtc = DateTime.UtcNow,
      LastError = SmartPlugError.None,
      Message = (on ? "Switched on" : "Switched off") + $", but the plug did not confirm it ({ex.Message})."
     };
    }

    return _status;
   }
   catch (OperationCanceledException)
   {
    _status = _status with { Connected = false, LastError = SmartPlugError.Timeout, Message = "No time to reach the plug." };
    return _status;
   }
   catch (Exception ex)
   {
    _status = _status with { Connected = false, LastError = SmartPlugError.Unreachable, Message = ex.Message };
    return _status;
   }
   finally
   {
    _gate.Release();
   }
  }

  /// <summary>
  /// Reads the relay state and changes nothing. Used by "Read Smart Plug Now" so
  /// the state on screen can be compared with the physical socket.
  /// </summary>
  public async Task<SmartPlugStatus> ReadStatusAsync(TimeSpan timeout)
  {
   using var cts = new CancellationTokenSource(timeout);
   try
   {
    RelayState observed = await _plug.GetStatusAsync(cts.Token).ConfigureAwait(false);
    _status = _status with
    {
     Connected = true,
     Relay = observed,
     LastSuccessUtc = DateTime.UtcNow,
     LastError = SmartPlugError.None,
     Message = $"The plug reports {observed}."
    };
   }
   catch (OperationCanceledException)
   {
    _status = _status with { Connected = false, LastError = SmartPlugError.Timeout, Message = "The plug did not answer in time." };
   }
   catch (Exception ex)
   {
    _status = _status with { Connected = false, LastError = SmartPlugError.Unreachable, Message = ex.Message };
   }

   return _status;
  }

  public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
  {
   // Bounded, not indefinite: the test button must not hang behind an evaluation.
   if (!await _gate.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false))
   {
    _status = _status with { Connected = false, Message = "The plug is busy; try again in a moment." };
    return false;
   }

   try
   {
    bool ok = await _plug.TestConnectionAsync(ct).ConfigureAwait(false);
    _status = _status with
    {
     Connected = ok,
     LastError = ok ? SmartPlugError.None : SmartPlugError.Unreachable,
     Message = ok ? "Connected" : "Connection failed"
    };
    return ok;
   }
   finally { _gate.Release(); }
  }

  public void Dispose()
  {
   _plug.Dispose();
   _gate.Dispose();
  }

  /// <summary>
  /// Sends the command and records the intent. The caller must already hold the
  /// gate. This is the only place a relay command is issued.
  /// </summary>
  private async Task SendUnsafeAsync(bool on, CancellationToken token, int[] retryBackoffMs)
  {
   await RunWithRetryAsync(
    async () =>
    {
     if (on) await _plug.TurnOnAsync(token).ConfigureAwait(false);
     else await _plug.TurnOffAsync(token).ConfigureAwait(false);
    },
    token,
    retryBackoffMs).ConfigureAwait(false);

   IntendedRelay = on ? RelayState.On : RelayState.Off;
  }

  private async Task<RelayState> GetRelayWithRetryAsync(CancellationToken ct)
  {
   RelayState relay = RelayState.Unknown;
   await RunWithRetryAsync(
    async () => { relay = await _plug.GetStatusAsync(ct).ConfigureAwait(false); },
    ct,
    _retryBackoffMs).ConfigureAwait(false);
   return relay;
  }

  private static async Task RunWithRetryAsync(Func<Task> op, CancellationToken ct, int[] retryBackoffMs)
  {
   Exception? last = null;
   for (int attempt = 0; attempt <= retryBackoffMs.Length; attempt++)
   {
    try
    {
     await op().ConfigureAwait(false);
     return;
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex)
    {
     last = ex;
     if (attempt < retryBackoffMs.Length)
      await Task.Delay(retryBackoffMs[attempt], ct).ConfigureAwait(false);
    }
   }
   throw last ?? new InvalidOperationException("Operation failed.");
  }
 }
}
