using System;
using BatteryGuardian.SmartPlug;
using Xunit;

namespace BatteryGuardian.Tests
{
 public class TuyaCryptoTests
 {
 private readonly byte[] _key = MakeKey();
 private readonly byte[] _nonce = MakeNonce();

 private static byte[] MakeKey() { var k = new byte[16]; for (int i = 0; i < 16; i++) k[i] = (byte)i; return k; }
 private static byte[] MakeNonce() { var n = new byte[12]; for (int i = 0; i < 12; i++) n[i] = (byte)(i + 1); return n; }

 [Fact]
 public void EncryptDecryptRoundTrip()
 {
 byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("hello tuya");
 byte[] encrypted = TuyaCrypto.Encrypt(_key, _nonce, plaintext, null);
 byte[] decrypted = TuyaCrypto.Decrypt(_key, _nonce, encrypted, null);
 Assert.Equal(plaintext, decrypted);
 }

 [Fact]
 public void TamperedCiphertextFails()
 {
 byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("secret");
 byte[] encrypted = TuyaCrypto.Encrypt(_key, _nonce, plaintext, null);
 encrypted[0] ^= 0xFF;
 Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => TuyaCrypto.Decrypt(_key, _nonce, encrypted, null));
 }

 [Fact]
 public void WrongKeyFails()
 {
 byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("data");
 byte[] encrypted = TuyaCrypto.Encrypt(_key, _nonce, plaintext, null);
 var wrongKey = new byte[16];
 for (int i = 0; i < 16; i++) wrongKey[i] = 0x55;
 Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => TuyaCrypto.Decrypt(wrongKey, _nonce, encrypted, null));
 }

 [Fact]
 public void HmacIsDeterministicAnd32Bytes()
 {
 byte[] a = TuyaCrypto.HmacSha256(_key, new byte[] { 1, 2, 3 });
 byte[] b = TuyaCrypto.HmacSha256(_key, new byte[] { 1, 2, 3 });
 Assert.Equal(a, b);
 Assert.Equal(32, a.Length);
 }

 [Fact]
 public void FixedTimeEqualWorks()
 {
 Assert.True(TuyaCrypto.FixedTimeEqual(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }));
 Assert.False(TuyaCrypto.FixedTimeEqual(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 }));
 Assert.False(TuyaCrypto.FixedTimeEqual(new byte[] { 1 }, new byte[] { 1, 2 }));
 }

 [Fact]
 public void DeriveLocalKeyFromHex()
 {
 byte[] k = TuyaCrypto.DeriveLocalKey("00112233445566778899aabbccddeeff");
 Assert.Equal(16, k.Length);
 Assert.Equal((byte)0x00, k[0]);
 Assert.Equal((byte)0xFF, k[15]);
 }

 [Fact]
 public void DeriveLocalKeyFromAscii16()
 {
 byte[] k = TuyaCrypto.DeriveLocalKey("abcdefghijklmnop");
 Assert.Equal(16, k.Length);
 }

 [Fact]
 public void DeriveLocalKeyRejectsBadFormat()
 {
 Assert.Throws<ArgumentException>(() => TuyaCrypto.DeriveLocalKey("short"));
 }
 }
}
