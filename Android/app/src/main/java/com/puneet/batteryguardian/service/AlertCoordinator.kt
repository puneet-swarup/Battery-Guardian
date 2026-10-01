package com.puneet.batteryguardian.service

import android.content.Context
import com.puneet.batteryguardian.battery.BatterySnapshot
import com.puneet.batteryguardian.core.AlertEvaluator
import com.puneet.batteryguardian.data.DiagnosticLog
import com.puneet.batteryguardian.data.SettingsRepository
import com.puneet.batteryguardian.notify.AlertSink
import java.time.Instant

/**
 * Central alert state machine. It receives battery snapshots, decides whether an
 * alert should be active (via the pure [AlertEvaluator]) and applies repeat and
 * snooze rules before delegating to [AlertNotifier].
 *
 * This is the Android analogue of MainWindow.EvaluateAlerts + the alarm timer
 * in the Windows app. It is deliberately free of Android UI types so it can be
 * unit tested with a fake notifier and a fixed clock.
 */
class AlertCoordinator(
    private val context: Context,
    private val settingsRepository: SettingsRepository,
    private val notifier: AlertSink,
    private val clock: () -> Instant = { Instant.now() }
) {

    private val log = DiagnosticLog.get(context)

    private var highAlertActive = false
    private var lowAlertActive = false
    private var lastAlertAtMillis: Long = 0L
    private var currentMessage: String = ""

    /**
     * Evaluates a snapshot and, when appropriate, fires an alert. Returns the
     * resulting [EvaluationResult] so callers (e.g. the service) can update the
     * ongoing notification.
     */
    fun onBatterySnapshot(snapshot: BatterySnapshot): EvaluationResult {
        val settings = settingsRepository.load()

        val state = AlertEvaluator.evaluate(
            batteryPercent = snapshot.percent,
            isOnAcPower = snapshot.isOnAcPower,
            highThreshold = settings.highBatteryThreshold,
            lowThreshold = settings.lowBatteryThreshold
        )

        val wasHigh = highAlertActive
        val wasLow = lowAlertActive

        highAlertActive = state.highAlertShouldBeActive
        lowAlertActive = state.lowAlertShouldBeActive

        val message = state.activeMessage

        if (!highAlertActive && !lowAlertActive) {
            // No active alert: reset repeat bookkeeping.
            currentMessage = ""
            lastAlertAtMillis = 0L
            log.write("Evaluation: no alert active")
            return EvaluationResult(
                snapshot = snapshot,
                alertActive = false,
                message = "",
                alertFired = false
            )
        }

        // A new alert condition started (high or low transitioned on).
        val newCondition = (highAlertActive && !wasHigh) || (lowAlertActive && !wasLow)
        if (newCondition) {
            currentMessage = message
            lastAlertAtMillis = 0L
        } else if (message.isNotBlank()) {
            currentMessage = message
        }

        // Snooze check.
        if (settings.isSnoozed(clock())) {
            log.write("Evaluation: alert active but snoozed")
            return EvaluationResult(
                snapshot = snapshot,
                alertActive = true,
                message = currentMessage,
                alertFired = false,
                snoozed = true
            )
        }

        val nowMillis = clock().toEpochMilli()
        val repeatMillis = settings.alertRepeatIntervalSeconds * 1000L
        val dueForRepeat = lastAlertAtMillis == 0L || (nowMillis - lastAlertAtMillis) >= repeatMillis

        var fired = false
        if (dueForRepeat && currentMessage.isNotBlank()) {
            notifier.notify(currentMessage, settings)
            lastAlertAtMillis = nowMillis
            fired = true
            log.write("Evaluation: fired alert '$currentMessage'")
        }

        return EvaluationResult(
            snapshot = snapshot,
            alertActive = true,
            message = currentMessage,
            alertFired = fired,
            snoozed = false
        )
    }

    /** Clears active-alert bookkeeping (e.g. when monitoring stops). */
    fun reset() {
        highAlertActive = false
        lowAlertActive = false
        lastAlertAtMillis = 0L
        currentMessage = ""
    }

    fun isAlertActive(): Boolean = highAlertActive || lowAlertActive

    data class EvaluationResult(
        val snapshot: BatterySnapshot,
        val alertActive: Boolean,
        val message: String,
        val alertFired: Boolean,
        val snoozed: Boolean = false
    )
}
