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
 private SmartPlugStatus _status;

 public SmartPlugController(ISmartPlug plug, SmartPlugConfig config)
 {
 _plug = plug ?? throw new ArgumentNullException(nameof(plug));
 _config = config ?? throw new ArgumentNullException(nameof(config));
 _status = config.Enabled ? new SmartPlugStatus(true, false, RelayState.Unknown, null, SmartPlugError.None, null) : SmartPlugStatus.NotConfigured();
 }

 public SmartPlugStatus Status => _status;

 /// <summary>
 /// Evaluates a battery reading and applies the appropriate command.
 /// Returns the resulting status. Never throws for network failures;
 /// failures are classified into <see cref="SmartPlugStatus.LastError".
 /// </summary>
 public async Task<SmartPlugStatus> EvaluateAsync(int batteryPercent, CancellationToken ct = default)
 {
 if (!_config.Enabled)
 {
 _status = _status with { Message = "Disabled" };
 return _status;
 }

 await _gate.WaitAsync(ct).ConfigureAwait(false);
 try
 {
 // 1. Determine actual relay state first (authoritative).
 RelayState relay = await GetRelayWithRetryAsync(ct).ConfigureAwait(false);
 var decision = SmartPlugDecision.Evaluate(batteryPercent, batteryPercent >= 0, relay, _config);

 switch (decision.Action)
 {
 case SmartPlugDecision.Action.TurnOff:
 await RunWithRetryAsync(() => _plug.TurnOffAsync(ct), ct).ConfigureAwait(false);
 relay = RelayState.Off;
 break;
 case SmartPlugDecision.Action.TurnOn:
 await RunWithRetryAsync(() => _plug.TurnOnAsync(ct), ct).ConfigureAwait(false);
 relay = RelayState.On;
 break;
 case SmartPlugDecision.Action.Query:
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
 _status = _status with { Connected = false, LastError = SmartPlugError.Timeout, Message = "Operation timed out." };
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

 private static async Task RunWithRetryAsync(Func<Task> op, CancellationToken ct)
 {
 int[] backoffMs = { 5000, 15000, 30000 };
 Exception? last = null;
 for (int attempt = 0; attempt <= backoffMs.Length; attempt++)
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
 if (attempt < backoffMs.Length)
 await Task.Delay(backoffMs[attempt], ct).ConfigureAwait(false);
 }
 }
 throw last ?? new InvalidOperationException("Operation failed.");
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
