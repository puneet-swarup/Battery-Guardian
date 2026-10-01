package com.puneet.batteryguardian.notify

import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import android.os.Build

/**
 * Creates the notification channels required since Android 8 (API 26).
 * Safe to call repeatedly - creating an existing channel is a no-op.
 */
object NotificationChannels {

    const val ALERTS = "bg_alerts"
    const val SERVICE = "bg_service"

    fun ensureCreated(context: Context) {
        val manager = context.getSystemService(NotificationManager::class.java) ?: return

        val alerts = NotificationChannel(
            ALERTS,
            "Battery alerts",
            NotificationManager.IMPORTANCE_HIGH
        ).apply {
            description = "High and low battery warnings"
            enableVibration(true)
        }

        val service = NotificationChannel(
            SERVICE,
            "Monitoring service",
            NotificationManager.IMPORTANCE_LOW
        ).apply {
            description = "Keeps battery monitoring running in the background"
            setShowBadge(false)
        }

        manager.createNotificationChannel(alerts)
        manager.createNotificationChannel(service)
    }
}
