package com.puneet.batteryguardian.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import com.puneet.batteryguardian.data.DiagnosticLog
import com.puneet.batteryguardian.data.SettingsRepository
import com.puneet.batteryguardian.domain.smartplug.SmartPlugManager
import com.puneet.batteryguardian.domain.tuya.RelayState
import kotlin.concurrent.thread

/**
 * Cuts the smart plug when the phone is switched off, and remembers what it was
 * doing so [BootReceiver] can put it back.
 *
 * The same reasoning as the Windows application: a phone that is off should not
 * sit on a live charger at 100% all night. Android gives a receiver a few seconds
 * on shutdown, so the command is handed to a background thread and this method
 * returns immediately - if the phone dies first, the plug stays on and the log
 * says so.
 */
class ShutdownReceiver : BroadcastReceiver() {

    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != Intent.ACTION_SHUTDOWN) return

        try {
            val repo = SettingsRepository(context)
            val settings = repo.load()
            val log = DiagnosticLog.get(context)

            if (!settings.smartPlugEnabled) {
                log.writeAlways("SHUTDOWN: plug control is off, nothing to cut")
                return
            }

            val manager = SmartPlugManager(context)
            val controller = manager.controllerFor(settings)
            if (controller == null) {
                log.writeAlways("SHUTDOWN: no controller, nothing to cut")
                return
            }

            // What we last knew: no network read here, the phone is going away.
            val known = controller.lastRelay
            repo.setSmartPlugStateBeforeShutdown(
                when (known) {
                    RelayState.ON -> "On"
                    RelayState.OFF -> "Off"
                    RelayState.UNKNOWN -> ""
                }
            )
            log.writeAlways("SHUTDOWN: remembered '$known', cutting the plug")

            thread(name = "plug-shutdown") {
                try {
                    val result = controller.applyNow(false)
                    log.writeAlways("SHUTDOWN: cut attempted, plug now $result")
                } catch (t: Throwable) {
                    log.writeAlways("SHUTDOWN: cut failed - ${t.javaClass.simpleName}: ${t.message}")
                } finally {
                    manager.close()
                }
            }
        } catch (t: Throwable) {
            DiagnosticLog.get(context).writeException("ShutdownReceiver", t)
        }
    }
}
