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
    val monitoringEnabled: Boolean = false
) {
    /** True when alerts are currently suppressed by a snooze. */
    fun isSnoozed(now: Instant = Instant.now()): Boolean =
        snoozedUntilUtc != null && snoozedUntilUtc.isAfter(now)
}
