package com.puneet.batteryguardian.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import com.puneet.batteryguardian.data.DiagnosticLog
import com.puneet.batteryguardian.data.SettingsRepository

/**
 * Restarts battery monitoring after the device boots or after the app is
 * replaced by an update, if the user had monitoring enabled.
 */
class BootReceiver : BroadcastReceiver() {

    override fun onReceive(context: Context, intent: Intent) {
        val action = intent.action ?: return
        if (action != Intent.ACTION_BOOT_COMPLETED &&
            action != Intent.ACTION_MY_PACKAGE_REPLACED
        ) return

        try {
            val repo = SettingsRepository(context)
            if (repo.load().monitoringEnabled) {
                DiagnosticLog.get(context).write("BootReceiver: restarting monitoring")
                BatteryMonitorService.start(context)
            }
        } catch (t: Throwable) {
            DiagnosticLog.get(context).writeException("BootReceiver", t)
        }
    }
}
