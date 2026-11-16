using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BatteryGuardian.SmartPlug
{
 public sealed class TuyaSmartPlug : ISmartPlug
 {
 private const int CmdControl = 0x07;
 private const int CmdStatus = 0x08;

 private readonly string _deviceId;
 private readonly string _host;
 private readonly int _port;
 private readonly int _relayDp;
 private readonly byte[] _localKey;
 private readonly int _timeoutMs;

 private TuyaSession? _session;

 public TuyaSmartPlug(string deviceId, string host, int port, byte[] localKey, int relayDp, int timeoutMs = 5000)
 {
 _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
 _host = host ?? throw new ArgumentNullException(nameof(host));
 _port = port;
 _localKey = localKey ?? throw new ArgumentNullException(nameof(localKey));
 _relayDp = relayDp;
 _timeoutMs = timeoutMs;
 }

 private async Task EnsureConnectedAsync(CancellationToken ct)
 {
 if (_session != null && _session.IsConnected) return;
 _session?.Dispose();
 _session = new TuyaSession(_host, _port, _localKey, _timeoutMs);
 await _session.ConnectAsync(ct).ConfigureAwait(false);
 }

 public async Task<RelayState> GetStatusAsync(CancellationToken ct = default)
 {
 await EnsureConnectedAsync(ct).ConfigureAwait(false);
 byte[] response = await _session!.SendAndReceiveAsync(CmdStatus, Array.Empty<byte>(), ct).ConfigureAwait(false);
 return ParseRelay(response);
 }

 public Task TurnOnAsync(CancellationToken ct = default) => SetRelayAsync(true, ct);
 public Task TurnOffAsync(CancellationToken ct = default) => SetRelayAsync(false, ct);

 private async Task SetRelayAsync(bool on, CancellationToken ct)
 {
 await EnsureConnectedAsync(ct).ConfigureAwait(false);
 string json = BuildControlJson(_relayDp, on);
 await _session!.SendAndReceiveAsync(CmdControl, Encoding.UTF8.GetBytes(json), ct).ConfigureAwait(false);
 }

 internal static string BuildControlJson(int dp, bool on)
 {
 char q = (char)34;
 string s = new string(q, 1);
 return s + "{" + s + "dps" + s + ":{" + s + dp + s + ":" + (on ? "true" : "false") + s + "}}";
 }

 public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
 {
 try
 {
 await EnsureConnectedAsync(ct).ConfigureAwait(false);
 await GetStatusAsync(ct).ConfigureAwait(false);
 return true;
 }
 catch
 {
 return false;
 }
 }

 internal RelayState ParseRelay(byte[] response)
 {
 if (response.Length == 0) return RelayState.Unknown;
 try
 {
 using var doc = JsonDocument.Parse(response);
 if (doc.RootElement.TryGetProperty("dps", out var dps) &&
 dps.TryGetProperty(_relayDp.ToString(), out var val))
 {
 if (val.ValueKind == JsonValueKind.True) return RelayState.On;
 if (val.ValueKind == JsonValueKind.False) return RelayState.Off;
 if (val.ValueKind == JsonValueKind.String && bool.TryParse(val.GetString(), out bool b))
 return b ? RelayState.On : RelayState.Off;
 }
 }
 catch (JsonException) { }
 return RelayState.Unknown;
 }

 public void Dispose()
 {
 _session?.Dispose();
 _session = null;
 }
 }
}
