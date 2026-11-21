package com.puneet.batteryguardian.data.tuya

/**
 * Turns the local key the user pastes in Settings into the 16 bytes the
 * protocol needs. Accepts the 16-character key from the Smart Life app, or the
 * same key written as 32 hex digits.
 *
 * A 32-character ASCII string is deliberately not accepted: an AES-128 key is
 * 16 bytes, and the old code quietly produced a 32-byte key that could never
 * work.
 */
object TuyaLocalKey {
    fun parse(localKey: String): ByteArray {
        val key = localKey.trim()
        require(key.isNotEmpty()) { "Local key must not be empty" }

        if (key.length == 32 && isHex(key)) return hexToBytes(key)
        if (key.length == 16) return key.toByteArray(Charsets.US_ASCII)

        throw IllegalArgumentException(
            "Invalid local key: expected 16 characters or 32 hex digits, got ${key.length}"
        )
    }

    private fun isHex(s: String): Boolean = s.all { c ->
        val code = c.code
        (code in 48..57) || (code in 97..102) || (code in 65..70)
    }

    private fun hexToBytes(s: String): ByteArray =
        ByteArray(s.length / 2) { i -> s.substring(i * 2, i * 2 + 2).toInt(16).toByte() }
}
