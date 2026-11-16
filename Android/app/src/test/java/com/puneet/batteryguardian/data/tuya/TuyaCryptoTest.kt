package com.puneet.batteryguardian.data.tuya

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class TuyaCryptoTest {
 private val key = ByteArray(16) { it.toByte() }
 private val nonce = ByteArray(12) { (it + 1).toByte() }

 @Test
 fun encryptDecryptRoundTrip() {
 val plaintext = "hello tuya".toByteArray(Charsets.UTF_8)
 val encrypted = TuyaCrypto.encrypt(key, nonce, plaintext)
 val decrypted = TuyaCrypto.decrypt(key, nonce, encrypted)
 assertArrayEquals(plaintext, decrypted)
 }

 @Test
 fun tamperedCiphertextFailsDecrypt() {
 val plaintext = "secret".toByteArray(Charsets.UTF_8)
 val encrypted = TuyaCrypto.encrypt(key, nonce, plaintext)
 encrypted[0] = (encrypted[0].toInt() xor 0xFF).toByte()
 var threw = false
 try { TuyaCrypto.decrypt(key, nonce, encrypted) } catch (t: Throwable) { threw = true }
 assertTrue(threw)
 }

 @Test
 fun wrongKeyFailsDecrypt() {
 val plaintext = "data".toByteArray(Charsets.UTF_8)
 val encrypted = TuyaCrypto.encrypt(key, nonce, plaintext)
 val wrongKey = ByteArray(16) { 0x55.toByte() }
 var threw = false
 try { TuyaCrypto.decrypt(wrongKey, nonce, encrypted) } catch (t: Throwable) { threw = true }
 assertTrue(threw)
 }

 @Test
 fun hmacDeterministic() {
 val a = TuyaCrypto.hmacSha256(key, byteArrayOf(1, 2, 3))
 val b = TuyaCrypto.hmacSha256(key, byteArrayOf(1, 2, 3))
 assertArrayEquals(a, b)
 assertEquals(32, a.size)
 }

 @Test
 fun fixedTimeEqualWorks() {
 assertTrue(TuyaCrypto.fixedTimeEqual(byteArrayOf(1, 2, 3), byteArrayOf(1, 2, 3)))
 assertFalse(TuyaCrypto.fixedTimeEqual(byteArrayOf(1, 2, 3), byteArrayOf(1, 2, 4)))
 assertFalse(TuyaCrypto.fixedTimeEqual(byteArrayOf(1), byteArrayOf(1, 2)))
 }

 @Test
 fun deriveLocalKeyFromHex() {
 val k = TuyaCrypto.deriveLocalKey("00112233445566778899aabbccddeeff")
 assertEquals(16, k.size)
 assertEquals(0x00, k[0].toInt())
 assertEquals(0xFF.toByte(), k[15])
 }

 @Test
 fun deriveLocalKeyFromAscii16() {
 val k = TuyaCrypto.deriveLocalKey("abcdefghijklmnop")
 assertEquals(16, k.size)
 }
}
