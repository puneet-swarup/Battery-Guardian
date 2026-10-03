package com.puneet.batteryguardian.data

import android.app.NotificationManager
import android.content.Context
import com.puneet.batteryguardian.core.DoNotDisturbDetector

/**
 * Reads the Android notification interruption filter to determine whether the
 * device is currently in Do Not Disturb (priority / silent) mode.
 *
 * Best-effort: any failure returns false so alerts are never silently swallowed
 * by a platform quirk.
 */
class NotificationDndDetector(context: Context) : DoNotDisturbDetector {

    private val notificationManager =
        context.applicationContext.getSystemService(Context.NOTIFICATION_SERVICE) as? NotificationManager

    override fun isDoNotDisturbActive(): Boolean {
        val manager = notificationManager ?: return false
        return try {
            val filter = manager.currentInterruptionFilter
            filter == NotificationManager.INTERRUPTION_FILTER_NONE ||
                filter == NotificationManager.INTERRUPTION_FILTER_PRIORITY ||
                filter == NotificationManager.INTERRUPTION_FILTER_ALARMS
        } catch (t: Throwable) {
            false
        }
    }
}
