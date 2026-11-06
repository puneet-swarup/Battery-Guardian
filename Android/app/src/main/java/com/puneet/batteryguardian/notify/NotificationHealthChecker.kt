package com.puneet.batteryguardian.notify

import android.app.NotificationManager
import android.content.Context
import android.os.PowerManager
import androidx.core.app.NotificationManagerCompat

// Read-only diagnostics for why alerts may be silently missing on a device.
//
// On several OEM ROMs (including Lenovo tablets on Android 10) notifications
// for sideloaded apps are disabled by default, or the alert channel is
// downgraded to IMPORTANCE_NONE, or battery optimisation kills the watchdog
// service. The alerting code swallows these failures, so the app previously
// gave no hint that anything was wrong. This checker surfaces the state so the
// UI can show a fix-it prompt.
//
// It performs no writes and changes no behaviour; it is safe to call at any
// time and never throws.
object NotificationHealthChecker {

    // A single diagnostic finding with a stable code for testing.
    enum class Issue {
        // App-level notifications are disabled by the user or the OS.
        NOTIFICATIONS_DISABLED,

        // The alert channel exists but has been blocked or downgraded.
        ALERT_CHANNEL_BLOCKED,

        // The service channel exists but has been blocked or downgraded.
        SERVICE_CHANNEL_BLOCKED,

        // The app is not exempt from battery optimisation (Doze may kill it).
        NOT_EXEMPT_FROM_BATTERY_OPTIMIZATION
    }

    // Inspects the current device state and returns every detected issue.
    // An empty list means notifications and the watchdog are healthy.
    fun detect(context: Context): List<Issue> {
        val issues = mutableListOf<Issue>()

        if (!areNotificationsEnabled(context)) {
            issues += Issue.NOTIFICATIONS_DISABLED
        }

        when (channelImportance(context, NotificationChannels.ALERTS)) {
            NotificationManager.IMPORTANCE_NONE -> issues += Issue.ALERT_CHANNEL_BLOCKED
            else -> Unit
        }

        when (channelImportance(context, NotificationChannels.SERVICE)) {
            NotificationManager.IMPORTANCE_NONE -> issues += Issue.SERVICE_CHANNEL_BLOCKED
            else -> Unit
        }

        if (!isExemptFromBatteryOptimization(context)) {
            issues += Issue.NOT_EXEMPT_FROM_BATTERY_OPTIMIZATION
        }

        return issues
    }

    // True when the app is allowed to post notifications.
    fun areNotificationsEnabled(context: Context): Boolean =
        try {
            NotificationManagerCompat.from(context).areNotificationsEnabled()
        } catch (e: Throwable) {
            false
        }

    // Returns the importance of a channel, or null when the channel has not
    // been created yet (which callers should treat as not blocked).
    fun channelImportance(context: Context, channelId: String): Int? =
        try {
            val manager = context.getSystemService(NotificationManager::class.java)
            manager?.getNotificationChannel(channelId)?.importance
        } catch (e: Throwable) {
            null
        }

    // True when the OS will let the foreground service keep running.
    fun isExemptFromBatteryOptimization(context: Context): Boolean =
        try {
            val pm = context.getSystemService(PowerManager::class.java)
            pm?.isIgnoringBatteryOptimizations(context.packageName) ?: false
        } catch (_: Throwable) {
            false
        }
}
