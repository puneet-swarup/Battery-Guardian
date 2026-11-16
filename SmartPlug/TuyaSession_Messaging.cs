using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace BatteryGuardian.SmartPlug
{
 public sealed partial class TuyaSession
 {
 /// <summary>
 /// Sends a plaintext payload (already JSON) for the given command and
 /// waits for the response frame, returning the decrypted plaintext.
 /// </summary>
 public async Task<byte[]> SendAndReceiveAsync(int command, byte[] plaintext, CancellationToken ct = default)
 {
 if (_stream == null || _sessionKey == null)
 throw new InvalidOperationException("Not connected.");

 byte[] nonce = new byte[TuyaMessageCipher.NonceSize];
 RandomNumberGenerator.Fill(nonce);

 // Associated data is the 4-byte packet length. The length covers
 // seq(4) + cmd(4) + payload(nonce+ct+tag). We compute it in two
 // passes because the length depends on the ciphertext size.
 int plainLen = plaintext.Length;
 int encLen = TuyaMessageCipher.NonceSize + plainLen + TuyaMessageCipher.TagSize;
 int totalLength = 8 + encLen;
 byte[] ad = TuyaMessageCipher.BuildAssociatedData(totalLength);
 byte[] encrypted = TuyaMessageCipher.Encrypt(_sessionKey, plaintext, nonce, ad);

 int seq = _sequence++;
 byte[] frame = TuyaMessageCodec.Encode(seq, command, encrypted);
 await _stream.WriteAsync(frame, ct).ConfigureAwait(false);

 var response = await ReceiveFrameAsync(ct).ConfigureAwait(false);
 int respTotal = response.Payload.Length + 8;
 byte[] respAd = TuyaMessageCipher.BuildAssociatedData(respTotal);
 return TuyaMessageCipher.Decrypt(_sessionKey, response.Payload, respAd);
 }
 }
}
