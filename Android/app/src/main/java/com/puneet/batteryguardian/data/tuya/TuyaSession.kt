package com.puneet.batteryguardian.data.tuya

import java.io.InputStream
import java.net.InetSocketAddress
import java.net.Socket
import javax.crypto.Cipher
import javax.crypto.spec.SecretKeySpec

class TuyaSession(
 private val host: String,
 private val port: Int,
 private val localKey: ByteArray,
 private val timeoutMs: Int = 5000
) {
 companion object {
 const val CMD_SESS_START = 0x03
 const val CMD_SESS_RESULT = 0x04
 const val CMD_SESS_FINISH = 0x05
 const val CMD_SESS_FINISH_ACK = 0x06
 const val CMD_CONTROL = 0x07
 const val CMD_STATUS = 0x08
 }

 private var socket: Socket? = null
 private var input: InputStream? = null
 private var sessionKey: ByteArray? = null
 private var sequence = 1

 val isConnected: Boolean get() = socket?.isConnected == true && sessionKey != null

 fun connect() {
 val s = Socket()
 s.connect(InetSocketAddress(host, port), timeoutMs)
 s.soTimeout = timeoutMs
 socket = s
 input = s.getInputStream()
 negotiate()
 }

 private fun negotiate() {
 val clientNonce = TuyaCrypto.newNonce()
 sendRaw(CMD_SESS_START, clientNonce)
 val startAck = receiveFrame()
 require(startAck.command == CMD_SESS_RESULT) { "Expected 0x04, got 0x" + startAck.command.toString(16) }
 val deviceNonce = startAck.payload.copyOfRange(0, 16)

 val combined = clientNonce + deviceNonce
 val hmac = TuyaCrypto.hmacSha256(localKey, combined)
 sendRaw(CMD_SESS_FINISH, hmac)
 val finishAck = receiveFrame()
 require(finishAck.command == CMD_SESS_FINISH_ACK) { "Expected 0x06." }
 if (finishAck.payload.size >= 32) {
 val expected = TuyaCrypto.hmacSha256(localKey, deviceNonce + clientNonce)
 require(TuyaCrypto.fixedTimeEqual(expected, finishAck.payload.copyOfRange(0, 32))) {
 "Session-key HMAC verification failed (wrong local key)."
 }
 }
 val xor = ByteArray(16) { i -> (clientNonce[i].toInt() xor deviceNonce[i].toInt()).toByte() }
 sessionKey = aesEcbEncrypt(localKey, xor)
 }

 private fun aesEcbEncrypt(key: ByteArray, block: ByteArray): ByteArray {
 val cipher = Cipher.getInstance("AES/ECB/NoPadding")
 cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key, "AES"))
 return cipher.doFinal(block)
 }

 private fun sendRaw(command: Int, payload: ByteArray) {
 val out = socket?.getOutputStream() ?: throw IllegalStateException("Not connected.")
 val frame = TuyaMessageCodec.encode(sequence++, command, payload)
 out.write(frame)
 out.flush()
 }

 private fun receiveFrame(): TuyaMessageCodec.TuyaFrame {
 val frameBytes = TuyaMessageCodec.tryReadFrame(input ?: throw IllegalStateException("Not connected."))
 ?: throw IllegalStateException("Connection closed before frame.")
 return TuyaMessageCodec.decode(frameBytes)
 }

 fun sendAndReceive(command: Int, plaintext: ByteArray): ByteArray {
 val key = sessionKey ?: throw IllegalStateException("No session key.")
 val nonce = TuyaCrypto.newNonce()
 val encLen = TuyaCrypto.GCM_NONCE_SIZE + plaintext.size + TuyaCrypto.GCM_TAG_SIZE
 val totalLength = 8 + encLen
 val ad = TuyaMessageCipher.buildAssociatedData(totalLength)
 val encrypted = TuyaMessageCipher.encrypt(key, plaintext, nonce, ad)
 sendRaw(command, encrypted)
 val response = receiveFrame()
 val respAd = TuyaMessageCipher.buildAssociatedData(response.payload.size + 8)
 return TuyaMessageCipher.decrypt(key, response.payload, respAd)
 }

 fun close() {
 try { input?.close() } catch (e: Exception) {}
 try { socket?.close() } catch (_: Exception) {}
 socket = null
 input = null
 sessionKey = null
 }
}
