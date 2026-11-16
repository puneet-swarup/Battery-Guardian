package com.puneet.batteryguardian.data.tuya

object TuyaMessageCipher {
 const val NONCE_SIZE = 12
 const val TAG_SIZE = 16

 fun buildAssociatedData(totalLength: Int): ByteArray {
 return byteArrayOf(
 (totalLength ushr 24).toByte(),
 (totalLength ushr 16).toByte(),
 (totalLength ushr 8).toByte(),
 totalLength.toByte()
 )
 }

 fun encrypt(sessionKey: ByteArray, plaintext: ByteArray, nonce: ByteArray, associatedData: ByteArray): ByteArray {
 require(nonce.size == NONCE_SIZE) { "Nonce must be 12 bytes." }
 val ct = TuyaCrypto.encrypt(sessionKey, nonce, plaintext, associatedData)
 val out = ByteArray(nonce.size + ct.size)
 System.arraycopy(nonce, 0, out, 0, nonce.size)
 System.arraycopy(ct, 0, out, nonce.size, ct.size)
 return out
 }

 fun decrypt(sessionKey: ByteArray, payload: ByteArray, associatedData: ByteArray): ByteArray {
 require(payload.size >= NONCE_SIZE + TAG_SIZE) { "Encrypted payload too short." }
 val nonce = payload.copyOfRange(0, NONCE_SIZE)
 val ct = payload.copyOfRange(NONCE_SIZE, payload.size)
 return TuyaCrypto.decrypt(sessionKey, nonce, ct, associatedData)
 }
}
