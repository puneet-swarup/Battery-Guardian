package com.puneet.batteryguardian.core

import java.time.LocalTime

/**
 * Supplies the current quiet-hours configuration. Abstracted so tests can inject
 * a fixed schedule without touching SharedPreferences.
 */
fun interface QuietHoursProvider {
    fun getSchedule(): QuietHoursSchedule
}

/**
 * Detects whether the OS is currently in Do Not Disturb / silent mode.
 * Abstracted for testability; the Android implementation reads the notification
 * interruption filter.
 */
fun interface DoNotDisturbDetector {
    fun isDoNotDisturbActive(): Boolean
}

/**
 * Decides whether audible alerting should currently be suppressed.
 *
 * Suppression is active when EITHER the configured quiet-hours window contains
 * the current local time OR the OS is in Do Not Disturb.
 *
 * Pure orchestration over injected abstractions - no direct OS calls, fully
 * unit testable, single responsibility.
 *
 * Port of QuietHoursEvaluator.cs from the Windows application.
 */
class QuietHoursEvaluator(
    private val scheduleProvider: QuietHoursProvider,
    private val dndDetector: DoNotDisturbDetector,
    private val clock: () -> LocalTime = { LocalTime.now() }
) {

    /** True when audible alerts (voice + beep) should be suppressed. */
    fun isAudibleAlertSuppressed(): Boolean {
        if (isWithinQuietHours()) return true
        return safelyDetectDnd()
    }

    /** True only if the configured quiet-hours window is active now. */
    fun isWithinQuietHours(): Boolean {
        val schedule = try {
            scheduleProvider.getSchedule()
        } catch (t: Throwable) {
            QuietHoursSchedule.DISABLED
        }
        return schedule.isQuietAt(clock())
    }

    private fun safelyDetectDnd(): Boolean = try {
        dndDetector.isDoNotDisturbActive()
    } catch (t: Throwable) {
        // A failure to read DND state must never suppress or crash alerts.
        false
    }
}
