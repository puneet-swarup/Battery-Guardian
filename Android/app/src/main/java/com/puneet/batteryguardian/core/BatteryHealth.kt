package com.puneet.batteryguardian.core

import kotlin.math.roundToInt

/**
 * Represents the health of a battery, derived from its design capacity versus
 * its current full-charge capacity. Both values are in milliwatt-hours (mWh)
 * or microamp-hours (uAh) - the ratio is what matters, not the unit.
 *
 * Pure data object - no Android dependencies. Direct port of BatteryHealthInfo.cs.
 */
data class BatteryHealth(
    val designCapacity: Long,
    val fullChargeCapacity: Long
) {
    companion object {
        const val UNAVAILABLE = -1

        /**
         * Builds a BatteryHealth from raw values, returning null when the data is
         * missing or nonsensical (design capacity of zero, negative values, etc.).
         */
        fun from(designCapacity: Long, fullChargeCapacity: Long): BatteryHealth? {
            if (designCapacity <= 0 || fullChargeCapacity <= 0) return null
            return BatteryHealth(designCapacity, fullChargeCapacity)
        }
    }

    /**
     * Health percentage (0-100). Returns -1 if it cannot be calculated.
     */
    val healthPercent: Int
        get() {
            if (designCapacity <= 0 || fullChargeCapacity <= 0) return UNAVAILABLE
            val pct = (fullChargeCapacity.toDouble() / designCapacity.toDouble() * 100.0).roundToInt()
            return pct.coerceIn(0, 100)
        }

    /** A human-readable description of the health status. */
    val healthLabel: HealthLabel
        get() = when {
            healthPercent < 0 -> HealthLabel.UNAVAILABLE
            healthPercent >= 80 -> HealthLabel.GOOD
            healthPercent >= 60 -> HealthLabel.FAIR
            else -> HealthLabel.POOR
        }
}

enum class HealthLabel {
    GOOD, FAIR, POOR, UNAVAILABLE
}
