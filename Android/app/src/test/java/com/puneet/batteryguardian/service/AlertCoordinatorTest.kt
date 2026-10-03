package com.puneet.batteryguardian.service

import android.content.Context
import com.puneet.batteryguardian.battery.BatterySnapshot
import com.puneet.batteryguardian.battery.BatteryStatus
import com.puneet.batteryguardian.data.Settings
import com.puneet.batteryguardian.data.SettingsRepository
import com.puneet.batteryguardian.notify.AlertSink
import io.mockk.every
import io.mockk.mockk
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.time.Instant

/**
 * Tests the repeat and snooze logic of [AlertCoordinator] using a fake clock and
 * a recording [AlertSink]. This replaces the Windows app's runtime-only alert
 * timer behaviour with deterministic unit tests.
 */
class AlertCoordinatorTest {

    private class RecordingSink : AlertSink {
        val messages = mutableListOf<String>()
        val settingsSeen = mutableListOf<Settings>()
        val suppressedFlags = mutableListOf<Boolean>()
        var releaseCount = 0
        override fun notify(message: String, settings: Settings, suppressAudible: Boolean) {
            messages.add(message)
            settingsSeen.add(settings)
            suppressedFlags.add(suppressAudible)
        }
        override fun release() {
            releaseCount++
        }
    }

    private lateinit var context: Context
    private lateinit var repository: SettingsRepository
    private lateinit var sink: RecordingSink
    private var now: Instant = Instant.parse("2026-01-01T12:00:00Z")

    private var stored = Settings(
        highBatteryThreshold = 95,
        lowBatteryThreshold = 15,
        alertRepeatIntervalSeconds = 300
    )

    @Before
    fun setUp() {
        context = mockk(relaxed = true)
        repository = mockk(relaxed = true)
        sink = RecordingSink()
        stored = Settings(
            highBatteryThreshold = 95,
            lowBatteryThreshold = 15,
            alertRepeatIntervalSeconds = 300
        )
        every { repository.load() } answers { stored }
    }

    private fun coordinator() = AlertCoordinator(context, repository, sink, clock = { now })

    private fun snapshot(percent: Int, onAc: Boolean) = BatterySnapshot(
        percent = percent,
        isOnAcPower = onAc,
        isCharging = onAc,
        isFull = false,
        status = if (onAc) BatteryStatus.CHARGING else BatteryStatus.DISCHARGING,
        health = null
    )

    @Test
    fun `fires immediately when high alert starts`() {
        val result = coordinator().onBatterySnapshot(snapshot(96, onAc = true))
        assertTrue(result.alertActive)
        assertTrue(result.alertFired)
        assertEquals(1, sink.messages.size)
    }

    @Test
    fun `fires immediately when low alert starts`() {
        val result = coordinator().onBatterySnapshot(snapshot(10, onAc = false))
        assertTrue(result.alertActive)
        assertTrue(result.alertFired)
        assertTrue(sink.messages.first().contains("10%"))
    }

    @Test
    fun `does not repeat before interval elapses`() {
        val c = coordinator()
        c.onBatterySnapshot(snapshot(96, onAc = true))
        now = now.plusSeconds(100)
        val result = c.onBatterySnapshot(snapshot(97, onAc = true))
        assertFalse(result.alertFired)
        assertEquals(1, sink.messages.size)
    }

    @Test
    fun `repeats after interval elapses`() {
        val c = coordinator()
        c.onBatterySnapshot(snapshot(96, onAc = true))
        now = now.plusSeconds(301)
        val result = c.onBatterySnapshot(snapshot(97, onAc = true))
        assertTrue(result.alertFired)
        assertEquals(2, sink.messages.size)
    }

    @Test
    fun `repeats exactly at the interval boundary`() {
        val c = coordinator()
        c.onBatterySnapshot(snapshot(96, onAc = true))
        now = now.plusSeconds(300)
        val result = c.onBatterySnapshot(snapshot(96, onAc = true))
        assertTrue(result.alertFired)
        assertEquals(2, sink.messages.size)
    }

