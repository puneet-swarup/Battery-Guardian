package com.puneet.batteryguardian.core

import java.time.Instant

/**
 * A single point-in-time battery reading recorded by the history store.
 * Immutable value object with no Android dependencies, so it is unit-testable.
 *
 * Kotlin port of BatteryHistoryEntry.cs from the Windows application.
 */
data class BatteryHistoryEntry(
    val utc: Instant,
    val percent: Int,
    val isOnAcPower: Boolean
)
