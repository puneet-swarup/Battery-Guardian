using System;
using System.Threading;
using System.Threading.Tasks;

namespace BatteryGuardian.SmartPlug
{
 /// <summary>Relay state as reported by the physical device.</summary>
 public enum RelayState
 {
 Unknown = 0,
 On = 1,
 Off = 2,
 }

 /// <summary>Classification of a smart-plug failure (for diagnostics/UI).</summary>
 public enum SmartPlugError
 {
 None = 0,
 NotConfigured,
 InvalidCredentials,
 Unreachable,
 Timeout,
 Protocol,
 DeviceError,
 }

 /// <summary>Point-in-time snapshot of the controller's view of the device.</summary>
 public sealed record SmartPlugStatus(
 bool Configured,
 bool Connected,
 RelayState Relay,
 DateTime? LastSuccessUtc,
 SmartPlugError LastError,
 string? Message)
 {
 public static SmartPlugStatus NotConfigured() =>
 new(false, false, RelayState.Unknown, null, SmartPlugError.NotConfigured, "Smart plug is not configured.");
 }

 /// <summary>
 /// Logical interface the controller talks to. Keeps AES, TCP framing and
 /// platform details out of the threshold logic.
 /// </summary>
 public interface ISmartPlug : IDisposable
 {
 Task<RelayState> GetStatusAsync(CancellationToken ct = default);
 Task TurnOnAsync(CancellationToken ct = default);
 Task TurnOffAsync(CancellationToken ct = default);
 Task<bool> TestConnectionAsync(CancellationToken ct = default);
 }
}
