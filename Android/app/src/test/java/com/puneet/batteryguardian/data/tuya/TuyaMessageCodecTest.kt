package com.puneet.batteryguardian.data.tuya

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.ByteArrayInputStream

class TuyaMessageCodecTest {
 @Test
 fun encodeDecodeRoundTrip() {
 val payload = byteArrayOf(1, 2, 3, 4, 5)
 val frame = TuyaMessageCodec.encode(7, 8, payload)
 val decoded = TuyaMessageCodec.decode(frame)
 assertEquals(7, decoded.sequence)
 assertEquals(8, decoded.command)
 assertArrayEquals(payload, decoded.payload)
 }

 @Test
 fun encodeUses6699Prefix() {
 val frame = TuyaMessageCodec.encode(1, 7, ByteArray(0))
 assertEquals(0x00, frame[0].toInt() and 0xFF)
 assertEquals(0x00, frame[1].toInt() and 0xFF)
 assertEquals(0x66, frame[2].toInt() and 0xFF)
 assertEquals(0x99, frame[3].toInt() and 0xFF)
 }

 @Test(expected = IllegalArgumentException::class)
 fun decodeRejectsBadPrefix() {
 val bad = byteArrayOf(0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0, 7)
 TuyaMessageCodec.decode(bad)
 }

 @Test(expected = IllegalArgumentException::class)
 fun decodeRejectsShortFrame() {
 TuyaMessageCodec.decode(byteArrayOf(0, 0, 0, 1))
 }

 @Test
 fun tryReadFrameReadsOneFrame() {
 val frame = TuyaMessageCodec.encode(3, 8, byteArrayOf(9, 9))
 val input = ByteArrayInputStream(frame)
 val read = TuyaMessageCodec.tryReadFrame(input)
 assertTrue(read != null)
 assertArrayEquals(frame, read)
 assertNull(TuyaMessageCodec.tryReadFrame(input))
 }

 @Test
 fun multipleFramesInOneStream() {
 val f1 = TuyaMessageCodec.encode(1, 7, byteArrayOf(1))
 val f2 = TuyaMessageCodec.encode(2, 8, byteArrayOf(2))
 val input = ByteArrayInputStream(f1 + f2)
 val r1 = TuyaMessageCodec.tryReadFrame(input)
 val r2 = TuyaMessageCodec.tryReadFrame(input)
 assertArrayEquals(f1, r1)
 assertArrayEquals(f2, r2)
 }
}
