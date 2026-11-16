undefined
using System;

namespace BatteryGuardian.SmartPlug
{
 /// <summary>
 /// Encrypts/decrypts Tuya 3.5 message payloads using AES-128-GCM.
 /// The wire layout used here is: nonce(12) + ciphertext + tag(16).
 /// The associated data is the 4-byte big-endian packet length field,
 /// matching the TinyTuya 3.5 format. This class is pure and unit-tested.
 /// </summary>
 public static class TuyaMessageCipher
 {
 public const int NonceSize = 12;
 public const int TagSize = 16;

 /// <summary>Builds the associated data: 4-byte big-endian length.</summary>
 public static byte[] BuildAssociatedData(int totalLength)
 {
 byte[] ad = new byte[4];
 BinaryPrimitives.WriteInt32BigEndian(ad, totalLength);
 return ad;
 }

 /// <summary>
 /// Encrypts plaintext with the session key. Returns nonce+ct+tag.
 /// </summary>
 public static byte[] Encrypt(byte[] sessionKey, byte[] plaintext, byte[] nonce, byte[] associatedData)
 {
 if (nonce.Length != NonceSize)
 throw new ArgumentException("Nonce must be 12 bytes.");
 return Concat(nonce, TuyaCrypto.Encrypt(sessionKey, nonce, plaintext, associatedData));
 }

 /// <summary>
 /// Decrypts a nonce+ct+tag payload. Throws CryptographicException when
 /// the tag is invalid or the payload is truncated.
 /// </summary>
 public static byte[] Decrypt(byte[] sessionKey, byte[] payload, byte[] associatedData)
 {
 if (payload.Length < NonceSize + TagSize)
 throw new TuyaProtocolException("Encrypted payload too short (" + payload.Length + ").");
 byte[] nonce = payload[..NonceSize];
 byte[] ciphertextWithTag = payload[NonceSize..];
 return TuyaCrypto.Decrypt(sessionKey, nonce, ciphertextWithTag, associatedData);
 }

 private static byte[] Concat(byte[] a, byte[] b)
 {
 byte[] r = new byte[a.Length + b.Length];
 Buffer.BlockCopy(a, 0, r, 0, a.Length);
 Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
 return r;
 }
 }
}
