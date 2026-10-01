package com.puneet.batteryguardian.battery

/**
 * An immutable reading of the device's battery at a point in time.
 * Pure data object - no Android dependencies.
 */
data class BatterySnapshot(
    val percent: Int,
    val isOnAcPower: Boolean,
    val isCharging: Boolean,
    val isFull: Boolean,
    val status: BatteryStatus,
    val health: BatteryHealthReading?
) {
    companion object {
        /** Sentinel used when a reading could not be obtained. */
        val UNKNOWN = BatterySnapshot(
            percent = -1,
            isOnAcPower = false,
            isCharging = false,
            isFull = false,
            status = BatteryStatus.UNKNOWN,
            health = null
        )
    }
}

enum class BatteryStatus {
    CHARGING,
    DISCHARGING,
    FULL,
    NOT_CHARGING,
    UNKNOWN
}

/**
 * Best-effort battery health reading. Not all devices expose design capacity,
 * so both fields may be null even when the device reports a percentage.
 */
data class BatteryHealthReading(
    val designCapacity: Long?,
    val fullChargeCapacity: Long?
)
