package com.puneet.batteryguardian.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import com.puneet.batteryguardian.battery.BatteryReader

/**
 * Receives live battery changes. It can be registered dynamically by the
 * monitoring service (with a callback) and is also declared in the manifest so
 * the system can wake the app on battery events.
 *
 * The optional [callback] allows the foreground service to react to each change;
 * the manifest-registered instance (no callback) simply re-arms the service.
 */
class BatteryStateReceiver(
    private val callback: ((com.puneet.batteryguardian.battery.BatterySnapshot) -> Unit)? = null
) : BroadcastReceiver() {

    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != Intent.ACTION_BATTERY_CHANGED) return

        val snapshot = BatteryReader(context).read()

        if (callback != null) {
            callback.invoke(snapshot)
        } else {
            // Manifest-registered path: ensure the monitoring service is running.
            val repo = com.puneet.batteryguardian.data.SettingsRepository(context)
            if (repo.load().monitoringEnabled) {
                BatteryMonitorService.start(context)
            }
        }
    }
}
