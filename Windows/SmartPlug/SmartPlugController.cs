using System;
using System.Threading;
using System.Threading.Tasks;

namespace BatteryGuardian.SmartPlug
{
 /// <summary>
 /// Stateful orchestrator that turns battery readings into smart-plug
 /// commands. Owns retry/backoff and a synchronization gate so overlapping
 /// evaluations cannot race. Uses the actual relay state as the source of
 /// truth.
 /// </summary>
 public sealed class SmartPlugController : IDisposable
 {
 private readonly ISmartPlug _plug;
 private readonly SmartPlugConfig _config;
 private readonly SemaphoreSlim _gate = new(1, 1);
 private readonly int[] _retryBackoffMs;
 private SmartPlugStatus _status;

 /// <summary>
 /// Upper bound on one evaluation. Readings arrive every 30 seconds, so an
 /// evaluation that cannot finish inside that is abandoned rather than queued.
 /// </summary>
 private static readonly TimeSpan EvaluationTimeout = TimeSpan.FromSeconds(20);

 public SmartPlugController(ISmartPlug plug, SmartPlugConfig config, int[]? retryBackoffMs = null)
 {
  _plug = plug ?? throw new ArgumentNullException(nameof(plug));
  _config = config ?? throw new ArgumentNullException(nameof(config));
  _retryBackoffMs = retryBackoffMs ?? new[] { 1000, 2000 };
  _status = config.Enabled ? new SmartPlugStatus(true, false, RelayState.Unknown, null, SmartPlugError.None, null) : SmartPlugStatus.NotConfigured();
 }

 public SmartPlugStatus Status => _status;

 /// <summary>
 /// Evaluates a battery reading and applies the appropriate command.
 /// Returns the resulting status. Never throws for network failures; failures
 /// are classified into <see cref="SmartPlugStatus.LastError"/>.
 /// </summary>
 public async Task<SmartPlugStatus> EvaluateAsync(int batteryPercent, CancellationToken ct = default)
 {
  if (!_config.Enabled)
  {
   _status = _status with { Message = "Disabled" };
   return _status;
  }

  // Skip, never queue. WaitAsync(0) takes the gate only if it is free.
  // Queueing here was a defect: an unreachable plug held the gate through
  // about two minutes of retries, every 30-second tick added another waiter,
  // and the app then acted on readings many minutes old - or looked dead.
  if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false))
  {
   _status = _status with { Message = "Previous check still running; this reading was skipped." };
   return _status;
  }

  using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
  timeout.CancelAfter(EvaluationTimeout);
  CancellationToken token = timeout.Token;

  try
  {
   // 1. Determine actual relay state first (authoritative).
   RelayState relay = await GetRelayWithRetryAsync(token).ConfigureAwait(false);
   var decision = SmartPlugDecision.Evaluate(batteryPercent, batteryPercent >= 0, relay, _config);

   switch (decision.Action)
   {
    case SmartPlugDecision.Action.TurnOff:
     await RunWithRetryAsync(() => _plug.TurnOffAsync(token), token).ConfigureAwait(false);
     relay = RelayState.Off;
     break;
    case SmartPlugDecision.Action.TurnOn:
     await RunWithRetryAsync(() => _plug.TurnOnAsync(token), token).ConfigureAwait(false);
     relay = RelayState.On;
     break;
    case SmartPlugDecision.Action.None:
    default:
     break;
   }

   _status = new SmartPlugStatus(true, true, relay, DateTime.UtcNow, SmartPlugError.None, decision.Reason);
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

 private async Task<RelayState> GetRelayWithRetryAsync(CancellationToken ct)
 {
  RelayState relay = RelayState.Unknown;
  await RunWithRetryAsync(async () => { relay = await _plug.GetStatusAsync(ct).ConfigureAwait(false); }, ct).ConfigureAwait(false);
  return relay;
 }

 private async Task RunWithRetryAsync(Func<Task> op, CancellationToken ct)
 {
  Exception? last = null;
  for (int attempt = 0; attempt <= _retryBackoffMs.Length; attempt++)
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
    if (attempt < _retryBackoffMs.Length)
     await Task.Delay(_retryBackoffMs[attempt], ct).ConfigureAwait(false);
   }
  }
  throw last ?? new InvalidOperationException("Operation failed.");
 }

 /// <summary>
 /// Sends one command and returns, without retries and without waiting for an
 /// in-flight evaluation. Used when the machine is going to sleep or shutting
 /// down, where the OS will not wait for us: a slow, correct attempt is worth
 /// less than a fast one.
 /// </summary>
 public async Task<SmartPlugStatus> ApplyNowAsync(bool on, TimeSpan timeout)
 {
  using var cts = new CancellationTokenSource(timeout);
  try
  {
   if (on) await _plug.TurnOnAsync(cts.Token).ConfigureAwait(false);
   else await _plug.TurnOffAsync(cts.Token).ConfigureAwait(false);

   _status = _status with
   {
    Connected = true,
    Relay = on ? RelayState.On : RelayState.Off,
    LastSuccessUtc = DateTime.UtcNow,
    LastError = SmartPlugError.None,
    Message = on ? "Switched on." : "Switched off."
   };
  }
  catch (OperationCanceledException)
  {
   _status = _status with { Connected = false, LastError = SmartPlugError.Timeout, Message = "No time to reach the plug." };
  }
  catch (Exception ex)
  {
   _status = _status with { Connected = false, LastError = SmartPlugError.Unreachable, Message = ex.Message };
  }

  return _status;
 }

 public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
 {
  await _gate.WaitAsync(ct).ConfigureAwait(false);
  try
  {
   bool ok = await _plug.TestConnectionAsync(ct).ConfigureAwait(false);
   _status = _status with { Connected = ok, LastError = ok ? SmartPlugError.None : SmartPlugError.Unreachable, Message = ok ? "Connected" : "Connection failed" };
   return ok;
  }
  finally { _gate.Release(); }
 }

 public void Dispose()
 {
  _plug.Dispose();
  _gate.Dispose();
 }
 }
}
