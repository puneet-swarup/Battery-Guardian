package com.puneet.batteryguardian.data.tuya

import java.io.InputStream

object TuyaMessageCodec {
 const val PREFIX = 0x00006699
 const val HEADER_SIZE = 4
 const val RETURN_CODE_SIZE = 4
 const val MAX_PACKET_SIZE = 16 * 1024
 const val MAX_PAYLOAD_SIZE = MAX_PACKET_SIZE - 8 - RETURN_CODE_SIZE

 data class TuyaFrame(val sequence: Int, val command: Int, val payload: ByteArray) {
 override fun equals(other: Any?): Boolean {
 if (this === other) return true
 if (other !is TuyaFrame) return false
 return sequence == other.sequence && command == other.command && payload.contentEquals(other.payload)
 }
 override fun hashCode(): Int = 31 * (31 * sequence + command) + payload.contentHashCode()
 }

 fun encode(sequence: Int, command: Int, payload: ByteArray): ByteArray {
 require(payload.size <= MAX_PAYLOAD_SIZE) { "Payload too large." }
 require(sequence >= 0) { "Sequence must be non-negative." }
 val totalLength = 8 + payload.size
 val buf = ByteArray(16 + payload.size)
 writeInt(buf, 0, PREFIX)
 writeInt(buf, 4, totalLength)
 writeInt(buf, 8, sequence)
 writeInt(buf, 12, command)
 System.arraycopy(payload, 0, buf, 16, payload.size)
 return buf
 }

 fun decode(data: ByteArray): TuyaFrame {
 require(data.size >= 16) { "Frame too short." }
 val prefix = readInt(data, 0)
 require(prefix == PREFIX) { "Invalid prefix." }
 val totalLength = readInt(data, 4)
 require(totalLength >= 8) { "Invalid packet length." }
 require(8 + totalLength == data.size) { "Frame length mismatch." }
 val sequence = readInt(data, 8)
 val command = readInt(data, 12)
 val payload = data.copyOfRange(16, data.size)
 return TuyaFrame(sequence, command, payload)
 }

 fun tryReadFrame(input: InputStream): ByteArray? {
 val header = ByteArray(8)
 if (!readExactly(input, header, 0)) return null
 val prefix = readInt(header, 0)
 require(prefix == PREFIX) { "Invalid prefix." }
 val totalLength = readInt(header, 4)
 require(totalLength in 8..MAX_PACKET_SIZE) { "Invalid packet length." }
 val buf = ByteArray(8 + totalLength)
 System.arraycopy(header, 0, buf, 0, 8)
 if (!readExactly(input, buf, 8)) return null
 return buf
 }

 private fun readExactly(input: InputStream, buf: ByteArray, offset: Int): Boolean {
 var off = offset
 while (off < buf.size) {
 val read = input.read(buf, off, buf.size - off)
 if (read <= 0) return false
 off += read
 }
 return true
 }

 private fun writeInt(buf: ByteArray, offset: Int, value: Int) {
 buf[offset] = (value ushr 24).toByte()
 buf[offset + 1] = (value ushr 16).toByte()
 buf[offset + 2] = (value ushr 8).toByte()
 buf[offset + 3] = value.toByte()
 }

 private fun readInt(buf: ByteArray, offset: Int): Int =
 ((buf[offset].toInt() and 0xFF) shl 24) or
 ((buf[offset + 1].toInt() and 0xFF) shl 16) or
 ((buf[offset + 2].toInt() and 0xFF) shl 8) or
 (buf[offset + 3].toInt() and 0xFF)
}
