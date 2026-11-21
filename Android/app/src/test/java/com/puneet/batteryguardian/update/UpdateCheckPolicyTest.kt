package com.puneet.batteryguardian.update

import com.puneet.batteryguardian.data.Settings
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

/**
 * The automatic update check never ran on any install: the receiver was
 * declared in the manifest and nothing sent it the broadcast. These tests pin
 * the policy that now decides when to fire it.
 */
class UpdateCheckPolicyTest {

    private val now = Instant.parse("2026-11-21T10:00:00Z")

    @Test
    fun aFirstRunIsDue() {
        assertTrue(UpdateCheckPolicy.isDue(Settings(checkForUpdatesAutomatically = true), now))
    }

    @Test
    fun nothingIsDueWhenTheUserTurnedItOff() {
        val settings = Settings(checkForUpdatesAutomatically = false, lastUpdateCheckUtc = null)
        assertFalse(UpdateCheckPolicy.isDue(settings, now))
    }

    @Test
    fun aCheckWithinTheDayIsNotDue() {
        val settings = Settings(
            checkForUpdatesAutomatically = true,
            lastUpdateCheckUtc = now.minusSeconds(23 * 3600)
        )
        assertFalse(UpdateCheckPolicy.isDue(settings, now))
    }

    @Test
    fun aCheckOlderThanADayIsDue() {
        val settings = Settings(
            checkForUpdatesAutomatically = true,
            lastUpdateCheckUtc = now.minusSeconds(24 * 3600)
        )
        assertTrue(UpdateCheckPolicy.isDue(settings, now))
    }

    @Test
    fun aTimestampInTheFutureDoesNotBlockForever() {
        val settings = Settings(
            checkForUpdatesAutomatically = true,
            lastUpdateCheckUtc = now.plusSeconds(3600)
        )
        assertTrue("a clock that jumped backwards must not silence updates for a day",
            UpdateCheckPolicy.isDue(settings, now))
    }
}
