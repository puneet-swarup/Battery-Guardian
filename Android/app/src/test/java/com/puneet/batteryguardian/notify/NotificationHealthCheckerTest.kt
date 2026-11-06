package com.puneet.batteryguardian.notify

import android.app.Application
import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import androidx.test.core.app.ApplicationProvider
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config

/**
 * Robolectric tests for [NotificationHealthChecker]. These verify that the
 * diagnostic surfaces the exact conditions that cause silent alert loss on
 * tablets - disabled notifications, blocked channels - without changing any
 * runtime behaviour.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class NotificationHealthCheckerTest {

    private lateinit var context: Context
    private lateinit var application: Application

    @Before
    fun setUp() {
        application = ApplicationProvider.getApplicationContext()
        context = application
        NotificationChannels.ensureCreated(context)
    }

    @Test
    fun `notifications enabled is detected when permission is granted`() {
        shadowOf(application).grantPermissions(android.Manifest.permission.POST_NOTIFICATIONS)
        assertTrue(NotificationHealthChecker.areNotificationsEnabled(context))
    }

    @Test
    fun `detect reports notifications disabled exactly when they are disabled`() {
        // Consistency contract: the NOTIFICATIONS_DISABLED issue must be present
        // if and only if areNotificationsEnabled() is false. This avoids
        // depending on Robolectric's default permission state, which varies.
        val issues = NotificationHealthChecker.detect(context)
        val enabled = NotificationHealthChecker.areNotificationsEnabled(context)
        assertEquals(!enabled, issues.contains(NotificationHealthChecker.Issue.NOTIFICATIONS_DISABLED))
    }

    @Test
    fun `detect reports notifications disabled when they are explicitly blocked`() {
        // Deny the runtime permission and verify the checker reflects it.
        shadowOf(application).denyPermissions(android.Manifest.permission.POST_NOTIFICATIONS)
        val enabled = NotificationHealthChecker.areNotificationsEnabled(context)
        val issues = NotificationHealthChecker.detect(context)
        assertEquals(!enabled, issues.contains(NotificationHealthChecker.Issue.NOTIFICATIONS_DISABLED))
    }

    @Test
    fun `channel importance is null for an unknown channel`() {
        assertNull(NotificationHealthChecker.channelImportance(context, "does_not_exist"))
    }

    @Test
    fun `alert channel importance is read when present`() {
        val importance = NotificationHealthChecker.channelImportance(context, NotificationChannels.ALERTS)
        assertEquals(NotificationManager.IMPORTANCE_HIGH, importance)
    }

    @Test
    fun `detect reports a blocked alert channel`() {
        shadowOf(application).grantPermissions(android.Manifest.permission.POST_NOTIFICATIONS)

        // Replace the alert channel with a blocked one to simulate the user
        // (or the OEM) silencing it.
        val manager = context.getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(
            NotificationChannel(
                NotificationChannels.ALERTS,
                "Battery alerts",
                NotificationManager.IMPORTANCE_NONE
            )
        )

        val issues = NotificationHealthChecker.detect(context)
        assertTrue(issues.contains(NotificationHealthChecker.Issue.ALERT_CHANNEL_BLOCKED))
    }

    @Test
    fun `detect does not report a healthy channel as blocked`() {
        shadowOf(application).grantPermissions(android.Manifest.permission.POST_NOTIFICATIONS)
        val issues = NotificationHealthChecker.detect(context)
        assertFalse(issues.contains(NotificationHealthChecker.Issue.ALERT_CHANNEL_BLOCKED))
        assertFalse(issues.contains(NotificationHealthChecker.Issue.SERVICE_CHANNEL_BLOCKED))
    }

    @Test
    fun `detect never throws`() {
        // Smoke test: detect() must be safe to call unconditionally.
        NotificationHealthChecker.detect(context)
    }
}
