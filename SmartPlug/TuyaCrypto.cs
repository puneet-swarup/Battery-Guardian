using System;
using System.Security.Cryptography;

namespace BatteryGuardian.SmartPlug
{
 /// <summary>
 /// Cryptographic primitives for the Tuya local protocol 3.5.
 /// Pure and stateless; built on AES-GCM and HMAC-SHA256 so it can be
 /// cross-checked against known test vectors.
 /// </summary>
 public static class TuyaCrypto
 {
 public const int GcmNonceSize = 12;
 public const int GcmTagSize = 16;
 public const int KeySize = 16;
 public const int HmacSize = 32;

 public static byte[] Encrypt(byte[] key, byte[] nonce, byte[] plaintext, byte[]? associatedData)
 {
 ValidateArgs(key, nonce);
 ArgumentNullException.ThrowIfNull(plaintext);
 associatedData ??= Array.Empty<byte>();
 byte[] ciphertext = new byte[plaintext.Length];
 byte[] tag = new byte[GcmTagSize];
 using var gcm = new AesGcm(key, GcmTagSize);
 gcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
 byte[] result = new byte[ciphertext.Length + tag.Length];
 Buffer.BlockCopy(ciphertext, 0, result, 0, ciphertext.Length);
 Buffer.BlockCopy(tag, 0, result, ciphertext.Length, tag.Length);
 return result;
 }

 public static byte[] Decrypt(byte[] key, byte[] nonce, byte[] ciphertextWithTag, byte[]? associatedData)
 {
 ValidateArgs(key, nonce);
 ArgumentNullException.ThrowIfNull(ciphertextWithTag);
 if (ciphertextWithTag.Length < GcmTagSize)
 throw new CryptographicException("Tuya ciphertext is too short (" + ciphertextWithTag.Length + " bytes).");
 associatedData ??= Array.Empty<byte>();
 int ctLen = ciphertextWithTag.Length - GcmTagSize;
 byte[] ciphertext = new byte[ctLen];
 byte[] tag = new byte[GcmTagSize];
 Buffer.BlockCopy(ciphertextWithTag, 0, ciphertext, 0, ctLen);
 Buffer.BlockCopy(ciphertextWithTag, ctLen, tag, 0, GcmTagSize);
 byte[] plaintext = new byte[ctLen];
 using var gcm = new AesGcm(key, GcmTagSize);
 gcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
 return plaintext;
 }

 public static byte[] HmacSha256(byte[] key, byte[] data)
 {
 ArgumentNullException.ThrowIfNull(key);
 ArgumentNullException.ThrowIfNull(data);
 using var hmac = new HMACSHA256(key);
 return hmac.ComputeHash(data);
 }

 public static bool FixedTimeEqual(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
 {
 if (a.Length != b.Length) return false;
 int diff = 0;
 for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
 return diff == 0;
 }

 public static byte[] DeriveLocalKey(string localKey)
 {
 if (string.IsNullOrWhiteSpace(localKey))
 throw new ArgumentException("Local key must not be empty.");
 string key = localKey.Trim();
 if (key.Length == 32 && IsHex(key))
 return Convert.FromHexString(key);
 if (key.Length == 16 || key.Length == 32)
 return System.Text.Encoding.ASCII.GetBytes(key);
 throw new ArgumentException("Invalid local key format (length " + key.Length + "). Expected 32 hex or 16/32 ASCII characters.");
 }

 private static bool IsHex(string value)
 {
 foreach (char c in value)
 if (!IsHexChar(c)) return false;
 return true;
 }

 private static bool IsHexChar(char c) =>
 (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

 private static void ValidateArgs(byte[] key, byte[] nonce)
 {
 ArgumentNullException.ThrowIfNull(key);
 ArgumentNullException.ThrowIfNull(nonce);
 if (key.Length != KeySize)
 throw new ArgumentException("Tuya key must be " + KeySize + " bytes (got " + key.Length + ").");
 if (nonce.Length != GcmNonceSize)
 throw new ArgumentException("Tuya nonce must be " + GcmNonceSize + " bytes (got " + nonce.Length + ").");
 }
 }
}
