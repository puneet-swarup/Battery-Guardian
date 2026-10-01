package com.puneet.batteryguardian.service

import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import androidx.core.app.NotificationCompat
import com.puneet.batteryguardian.R
import com.puneet.batteryguardian.battery.BatterySnapshot
import com.puneet.batteryguardian.notify.NotificationChannels
import com.puneet.batteryguardian.ui.MainActivity

/**
 * Builds the persistent notification that represents the foreground service and
 * shows the current battery level. This is the Android substitute for the
 * Windows system-tray icon.
 */
object OngoingNotification {

    const val NOTIF_ID_SERVICE = 1000
    private const val REQ_CONTENT = 2000
    private const val REQ_STOP = 2002
    private const val REQ_SNOOZE = 2003

    fun build(context: Context, snapshot: BatterySnapshot): android.app.Notification {
        val contentIntent = PendingIntent.getActivity(
            context,
            REQ_CONTENT,
            Intent(context, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
            },
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val stopIntent = PendingIntent.getService(
            context,
            REQ_STOP,
            Intent(context, BatteryMonitorService::class.java).setAction(BatteryMonitorService.ACTION_STOP),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val snoozeIntent = PendingIntent.getService(
            context,
            REQ_SNOOZE,
            Intent(context, BatteryMonitorService::class.java).setAction(BatteryMonitorService.ACTION_SNOOZE),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val statusText = when {
            snapshot.percent < 0 -> context.getString(R.string.status_unknown)
            snapshot.isCharging -> context.getString(R.string.status_charging)
            snapshot.isFull -> context.getString(R.string.status_full)
            snapshot.isOnAcPower -> context.getString(R.string.status_not_charging)
            else -> context.getString(R.string.status_discharging)
        }

        val text = if (snapshot.percent >= 0) {
            context.getString(R.string.notification_service_text_fmt, snapshot.percent, statusText)
        } else {
            statusText
        }

        return NotificationCompat.Builder(context, NotificationChannels.SERVICE)
            .setSmallIcon(R.drawable.ic_battery_monitor)
            .setContentTitle(context.getString(R.string.notification_service_title))
            .setContentText(text)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setContentIntent(contentIntent)
            .addAction(R.drawable.ic_snooze, context.getString(R.string.notification_action_snooze), snoozeIntent)
            .addAction(R.drawable.ic_stop, context.getString(R.string.notification_action_stop), stopIntent)
            .build()
    }

    fun update(context: Context, snapshot: BatterySnapshot) {
        try {
            val manager = context.getSystemService(NotificationManager::class.java) ?: return
            manager.notify(NOTIF_ID_SERVICE, build(context, snapshot))
        } catch (_: Throwable) {
        }
    }
}
