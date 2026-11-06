package com.puneet.batteryguardian.ui

import android.content.Context
import com.puneet.batteryguardian.R
import com.puneet.batteryguardian.notify.NotificationChannels
import com.puneet.batteryguardian.notify.NotificationHealthChecker

// Pure presentation logic for the notification-health banner on the
// main screen. Keeps the activity free of decision logic and gives us a
// testable unit: given a list of issues, what should the user see and
// which action should the button perform?

enum class BannerAction {
    // Open the app's notification settings page.
    OPEN_APP_NOTIFICATION_SETTINGS,

    // Open the alerts channel specific settings page.
    OPEN_CHANNEL_SETTINGS,

    // Open the system battery-optimisation exemption dialog.
    REQUEST_BATTERY_EXEMPTION,

    // No deep-link available; the button is hidden.
    NONE
}

data class HealthBanner(
    val title: String,
    val message: String,
    val actionLabel: String,
    val action: BannerAction,
    val channelId: String?
)

// Builds the banner for the highest-priority issue in the list.
// Priority order (most-urgent first):
//   1. NOTIFICATIONS_DISABLED       (nothing posts at all)
//   2. ALERT_CHANNEL_BLOCKED        (alerts visible but silent)
//   3. SERVICE_CHANNEL_BLOCKED       (ongoing notification hidden)
//   4. NOT_EXEMPT_FROM_BATTERY_OPTIMIZATION
// Returns null when there is nothing to report.
object HealthBannerPresenter {

    private val PRIORITY: List<NotificationHealthChecker.Issue> = listOf(
        NotificationHealthChecker.Issue.NOTIFICATIONS_DISABLED,
        NotificationHealthChecker.Issue.ALERT_CHANNEL_BLOCKED,
        NotificationHealthChecker.Issue.SERVICE_CHANNEL_BLOCKED,
        NotificationHealthChecker.Issue.NOT_EXEMPT_FROM_BATTERY_OPTIMIZATION
    )

    // Returns the highest-priority banner for the given issues, or null.
    fun present(context: Context, issues: List<NotificationHealthChecker.Issue>): HealthBanner? {
        if (issues.isEmpty()) return null
        val highest = PRIORITY.firstOrNull { issues.contains(it) } ?: return null

        return when (highest) {
            NotificationHealthChecker.Issue.NOTIFICATIONS_DISABLED -> HealthBanner(
                title = context.getString(R.string.health_notifications_disabled_title),
                message = context.getString(R.string.health_notifications_disabled_message),
                actionLabel = context.getString(R.string.health_fix_button),
                action = BannerAction.OPEN_APP_NOTIFICATION_SETTINGS,
                channelId = null
            )

            NotificationHealthChecker.Issue.ALERT_CHANNEL_BLOCKED -> HealthBanner(
                title = context.getString(R.string.health_channel_blocked_title),
                message = context.getString(R.string.health_channel_blocked_message),
                actionLabel = context.getString(R.string.health_fix_button),
                action = BannerAction.OPEN_CHANNEL_SETTINGS,
                channelId = NotificationChannels.ALERTS
            )

            NotificationHealthChecker.Issue.SERVICE_CHANNEL_BLOCKED -> HealthBanner(
                title = context.getString(R.string.health_channel_blocked_title),
                message = context.getString(R.string.health_channel_blocked_message),
                actionLabel = context.getString(R.string.health_fix_button),
                action = BannerAction.OPEN_CHANNEL_SETTINGS,
                channelId = NotificationChannels.SERVICE
            )

            NotificationHealthChecker.Issue.NOT_EXEMPT_FROM_BATTERY_OPTIMIZATION -> HealthBanner(
                title = context.getString(R.string.health_battery_opt_title),
                message = context.getString(R.string.health_battery_opt_message),
                actionLabel = context.getString(R.string.health_fix_button),
                action = BannerAction.REQUEST_BATTERY_EXEMPTION,
                channelId = null
            )
        }
    }
}
