package com.puneet.batteryguardian.data.tuya

import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.Mac
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

// Cryptographic primitives for the Tuya local protocol 3.5.
object TuyaCrypto {
 const val GCM_NONCE_SIZE = 12
 const val GCM_TAG_BITS = 128
 const val GCM_TAG_SIZE = 16
 const val KEY_SIZE = 16
 const val HMAC_SIZE = 32
 private val random = SecureRandom()

 fun encrypt(key: ByteArray, nonce: ByteArray, plaintext: ByteArray, associatedData: ByteArray = ByteArray(0)): ByteArray {
 require(key.size == KEY_SIZE) { "Key must be 16 bytes" }
 require(nonce.size == GCM_NONCE_SIZE) { "Nonce must be 12 bytes" }
 val cipher = Cipher.getInstance("AES/GCM/NoPadding")
 cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(GCM_TAG_BITS, nonce))
 if (associatedData.isNotEmpty()) cipher.updateAAD(associatedData)
 return cipher.doFinal(plaintext)
 }

 fun decrypt(key: ByteArray, nonce: ByteArray, ciphertextWithTag: ByteArray, associatedData: ByteArray = ByteArray(0)): ByteArray {
 require(key.size == KEY_SIZE) { "Key must be 16 bytes" }
 require(nonce.size == GCM_NONCE_SIZE) { "Nonce must be 12 bytes" }
 require(ciphertextWithTag.size >= GCM_TAG_SIZE) { "Ciphertext too short" }
 val cipher = Cipher.getInstance("AES/GCM/NoPadding")
 cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(GCM_TAG_BITS, nonce))
 if (associatedData.isNotEmpty()) cipher.updateAAD(associatedData)
 return cipher.doFinal(ciphertextWithTag)
 }

 fun hmacSha256(key: ByteArray, data: ByteArray): ByteArray {
 val mac = Mac.getInstance("HmacSHA256")
 mac.init(SecretKeySpec(key, "HmacSHA256"))
 return mac.doFinal(data)
 }

 fun newNonce(): ByteArray {
 val b = ByteArray(GCM_NONCE_SIZE)
 random.nextBytes(b)
 return b
 }

 fun fixedTimeEqual(a: ByteArray, b: ByteArray): Boolean {
 if (a.size != b.size) return false
 var diff = 0
 for (i in a.indices) diff = diff or (a[i].toInt() xor b[i].toInt())
 return diff == 0
 }

 fun deriveLocalKey(localKey: String): ByteArray {
 val key = localKey.trim()
 require(key.isNotEmpty()) { "Local key must not be empty" }
 if (key.length == 32 && isHex(key)) return hexToBytes(key)
 if (key.length == 16 || key.length == 32) return key.toByteArray(Charsets.US_ASCII)
 throw IllegalArgumentException("Invalid local key format")
 }

 private fun isHex(s: String): Boolean {
 for (c in s) {
 val code = c.code
 val ok = (code >= 48 && code <= 57) || (code >= 97 && code <= 102) || (code >= 65 && code <= 70)
 if (!ok) return false
 }
 return true
 }

 private fun hexToBytes(hex: String): ByteArray {
 val out = ByteArray(hex.length / 2)
 for (i in out.indices) {
 out[i] = ((Character.digit(hex[i * 2], 16) shl 4) + Character.digit(hex[i * 2 + 1], 16)).toByte()
 }
 return out
 }
}
