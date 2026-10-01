package com.puneet.batteryguardian.service

import android.app.Notification
import android.app.NotificationManager
import android.content.Context
import androidx.test.core.app.ApplicationProvider
import com.puneet.batteryguardian.R
import com.puneet.batteryguardian.battery.BatterySnapshot
import com.puneet.batteryguardian.battery.BatteryStatus
import com.puneet.batteryguardian.notify.NotificationChannels
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

/**
 * Robolectric tests for [OngoingNotification] and [NotificationChannels].
 * Verifies the foreground notification builds without a device and that the
 * status text is chosen correctly for each battery state.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class OngoingNotificationTest {

    private lateinit var context: Context

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
    }

    private fun snapshot(
        percent: Int,
        charging: Boolean = false,
        full: Boolean = false,
        onAc: Boolean = false
    ) = BatterySnapshot(
        percent = percent,
        isOnAcPower = onAc,
        isCharging = charging,
        isFull = full,
        status = when {
            charging -> BatteryStatus.CHARGING
            full -> BatteryStatus.FULL
            else -> BatteryStatus.DISCHARGING
        },
        health = null
    )

    private fun textOf(notification: Notification): String =
        notification.extras.getString(Notification.EXTRA_TEXT) ?: ""

    @Test
    fun `builds a notification for a charging battery`() {
        val n = OngoingNotification.build(context, snapshot(50, charging = true, onAc = true))
        assertNotNull(n)
        assertEquals(context.getString(R.string.status_charging), notificationStatus(n))
    }

    @Test
    fun `builds a notification for a full battery`() {
        val n = OngoingNotification.build(context, snapshot(100, full = true, onAc = true))
        assertEquals(context.getString(R.string.status_full), notificationStatus(n))
    }

    @Test
    fun `builds a notification for a discharging battery`() {
        val n = OngoingNotification.build(context, snapshot(40))
        assertEquals(context.getString(R.string.status_discharging), notificationStatus(n))
    }

    @Test
    fun `builds a notification for an unknown battery`() {
        val n = OngoingNotification.build(context, BatterySnapshot.UNKNOWN)
        assertEquals(context.getString(R.string.status_unknown), notificationStatus(n))
    }

    @Test
    fun `notification is ongoing`() {
        val n = OngoingNotification.build(context, snapshot(40))
        val ongoing = Notification.FLAG_ONGOING_EVENT.toLong()
        assertEquals(ongoing, n.flags.toLong() and ongoing)
    }

    @Test
    fun `notification includes the battery percent when known`() {
        val n = OngoingNotification.build(context, snapshot(37))
        assertTrue("Expected percent in '${textOf(n)}'", textOf(n).contains("37"))
    }

    @Test
    fun `unknown battery omits the percent`() {
        val n = OngoingNotification.build(context, BatterySnapshot.UNKNOWN)
        assertFalse(textOf(n).contains("-1"))
    }

    @Test
    fun `update posts without throwing`() {
        OngoingNotification.update(context, snapshot(55, charging = true, onAc = true))
        val manager = context.getSystemService(NotificationManager::class.java)
        assertNotNull(manager.getNotificationChannel(NotificationChannels.SERVICE))
    }

    @Test
    fun `notification channel ids are stable`() {
        assertEquals("bg_alerts", NotificationChannels.ALERTS)
        assertEquals("bg_service", NotificationChannels.SERVICE)
    }

    @Test
    fun `ensureCreated registers both channels`() {
        NotificationChannels.ensureCreated(context)
        val manager = context.getSystemService(NotificationManager::class.java)
        assertNotNull(manager.getNotificationChannel(NotificationChannels.ALERTS))
        assertNotNull(manager.getNotificationChannel(NotificationChannels.SERVICE))
    }

    @Test
    fun `ensureCreated is idempotent`() {
        NotificationChannels.ensureCreated(context)
        NotificationChannels.ensureCreated(context)
        val manager = context.getSystemService(NotificationManager::class.java)
        assertNotNull(manager.getNotificationChannel(NotificationChannels.ALERTS))
    }

    /**
     * Extracts the status word from the notification text. The formatted string
     * is "Battery at NN% \u00B7 Status", so the status follows the middle dot.
     */
    private fun notificationStatus(notification: Notification): String {
        val text = textOf(notification)
        return if (text.contains("\u00B7")) text.substringAfter("\u00B7").trim() else text.trim()
    }
}
