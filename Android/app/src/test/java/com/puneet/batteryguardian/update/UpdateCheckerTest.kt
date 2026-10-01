package com.puneet.batteryguardian.update

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Unit tests for version tag parsing and comparison. Ported from
 * UpdateServiceTests.cs and extended with edge cases.
 */
class UpdateCheckerTest {

    @Test
    fun `parses full semantic tag`() {
        assertEquals(listOf(1, 9, 0), UpdateChecker.parseTagToVersion("v1.9.0"))
    }

    @Test
    fun `parses tag without v prefix`() {
        assertEquals(listOf(1, 9, 0), UpdateChecker.parseTagToVersion("1.9.0"))
    }

    @Test
    fun `parses uppercase V prefix`() {
        assertEquals(listOf(1, 2, 3), UpdateChecker.parseTagToVersion("V1.2.3"))
    }

    @Test
    fun `parses major minor only`() {
        assertEquals(listOf(1, 9, 0), UpdateChecker.parseTagToVersion("1.9"))
    }

    @Test
    fun `parses major only`() {
        assertEquals(listOf(2, 0, 0), UpdateChecker.parseTagToVersion("v2"))
    }

    @Test
    fun `strips suffix from prerelease tag`() {
        assertEquals(listOf(1, 0, 0), UpdateChecker.parseTagToVersion("v1.0.0-beta"))
    }

    @Test
    fun `strips build metadata suffix`() {
        assertEquals(listOf(1, 4, 2), UpdateChecker.parseTagToVersion("v1.4.2+build.9"))
    }

    @Test
    fun `trims surrounding whitespace`() {
        assertEquals(listOf(3, 1, 4), UpdateChecker.parseTagToVersion("  v3.1.4  "))
    }

    @Test
    fun `ignores parts beyond the third`() {
        assertEquals(listOf(1, 2, 3), UpdateChecker.parseTagToVersion("v1.2.3.4.5"))
    }

    @Test
    fun `returns null for blank tag`() {
        assertNull(UpdateChecker.parseTagToVersion(""))
        assertNull(UpdateChecker.parseTagToVersion("   "))
    }

    @Test
    fun `returns null for non numeric tag`() {
        assertNull(UpdateChecker.parseTagToVersion("latest"))
    }

    @Test
    fun `returns null for a lone v prefix`() {
        assertNull(UpdateChecker.parseTagToVersion("v"))
    }

    @Test
    fun `stops at first non numeric component`() {
        // "1.x.3" -> parses 1 then stops, pads with zeros.
        assertEquals(listOf(1, 0, 0), UpdateChecker.parseTagToVersion("1.x.3"))
    }

    @Test
    fun `isNewer detects a greater major`() {
        assertTrue(UpdateChecker.isNewer(listOf(2, 0, 0), listOf(1, 9, 9)))
    }

    @Test
    fun `isNewer detects a greater minor`() {
        assertTrue(UpdateChecker.isNewer(listOf(1, 10, 0), listOf(1, 9, 0)))
    }

    @Test
    fun `isNewer detects a greater patch`() {
        assertTrue(UpdateChecker.isNewer(listOf(1, 9, 1), listOf(1, 9, 0)))
    }

    @Test
    fun `isNewer is false for equal versions`() {
        assertFalse(UpdateChecker.isNewer(listOf(1, 9, 0), listOf(1, 9, 0)))
    }

    @Test
    fun `isNewer is false for older versions`() {
        assertFalse(UpdateChecker.isNewer(listOf(1, 8, 9), listOf(1, 9, 0)))
    }

    @Test
    fun `isNewer handles differing list lengths`() {
        assertFalse(UpdateChecker.isNewer(listOf(1, 0), listOf(1, 0, 0)))
        assertTrue(UpdateChecker.isNewer(listOf(1, 1), listOf(1, 0, 9)))
    }

    @Test
    fun `isNewer major beats any minor or patch`() {
        assertTrue(UpdateChecker.isNewer(listOf(2, 0, 0), listOf(1, 99, 99)))
    }

    @Test
    fun `parse then compare round trip detects upgrade`() {
        val remote = UpdateChecker.parseTagToVersion("v1.10.0")
        val current = UpdateChecker.parseTagToVersion("v1.9.9")
        assertTrue(UpdateChecker.isNewer(remote!!, current!!))
    }

    @Test
    fun `update info data class exposes all fields`() {
        val info = UpdateInfo(
            latestVersion = "v2.0.0",
            releaseUrl = "https://example.com/release",
            releaseNotes = "notes",
            downloadUrl = "https://example.com/app.apk"
        )
        assertEquals("v2.0.0", info.latestVersion)
        assertEquals("https://example.com/release", info.releaseUrl)
        assertEquals("notes", info.releaseNotes)
        assertEquals("https://example.com/app.apk", info.downloadUrl)
    }

    @Test
    fun `update info allows null download url`() {
        val info = UpdateInfo("v2.0.0", "u", "n", null)
        assertNull(info.downloadUrl)
    }
}
