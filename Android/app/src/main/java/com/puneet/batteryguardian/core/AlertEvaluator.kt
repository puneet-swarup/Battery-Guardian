package com.puneet.batteryguardian.core

/**
 * Pure logic for deciding whether a High or Low battery alert should currently
 * be active. Deliberately has no dependency on Android APIs, Context, timers or
 * notifications so that it can be unit tested on the JVM.
 *
 * Direct port of BatteryAlertEvaluator.cs from the Windows application.
 */
object AlertEvaluator {

    const val DEFAULT_HIGH_THRESHOLD = 95
    const val DEFAULT_LOW_THRESHOLD = 15

    /**
     * Evaluates the current battery state and returns what alerts should be active.
     *
     * @param batteryPercent Current battery percentage (0-100).
     * @param isOnAcPower True if the charger is physically plugged in.
     * @param highThreshold Configured high threshold (e.g. 95).
     * @param lowThreshold Configured low threshold (e.g. 15).
     */
    fun evaluate(
        batteryPercent: Int,
        isOnAcPower: Boolean,
        highThreshold: Int = DEFAULT_HIGH_THRESHOLD,
        lowThreshold: Int = DEFAULT_LOW_THRESHOLD
    ): AlertState {
        val highCondition = isOnAcPower && batteryPercent >= highThreshold
        val lowCondition = !isOnAcPower && batteryPercent <= lowThreshold

        return AlertState(
            highAlertShouldBeActive = highCondition,
            lowAlertShouldBeActive = lowCondition,
            highAlertMessage = if (highCondition) {
                "Battery is at $batteryPercent%. Consider unplugging the charger."
            } else "",
            lowAlertMessage = if (lowCondition) {
                "Battery is low at $batteryPercent%. Please connect the charger."
            } else ""
        )
    }
}
