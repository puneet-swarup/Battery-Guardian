package com.puneet.batteryguardian.notify

import android.app.Application
import android.app.NotificationManager
import android.content.Context
import androidx.test.core.app.ApplicationProvider
import com.puneet.batteryguardian.data.Settings
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config

/**
 * Robolectric tests for [AlertNotifier]. These cover the channel dispatch logic
 * (notification on/off) and the TTS init/queue paths that previously caused
 * voice alerts to be silently dropped on Android 11+.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class AlertNotifierTest {

    private lateinit var context: Context
    private lateinit var application: Application
    private lateinit var notifier: AlertNotifier

    @Before
    fun setUp() {
        application = ApplicationProvider.getApplicationContext()
        context = application
        NotificationChannels.ensureCreated(context)
        notifier = AlertNotifier(context)
    }

    private fun notifications() =
        shadowOf(context.getSystemService(NotificationManager::class.java))

    @Test
    fun `blank message is a no-op`() {
        notifier.notify("   ", Settings())
        assertEquals(0, notifications().size())
    }

    @Test
    fun `posts a notification when enabled and permission granted`() {
        shadowOf(application).grantPermissions(android.Manifest.permission.POST_NOTIFICATIONS)
        notifier.notify(
            "Battery is at 96%. Consider unplugging the charger.",
            Settings(showNotification = true, playSound = false, vibrate = false, speakAlert = false)
        )
        assertEquals(1, notifications().size())
    }

    @Test
    fun `skips notification when disabled`() {
        shadowOf(application).grantPermissions(android.Manifest.permission.POST_NOTIFICATIONS)
        notifier.notify(
            "Battery is low at 10%. Please connect the charger.",
            Settings(showNotification = false, playSound = false, vibrate = false, speakAlert = false)
        )
        assertEquals(0, notifications().size())
    }

    @Test
    fun `alert channel is created before posting`() {
        val manager = context.getSystemService(NotificationManager::class.java)
        assertNotNull(manager.getNotificationChannel(NotificationChannels.ALERTS))
    }

    @Test
    fun `notify with all channels enabled does not throw`() {
        shadowOf(application).grantPermissions(android.Manifest.permission.POST_NOTIFICATIONS)
        notifier.notify(
            "Battery is at 97%.",
            Settings(showNotification = true, playSound = true, vibrate = true, speakAlert = true)
        )
        // Nothing to assert beyond not throwing; the notification path is covered above.
    }

    @Test
    fun `release shuts down cleanly`() {
        notifier.release()
        // Subsequent notifies must not crash after shutdown.
        notifier.notify("after release", Settings(showNotification = false, playSound = false, vibrate = false, speakAlert = false))
    }
}
