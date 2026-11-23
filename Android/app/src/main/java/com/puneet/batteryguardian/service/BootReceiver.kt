package com.puneet.batteryguardian.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import com.puneet.batteryguardian.data.DiagnosticLog
import com.puneet.batteryguardian.data.SettingsRepository
import com.puneet.batteryguardian.domain.smartplug.SmartPlugManager
import kotlin.concurrent.thread

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
            val settings = repo.load()

            if (settings.monitoringEnabled) {
                DiagnosticLog.get(context).write("BootReceiver: restarting monitoring")
                BatteryMonitorService.start(context)
            }

            // Put the plug back the way it was before the phone was switched off.
            val remembered = settings.smartPlugStateBeforeShutdown
            if (remembered.isNotEmpty() && settings.smartPlugEnabled) {
                val log = DiagnosticLog.get(context)
                log.writeAlways("BOOT: restoring the plug to '$remembered'")

                thread(name = "plug-boot-restore") {
                    val manager = SmartPlugManager(context)
                    try {
                        val controller = manager.controllerFor(settings)
                        if (controller == null) {
                            log.writeAlways("BOOT: no controller, nothing restored")
                        } else {
                            val result = controller.applyNow(remembered == "On")
                            log.writeAlways("BOOT: restore attempted, plug now $result")
                        }
                        repo.setSmartPlugStateBeforeShutdown("")
                    } catch (t: Throwable) {
                        log.writeAlways("BOOT: restore failed - ${t.javaClass.simpleName}: ${t.message}")
                    } finally {
                        manager.close()
                    }
                }
            }
        } catch (t: Throwable) {
            DiagnosticLog.get(context).writeException("BootReceiver", t)
        }
    }
}
