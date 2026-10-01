package com.puneet.batteryguardian

import android.app.Application
import com.puneet.batteryguardian.data.DiagnosticLog
import com.puneet.batteryguardian.data.SettingsRepository
import com.puneet.batteryguardian.notify.NotificationChannels

/**
 * Application entry point. Sets up notification channels and mirrors the
 * diagnostic-logging flag from settings, equivalent to App.xaml.cs startup work.
 */
class BatteryGuardianApp : Application() {

    override fun onCreate() {
        super.onCreate()

        // Catch uncaught exceptions in the diagnostic log when enabled.
        val previous = Thread.getDefaultUncaughtExceptionHandler()
        Thread.setDefaultUncaughtExceptionHandler { thread, throwable ->
            try {
                DiagnosticLog.get(this).writeException("Uncaught[$thread]", throwable)
            } catch (_: Throwable) {
            }
            previous?.uncaughtException(thread, throwable)
        }

        NotificationChannels.ensureCreated(this)

        val settings = SettingsRepository(this).load()
        DiagnosticLog.get(this).enabled = settings.diagnosticLoggingEnabled
    }
}
