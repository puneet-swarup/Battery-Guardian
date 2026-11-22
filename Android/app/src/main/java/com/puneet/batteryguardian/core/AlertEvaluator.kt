package com.puneet.batteryguardian.core

import com.puneet.batteryguardian.domain.signals.BatteryBand

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
     * Decides what alerts should be active for a reading.
     *
     * The comparison with the thresholds is not done here: it happens once, in
     * [com.puneet.batteryguardian.domain.signals.ThresholdMonitor], and arrives
     * as a band. This object only knows what an alert means - a high alert needs
     * the charger plugged in, a low alert needs it out.
     *
     * @param batteryPercent Current battery percentage, for the message.
     * @param band Where that percentage sits against the alert levels.
     * @param isOnAcPower True if the charger is physically plugged in.
     */
    fun evaluate(
        batteryPercent: Int,
        band: BatteryBand,
        isOnAcPower: Boolean
    ): AlertState {
        val highCondition = isOnAcPower && band == BatteryBand.HIGH
        val lowCondition = !isOnAcPower && band == BatteryBand.LOW

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
