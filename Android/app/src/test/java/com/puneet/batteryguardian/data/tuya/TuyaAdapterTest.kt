package com.puneet.batteryguardian.data.tuya

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The pure parts of the smart-plug adapter: the relay mapping and key parsing.
 * The protocol itself is the SDK's, tested in the SDK repository.
 */
class TuyaAdapterTest {

    @Test
    fun relayMappingIsTriState() {
        assertEquals(RelayState.ON, TuyaSmartPlug.mapRelay(true))
        assertEquals(RelayState.OFF, TuyaSmartPlug.mapRelay(false))
        assertEquals(RelayState.UNKNOWN, TuyaSmartPlug.mapRelay(null))
    }

    @Test
    fun sixteenCharacterKeyIsAcceptedAsIs() {
        val key = TuyaLocalKey.parse("0123456789abcdef")
        assertEquals(16, key.size)
        assertEquals('0'.code.toByte(), key[0])
        assertEquals('f'.code.toByte(), key[15])
    }

    @Test
    fun thirtyTwoHexDigitsBecomeSixteenBytes() {
        val key = TuyaLocalKey.parse("000102030405060708090a0b0c0d0e0f")
        assertEquals(16, key.size)
        assertEquals(0x00.toByte(), key[0])
        assertEquals(0x0f.toByte(), key[15])
    }

    @Test
    fun keyIsTrimmed() {
        assertEquals(16, TuyaLocalKey.parse("  0123456789abcdef  ").size)
    }

    @Test
    fun sixteensAsciiCharsAreNotMistakenForHex() {
        // 16 chars that happen to be hex are still the 16-character form
        assertArrayEquals(
            "0123456789abcdef".toByteArray(Charsets.US_ASCII),
            TuyaLocalKey.parse("0123456789abcdef")
        )
    }

    @Test(expected = IllegalArgumentException::class)
    fun emptyKeyIsRejected() {
        TuyaLocalKey.parse("")
    }

    @Test(expected = IllegalArgumentException::class)
    fun shortKeyIsRejected() {
        TuyaLocalKey.parse("short")
    }

    @Test
    fun thirtyTwoHexDigitsAreNotMistakenForAStringKey() {
        // "0123456789abcdef" twice over looks like a 32-character key but is
        // valid hex, so it is read as 16 bytes. The ambiguity is inherent to
        // the format, not a bug in the parser.
        // Hex digits pair up: 01 23 45 ... ef 01 23 ... ef
        val key = TuyaLocalKey.parse("0123456789abcdef0123456789abcdef")
        assertEquals(16, key.size)
        assertEquals(0x01.toByte(), key[0])
        assertEquals(0x23.toByte(), key[1])
        assertEquals(0xef.toByte(), key[15])
    }

    @Test(expected = IllegalArgumentException::class)
    fun thirtyTwoNonHexCharsAreRejected() {
        // 32 bytes is not an AES-128 key; the old code accepted this silently
        TuyaLocalKey.parse("0123456789abcdefghijklmnopqrstuv")
    }
}