    @Test
    fun `clears when condition resolves`() {
        val c = coordinator()
        c.onBatterySnapshot(snapshot(96, onAc = true))
        val result = c.onBatterySnapshot(snapshot(90, onAc = false))
        assertFalse(result.alertActive)
        assertFalse(c.isAlertActive())
    }

    @Test
    fun `re-fires immediately when condition restarts`() {
        val c = coordinator()
        c.onBatterySnapshot(snapshot(96, onAc = true))
        // Resolve the alert.
        c.onBatterySnapshot(snapshot(90, onAc = false))
        // Condition returns within the old repeat window - should fire again.
        now = now.plusSeconds(30)
        val result = c.onBatterySnapshot(snapshot(97, onAc = true))
        assertTrue(result.alertFired)
        assertEquals(2, sink.messages.size)
    }

    @Test
    fun `snooze suppresses alerts`() {
        stored = stored.copy(snoozedUntilUtc = now.plusSeconds(600))
        val result = coordinator().onBatterySnapshot(snapshot(96, onAc = true))
        assertTrue(result.alertActive)
        assertTrue(result.snoozed)
        assertFalse(result.alertFired)
        assertEquals(0, sink.messages.size)
    }

    @Test
    fun `expired snooze allows alerts`() {
        stored = stored.copy(snoozedUntilUtc = now.minusSeconds(1))
        val result = coordinator().onBatterySnapshot(snapshot(96, onAc = true))
        assertTrue(result.alertFired)
        assertEquals(1, sink.messages.size)
    }

    @Test
    fun `low alert is not suppressed when high alert previously active`() {
        val c = coordinator()
        c.onBatterySnapshot(snapshot(96, onAc = true))
        // Transition straight from high to low condition.
        val result = c.onBatterySnapshot(snapshot(5, onAc = false))
        assertTrue(result.alertActive)
        assertTrue(result.alertFired)
        assertTrue(result.message.contains("5%"))
    }

    @Test
    fun `no alert in safe band resets message`() {
        val c = coordinator()
        val result = c.onBatterySnapshot(snapshot(50, onAc = false))
        assertFalse(result.alertActive)
        assertEquals("", result.message)
        assertFalse(result.alertFired)
    }

    @Test
    fun `reset clears active state`() {
        val c = coordinator()
        c.onBatterySnapshot(snapshot(96, onAc = true))
        assertTrue(c.isAlertActive())
        c.reset()
        assertFalse(c.isAlertActive())
    }

    @Test
    fun `notifier receives the current settings snapshot`() {
        stored = stored.copy(playSound = false, highBatteryThreshold = 90)
        coordinator().onBatterySnapshot(snapshot(95, onAc = true))
        assertEquals(1, sink.settingsSeen.size)
        assertFalse(sink.settingsSeen.first().playSound)
    }

    @Test
    fun `result carries the evaluated snapshot`() {
        val snap = snapshot(96, onAc = true)
        val result = coordinator().onBatterySnapshot(snap)
        assertEquals(snap, result.snapshot)
    }

    // ---- Audible suppression (quiet hours / DND) ----

    @Test
    fun `alert still fires when audible suppressed but flags the sink`() {
        val c = AlertCoordinator(context, repository, sink, { now }, isAudibleSuppressed = { true })
        val result = c.onBatterySnapshot(snapshot(96, onAc = true))
        assertTrue(result.alertFired)
        assertEquals(1, sink.messages.size)
        assertEquals(listOf(true), sink.suppressedFlags)
    }

    @Test
    fun `audible not suppressed by default`() {
        val result = coordinator().onBatterySnapshot(snapshot(96, onAc = true))
        assertTrue(result.alertFired)
        assertEquals(listOf(false), sink.suppressedFlags)
    }

    @Test
    fun `suppression predicate is evaluated per fire`() {
        var suppressed = false
        val c = AlertCoordinator(context, repository, sink, { now }, isAudibleSuppressed = { suppressed })
        c.onBatterySnapshot(snapshot(96, onAc = true))
        suppressed = true
        now = now.plusSeconds(301)
        c.onBatterySnapshot(snapshot(97, onAc = true))
        assertEquals(listOf(false, true), sink.suppressedFlags)
    }
}
