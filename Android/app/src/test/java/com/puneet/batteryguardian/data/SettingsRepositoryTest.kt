package com.puneet.batteryguardian.data

import androidx.test.core.app.ApplicationProvider
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import android.content.Context
import java.time.Instant

/**
 * Robolectric tests for [SettingsRepository]. These exercise the real
 * SharedPreferences round-trip on the JVM, so no device is needed.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class SettingsRepositoryTest {

    private lateinit var context: Context
    private lateinit var repository: SettingsRepository

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        // Start from a clean slate for each test.
        context.getSharedPreferences("battery_guardian_settings", Context.MODE_PRIVATE)
            .edit().clear().commit()
        repository = SettingsRepository(context)
    }

    @Test
    fun `load returns defaults when nothing is stored`() {
        val s = repository.load()
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
    fun `save then load round-trips every field`() {
        val lastCheck = Instant.parse("2026-01-01T10:00:00Z")
        val snooze = Instant.parse("2026-01-01T11:00:00Z")
        val original = Settings(
            highBatteryThreshold = 88,
            lowBatteryThreshold = 22,
            alertRepeatIntervalSeconds = 600,
            playSound = false,
            speakAlert = false,
            showNotification = false,
            vibrate = false,
            checkForUpdatesAutomatically = false,
            lastUpdateCheckUtc = lastCheck,
            diagnosticLoggingEnabled = true,
            snoozedUntilUtc = snooze,
            monitoringEnabled = true
        )
        repository.save(original)
        assertEquals(original, repository.load())
    }

    @Test
    fun `update applies a transform to persisted settings`() {
        repository.save(Settings(highBatteryThreshold = 80))
        repository.update { it.copy(highBatteryThreshold = 90) }
        assertEquals(90, repository.load().highBatteryThreshold)
    }

    @Test
    fun `setMonitoringEnabled persists the flag`() {
        repository.setMonitoringEnabled(true)
        assertTrue(repository.load().monitoringEnabled)
        repository.setMonitoringEnabled(false)
        assertFalse(repository.load().monitoringEnabled)
    }

    @Test
    fun `setSnoozedUntil persists an instant`() {
        val until = Instant.parse("2026-05-05T05:05:05Z")
        repository.setSnoozedUntil(until)
        assertEquals(until, repository.load().snoozedUntilUtc)
    }

    @Test
    fun `clearSnooze removes the snooze instant`() {
        repository.setSnoozedUntil(Instant.parse("2026-05-05T05:05:05Z"))
        repository.clearSnooze()
        assertNull(repository.load().snoozedUntilUtc)
    }

    @Test
    fun `setSnoozedUntil with null clears the value`() {
        repository.setSnoozedUntil(Instant.now())
        repository.setSnoozedUntil(null)
        assertNull(repository.load().snoozedUntilUtc)
    }

    @Test
    fun `setLastUpdateCheck persists the instant`() {
        val instant = Instant.parse("2026-02-02T02:02:02Z")
        repository.setLastUpdateCheck(instant)
        assertEquals(instant, repository.load().lastUpdateCheckUtc)
    }

    @Test
    fun `snooze survives a fresh repository instance`() {
        val until = Instant.parse("2026-06-06T06:06:06Z")
        repository.setSnoozedUntil(until)
        val second = SettingsRepository(context)
        assertEquals(until, second.load().snoozedUntilUtc)
    }

    @Test
    fun `lastUpdateCheck null by default is not written`() {
        // Saving a settings object with null lastUpdateCheck must remove the key.
        repository.save(Settings(lastUpdateCheckUtc = Instant.now()))
        repository.save(Settings(lastUpdateCheckUtc = null))
        assertNull(repository.load().lastUpdateCheckUtc)
    }
}
