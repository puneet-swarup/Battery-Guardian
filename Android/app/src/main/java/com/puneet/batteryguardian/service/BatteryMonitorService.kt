package com.puneet.batteryguardian.service

import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import com.puneet.batteryguardian.battery.BatteryReader
import com.puneet.batteryguardian.data.DiagnosticLog
import com.puneet.batteryguardian.data.SettingsRepository
import com.puneet.batteryguardian.notify.AlertNotifier
import com.puneet.batteryguardian.notify.NotificationChannels
import java.time.Instant

/**
 * Foreground service that keeps battery monitoring alive under Android's
 * background execution limits. It registers the battery receiver, evaluates
 * alerts on every change and mirrors the current level into the ongoing
 * notification.
 *
 * This is the Android replacement for the Windows app's always-running tray
 * process and DispatcherTimer.
 */
class BatteryMonitorService : Service() {

    private lateinit var settingsRepository: SettingsRepository
    private lateinit var notifier: AlertNotifier
    private lateinit var coordinator: AlertCoordinator
    private lateinit var batteryReader: BatteryReader
    private lateinit var log: DiagnosticLog

    private var receiver: BatteryStateReceiver? = null
    private var started = false

    override fun onCreate() {
        super.onCreate()
        settingsRepository = SettingsRepository(this)
        notifier = AlertNotifier(this)
        batteryReader = BatteryReader(this)
        coordinator = AlertCoordinator(this, settingsRepository, notifier)
        log = DiagnosticLog.get(this)
        NotificationChannels.ensureCreated(this)
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> {
                stopMonitoring()
                return START_NOT_STICKY
            }
            ACTION_SNOOZE -> {
                snooze(DEFAULT_SNOOZE_MINUTES)
                return START_STICKY
            }
        }

        startMonitoring()
        return START_STICKY
    }

    private fun startMonitoring() {
        if (!started) {
            try {
                // Must post the foreground notification promptly or the OS kills us.
                startForeground(OngoingNotification.NOTIF_ID_SERVICE, OngoingNotification.build(this, batteryReader.read()))
            } catch (t: Throwable) {
                log.writeException("startForeground", t)
                stopSelf()
                return
            }

            receiver = BatteryStateReceiver { snapshot ->
                val result = coordinator.onBatterySnapshot(snapshot)
                OngoingNotification.update(this, result.snapshot)
            }

            val filter = IntentFilter(Intent.ACTION_BATTERY_CHANGED)
            try {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                    registerReceiver(receiver, filter, Context.RECEIVER_NOT_EXPORTED)
                } else {
                    registerReceiver(receiver, filter)
                }
            } catch (t: Throwable) {
                log.writeException("registerReceiver", t)
            }

            settingsRepository.setMonitoringEnabled(true)
            started = true
            log.write("Monitoring started")
        }

        // Immediate first evaluation against the sticky battery intent.
        evaluateNow()
    }

    private fun evaluateNow() {
        try {
            val snapshot = batteryReader.read()
            val result = coordinator.onBatterySnapshot(snapshot)
            OngoingNotification.update(this, result.snapshot)
        } catch (t: Throwable) {
            log.writeException("evaluateNow", t)
        }
    }

    private fun snooze(minutes: Int) {
        val until = Instant.now().plusSeconds(minutes * 60L)
        settingsRepository.setSnoozedUntil(until)
        log.write("Snoozed until $until")
        evaluateNow()
    }

    private fun stopMonitoring() {
        try {
            receiver?.let {
                try {
                    unregisterReceiver(it)
                } catch (_: Throwable) {
                }
            }
        } finally {
            receiver = null
        }
        coordinator.reset()
        settingsRepository.setMonitoringEnabled(false)
        started = false
        log.write("Monitoring stopped")
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    override fun onDestroy() {
        try {
            receiver?.let {
                try {
                    unregisterReceiver(it)
                } catch (_: Throwable) {
                }
            }
        } catch (_: Throwable) {
        }
        notifier.release()
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    companion object {
        const val ACTION_START = "com.puneet.batteryguardian.action.START"
        const val ACTION_STOP = "com.puneet.batteryguardian.action.STOP"
        const val ACTION_SNOOZE = "com.puneet.batteryguardian.action.SNOOZE"
        private const val DEFAULT_SNOOZE_MINUTES = 30

        fun start(context: Context) {
            val intent = Intent(context, BatteryMonitorService::class.java).setAction(ACTION_START)
            ContextCompat.startForegroundService(context, intent)
        }

        fun stop(context: Context) {
            val intent = Intent(context, BatteryMonitorService::class.java).setAction(ACTION_STOP)
            try {
                context.startService(intent)
            } catch (_: Throwable) {
            }
        }

        fun snooze(context: Context, minutes: Int) {
            val intent = Intent(context, BatteryMonitorService::class.java).setAction(ACTION_SNOOZE)
            intent.putExtra("minutes", minutes)
            try {
                context.startService(intent)
            } catch (_: Throwable) {
            }
        }
    }
}
