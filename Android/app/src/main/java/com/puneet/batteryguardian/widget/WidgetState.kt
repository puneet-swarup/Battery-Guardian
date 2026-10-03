package com.puneet.batteryguardian.widget

import com.puneet.batteryguardian.battery.BatterySnapshot
import com.puneet.batteryguardian.core.BatteryHealth

/**
 * The display-ready strings for the home-screen widget, derived purely from a
 * [BatterySnapshot]. Keeping this mapping separate from the AppWidgetProvider
 * means the widget's presentation logic is unit-testable without the Android
 * widget framework.
 */
data class WidgetState(
    val levelText: String,
    val statusText: String,
    val healthText: String?
) {
    companion object {
        const val LEVEL_UNKNOWN = "--"
        const val STATUS_CHARGING = "Charging"
        const val STATUS_ON_BATTERY = "On battery"

        /**
         * Builds the widget state from a snapshot. Unknown readings render as a
         * neutral placeholder rather than a misleading number.
         */
        fun from(snapshot: BatterySnapshot): WidgetState {
            val level = if (snapshot.percent in 0..100) "${snapshot.percent}%" else LEVEL_UNKNOWN

            val status = when {
                snapshot.isCharging || snapshot.isFull -> STATUS_CHARGING
                else -> STATUS_ON_BATTERY
            }

            val healthText = snapshot.health?.let { reading ->
                val health = BatteryHealth.from(
                    reading.designCapacity ?: 0L,
                    reading.fullChargeCapacity ?: 0L
                )
                health?.let { "Health: ${it.healthPercent}%" }
            }

            return WidgetState(levelText = level, statusText = status, healthText = healthText)
        }
    }
}
