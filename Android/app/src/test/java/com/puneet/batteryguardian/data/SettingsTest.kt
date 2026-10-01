package com.puneet.batteryguardian.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

/**
 * Unit tests for the [Settings] model: defaults and snooze helpers.
 */
class SettingsTest {

    private val now: Instant = Instant.parse("2026-01-01T12:00:00Z")

    @Test
    fun `snooze is active when until is in the future`() {
        val settings = Settings(snoozedUntilUtc = now.plusSeconds(600))
        assertTrue(settings.isSnoozed(now))
    }

    @Test
    fun `snooze is inactive when until is in the past`() {
        val settings = Settings(snoozedUntilUtc = now.minusSeconds(600))
        assertFalse(settings.isSnoozed(now))
    }

    @Test
    fun `snooze is inactive when not set`() {
        val settings = Settings(snoozedUntilUtc = null)
        assertFalse(settings.isSnoozed())
    }

    @Test
    fun `snooze is inactive exactly at the expiry instant`() {
        // isAfter is strict, so an equal instant is not snoozed.
        val settings = Settings(snoozedUntilUtc = now)
        assertFalse(settings.isSnoozed(now))
    }

    @Test
    fun `snooze one millisecond in the future is active`() {
        val settings = Settings(snoozedUntilUtc = now.plusMillis(1))
        assertTrue(settings.isSnoozed(now))
    }

    @Test
    fun `defaults match windows application`() {
        val s = Settings()
        assertEquals(95, s.highBatteryThreshold)
        assertEquals(15, s.lowBatteryThreshold)
        assertEquals(300, s.alertRepeatIntervalSeconds)
        assertTrue(s.playSound)
        assertTrue(s.speakAlert)
        assertTrue(s.showNotification)
        assertTrue(s.vibrate)
        assertTrue(s.checkForUpdatesAutomatically)
        assertFalse(s.diagnosticLoggingEnabled)
        assertFalse(s.monitoringEnabled)
        assertNull(s.lastUpdateCheckUtc)
        assertNull(s.snoozedUntilUtc)
    }

    @Test
    fun `copy preserves unrelated fields`() {
        val original = Settings(highBatteryThreshold = 80, lowBatteryThreshold = 20)
        val changed = original.copy(highBatteryThreshold = 90)
        assertEquals(90, changed.highBatteryThreshold)
        assertEquals(20, changed.lowBatteryThreshold)
        assertEquals(original.alertRepeatIntervalSeconds, changed.alertRepeatIntervalSeconds)
    }

    @Test
    fun `data class equality is value based`() {
        assertEquals(Settings(), Settings())
        assertFalse(Settings(highBatteryThreshold = 90) == Settings(highBatteryThreshold = 91))
    }
}
