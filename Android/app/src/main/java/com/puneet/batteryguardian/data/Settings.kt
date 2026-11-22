package com.puneet.batteryguardian.data

import java.time.Instant

/**
 * Persisted configuration, mirroring Settings.cs from the Windows application.
 * Immutable value object; mutations go through SettingsRepository.
 */
data class Settings(
    val highBatteryThreshold: Int = 95,
    val lowBatteryThreshold: Int = 15,
    val alertRepeatIntervalSeconds: Int = 300,
    val playSound: Boolean = true,
    val speakAlert: Boolean = true,
    val showNotification: Boolean = true,
    val vibrate: Boolean = true,
    val checkForUpdatesAutomatically: Boolean = true,
    val lastUpdateCheckUtc: Instant? = null,
    val diagnosticLoggingEnabled: Boolean = false,
    val snoozedUntilUtc: Instant? = null,
    val monitoringEnabled: Boolean = false,

    // ---- Battery history (feature 1) ----
    val historyEnabled: Boolean = true,
    val idealZoneLowPercent: Int = 20,
    val idealZoneHighPercent: Int = 80,

    // ---- Quiet hours / DND (feature 5) ----
    val quietHoursEnabled: Boolean = false,
    /** Quiet-hours start, minutes since local midnight. */
    val quietHoursStartMinutes: Int = 22 * 60,
    /** Quiet-hours end, minutes since local midnight. */
    val quietHoursEndMinutes: Int = 7 * 60,
    val respectDoNotDisturb: Boolean = true,

    // ---- Charge limit guidance (feature 20) ----
    val chargeLimitNudgeShown: Boolean = false,

 // ---- Smart plug (Tuya) ----
 val smartPlugEnabled: Boolean = false,
 // The plug follows the same High/Low levels as the alerts. It used to have its
 // own pair, which let the app alert at one level and switch at another.
 val smartPlugDeviceId: String = "",
 val smartPlugHost: String = "",
 val smartPlugPort: Int = 6668,
 val smartPlugRelayDp: Int = 1,
 val smartPlugProtocolVersion: String = "3.5"
) {
    /** True when alerts are currently suppressed by a snooze. */
    fun isSnoozed(now: Instant = Instant.now()): Boolean =
        snoozedUntilUtc != null && snoozedUntilUtc.isAfter(now)
}
