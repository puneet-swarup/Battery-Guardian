using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace BatteryGuardian.SmartPlug
{
 /// <summary>
 /// Manages a single Tuya 3.5 TCP session: connect, negotiate the
 /// session key, frame outgoing packets and read framed responses.
 /// All network work is async; the caller is expected to serialise calls.
 /// </summary>
 public sealed partial class TuyaSession : IDisposable
 {
 private const int CmdSessKeyNegStart = 0x03;
 private const int CmdSessKeyNegResult = 0x04;
 private const int CmdSessKeyNegFinish = 0x05;
 private const int CmdSessKeyNegFinishAck = 0x06;
 private const int CmdControl = 0x07;
 private const int CmdStatus = 0x08;
 private const int CmdDpQuery = 0x0A;

 private readonly string _host;
 private readonly int _port;
 private readonly byte[] _localKey;
 private readonly int _timeoutMs;
 private TcpClient? _client;
 private NetworkStream? _stream;
 private byte[]? _sessionKey;
 private int _sequence = 1;
 private readonly object _lock = new();

 public TuyaSession(string host, int port, byte[] localKey, int timeoutMs = 5000)
 {
 _host = host ?? throw new ArgumentNullException(nameof(host));
 _port = port;
 _localKey = localKey ?? throw new ArgumentNullException(nameof(localKey));
 _timeoutMs = timeoutMs;
 }

 public bool IsConnected => _client?.Connected == true && _sessionKey != null;

 public async Task ConnectAsync(CancellationToken ct = default)
 {
 _client = new TcpClient { NoDelay = true };
 using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
 cts.CancelAfter(_timeoutMs);
 try
 {
 await _client.ConnectAsync(_host, _port, cts.Token).ConfigureAwait(false);
 }
 catch (OperationCanceledException)
 {
 throw new TuyaProtocolException("Timed out connecting to " + _host + ":" + _port + ".");
 }
 _stream = _client.GetStream();
 _stream.ReadTimeout = _timeoutMs;
 _stream.WriteTimeout = _timeoutMs;
 await NegotiateSessionKeyAsync(ct).ConfigureAwait(false);
 }

 private async Task NegotiateSessionKeyAsync(CancellationToken ct)
 {
 byte[] clientNonce = new byte[16];
 RandomNumberGenerator.Fill(clientNonce);

 // 1. Send SESS_KEY_NEG_START with client nonce (NOT encrypted).
 await SendRawAsync(CmdSessKeyNegStart, clientNonce, ct).ConfigureAwait(false);
 var startAck = await ReceiveFrameAsync(ct).ConfigureAwait(false);
 if (startAck.Command != CmdSessKeyNegResult)
 throw new TuyaProtocolException("Expected session-key response 0x04, got 0x" + startAck.Command.ToString("X2") + ".");
 byte[] deviceNonce = startAck.Payload;
 if (deviceNonce.Length < 16)
 throw new TuyaProtocolException("Device nonce too short (" + deviceNonce.Length + ").");
 if (deviceNonce.Length > 16) deviceNonce = deviceNonce[..16];

 // 2. hmac = HMAC-SHA256(localKey, clientNonce + deviceNonce)
 byte[] combined = new byte[32];
 Buffer.BlockCopy(clientNonce, 0, combined, 0, 16);
 Buffer.BlockCopy(deviceNonce, 0, combined, 16, 16);
 byte[] hmac = TuyaCrypto.HmacSha256(_localKey, combined);

 // 3. Send SESS_KEY_NEG_FINISH with the hmac.
 await SendRawAsync(CmdSessKeyNegFinish, hmac, ct).ConfigureAwait(false);
 var finishAck = await ReceiveFrameAsync(ct).ConfigureAwait(false);
 if (finishAck.Command != CmdSessKeyNegFinishAck)
 throw new TuyaProtocolException("Expected session-key finish ack 0x06, got 0x" + finishAck.Command.ToString("X2") + ".");
 if (finishAck.Payload.Length >= 32)
 {
 // Device proves knowledge of the local key by returning
 // HMAC(localKey, deviceNonce + clientNonce).
 byte[] expectCombined = new byte[32];
 Buffer.BlockCopy(deviceNonce, 0, expectCombined, 0, 16);
 Buffer.BlockCopy(clientNonce, 0, expectCombined, 16, 16);
 byte[] expected = TuyaCrypto.HmacSha256(_localKey, expectCombined);
 if (!TuyaCrypto.FixedTimeEqual(expected, finishAck.Payload.AsSpan(0, 32)))
 throw new TuyaProtocolException("Session-key HMAC verification failed (wrong local key).");
 }

 // 4. sessionKey = AES-128-ECB-Encrypt(localKey, clientNonce XOR deviceNonce)
 byte[] xor = new byte[16];
 for (int i = 0; i < 16; i++) xor[i] = (byte)(clientNonce[i] ^ deviceNonce[i]);
 _sessionKey = AesEcbEncrypt(_localKey, xor);
 }

 private static byte[] AesEcbEncrypt(byte[] key, byte[] block)
 {
 using var aes = System.Security.Cryptography.Aes.Create();
 aes.Key = key;
 aes.Mode = CipherMode.ECB;
 aes.Padding = PaddingMode.None;
 using var enc = aes.CreateEncryptor();
 return enc.TransformFinalBlock(block, 0, block.Length);
 }

 private Task SendRawAsync(int command, byte[] payload, CancellationToken ct)
 {
 if (_stream == null) throw new InvalidOperationException("Not connected.");
 byte[] frame = TuyaMessageCodec.Encode(_sequence++, command, payload);
 return _stream.WriteAsync(frame, ct).AsTask();
 }

 private async Task<TuyaMessageCodec.TuyaFrame> ReceiveFrameAsync(CancellationToken ct)
 {
 if (_stream == null) throw new InvalidOperationException("Not connected.");
 byte[]? frameBytes = null;
 try
 {
 using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
 cts.CancelAfter(_timeoutMs);
 bool ok = await Task.Run(() => TuyaMessageCodec.TryReadFrame(_stream, out frameBytes), cts.Token).ConfigureAwait(false);
 if (!ok) throw new TuyaProtocolException("Connection closed before a complete frame arrived.");
 }
 catch (OperationCanceledException)
 {
 throw new TuyaProtocolException("Timed out waiting for a Tuya frame.");
 }
 return TuyaMessageCodec.Decode(frameBytes!);
 }

 public void Dispose()
 {
 _stream?.Dispose();
 _client?.Dispose();
 _stream = null;
 _client = null;
 _sessionKey = null;
 }
 }
}
