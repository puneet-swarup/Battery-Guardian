package com.puneet.batteryguardian.ui

import android.content.Context
import androidx.test.core.app.ApplicationProvider
import com.puneet.batteryguardian.notify.NotificationChannels
import com.puneet.batteryguardian.notify.NotificationHealthChecker
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

/**
 * Tests for the pure presentation logic that turns NotificationHealthChecker
 * issues into a user-visible banner and picks the correct fix-it action.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class HealthBannerPresenterTest {

    private lateinit var context: Context

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        NotificationChannels.ensureCreated(context)
    }

    @Test
    fun `no issues means no banner`() {
        assertNull(HealthBannerPresenter.present(context, emptyList()))
    }

    @Test
    fun `notifications disabled produces an app-settings banner`() {
        val banner = HealthBannerPresenter.present(
            context,
            listOf(NotificationHealthChecker.Issue.NOTIFICATIONS_DISABLED)
        )
        assertNotNull(banner)
        assertEquals(BannerAction.OPEN_APP_NOTIFICATION_SETTINGS, banner!!.action)
        assertNull(banner!!.channelId)
    }

    @Test
    fun `blocked alert channel targets the alerts channel`() {
        val banner = HealthBannerPresenter.present(
            context,
            listOf(NotificationHealthChecker.Issue.ALERT_CHANNEL_BLOCKED)
        )
        assertNotNull(banner)
        assertEquals(BannerAction.OPEN_CHANNEL_SETTINGS, banner!!.action)
        assertEquals(NotificationChannels.ALERTS, banner!!.channelId)
    }

    @Test
    fun `blocked service channel targets the service channel`() {
        val banner = HealthBannerPresenter.present(
            context,
            listOf(NotificationHealthChecker.Issue.SERVICE_CHANNEL_BLOCKED)
        )
        assertNotNull(banner)
        assertEquals(BannerAction.OPEN_CHANNEL_SETTINGS, banner!!.action)
        assertEquals(NotificationChannels.SERVICE, banner!!.channelId)
    }

    @Test
    fun `battery optimisation issue produces the exemption action`() {
        val banner = HealthBannerPresenter.present(
            context,
            listOf(NotificationHealthChecker.Issue.NOT_EXEMPT_FROM_BATTERY_OPTIMIZATION)
        )
        assertNotNull(banner)
        assertEquals(BannerAction.REQUEST_BATTERY_EXEMPTION, banner!!.action)
    }

    @Test
    fun `notifications disabled outranks a blocked channel`() {
        val banner = HealthBannerPresenter.present(
            context,
            listOf(
                NotificationHealthChecker.Issue.ALERT_CHANNEL_BLOCKED,
                NotificationHealthChecker.Issue.NOTIFICATIONS_DISABLED
            )
        )
        assertNotNull(banner)
        assertEquals(BannerAction.OPEN_APP_NOTIFICATION_SETTINGS, banner!!.action)
    }
}
