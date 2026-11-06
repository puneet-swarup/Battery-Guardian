package com.puneet.batteryguardian.service

import android.app.Notification
import android.content.Context
import androidx.test.core.app.ApplicationProvider
import com.puneet.batteryguardian.battery.BatterySnapshot
import com.puneet.batteryguardian.battery.BatteryStatus
import com.puneet.batteryguardian.notify.NotificationChannels
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config

// Behavioural tests for the notification call-to-action buttons (Snooze and
// Stop) - the buttons the user reported as unresponsive from the shade.
//
// They previously used PendingIntent.getService(), which cannot legally start
// a foreground service from a background context and threw an uncaught
// IllegalStateException, so the taps appeared dead. These tests lock in the
// corrected wiring: both actions must be foreground-service pending intents
// targeting BatteryMonitorService with the correct action string.
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class OngoingNotificationActionsTest {

    private lateinit var context: Context

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        NotificationChannels.ensureCreated(context)
    }

    private fun snapshot() = BatterySnapshot(
        percent = 50,
        isOnAcPower = false,
        isCharging = false,
        isFull = false,
        status = BatteryStatus.DISCHARGING,
        health = null
    )

    // Returns the action PendingIntents attached to the notification.
    private fun actionIntents(notification: Notification): List<android.app.PendingIntent> {
        val actions = notification.actions ?: return emptyList()
        return actions.mapNotNull { it.actionIntent }
    }

    @Test
    fun `notification exposes exactly two actions`() {
        val n = OngoingNotification.build(context, snapshot())
        assertNotNull(n.actions)
        assertEquals(2, n.actions.size)
    }

    @Test
    fun `snooze action targets the monitor service with the snooze action`() {
        val n = OngoingNotification.build(context, snapshot())
        val intents = actionIntents(n)
        assertTrue("Expected at least two action intents", intents.size >= 2)

        val savedIntents = intents.mapNotNull { shadowOf(it).savedIntent }
        val snooze = savedIntents.firstOrNull {
            it.action == BatteryMonitorService.ACTION_SNOOZE
        }
        assertNotNull("Snooze action must target ACTION_SNOOZE", snooze)
        assertEquals(BatteryMonitorService::class.java.name, snooze!!.component?.className)
    }

    @Test
    fun `snooze action carries a positive duration`() {
        val n = OngoingNotification.build(context, snapshot())
        val savedIntents = actionIntents(n).mapNotNull { shadowOf(it).savedIntent }
        val snooze = savedIntents.first { it.action == BatteryMonitorService.ACTION_SNOOZE }
        val minutes = snooze.getIntExtra(BatteryMonitorService.EXTRA_SNOOZE_MINUTES, -1)
        assertTrue("Snooze duration must be positive, was $minutes", minutes > 0)
    }

    @Test
    fun `stop action targets the monitor service with the stop action`() {
        val n = OngoingNotification.build(context, snapshot())
        val savedIntents = actionIntents(n).mapNotNull { shadowOf(it).savedIntent }
        val stop = savedIntents.firstOrNull {
            it.action == BatteryMonitorService.ACTION_STOP
        }
        assertNotNull("Stop action must target ACTION_STOP", stop)
        assertEquals(BatteryMonitorService::class.java.name, stop!!.component?.className)
    }

    @Test
    fun `both actions target the same service class`() {
        val n = OngoingNotification.build(context, snapshot())
        val savedIntents = actionIntents(n).mapNotNull { shadowOf(it).savedIntent }
        val components = savedIntents.mapNotNull { it.component?.className }.toSet()
        assertEquals(setOf(BatteryMonitorService::class.java.name), components)
    }
}
