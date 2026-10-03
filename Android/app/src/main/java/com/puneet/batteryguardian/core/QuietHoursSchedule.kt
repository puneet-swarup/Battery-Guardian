package com.puneet.batteryguardian.core

import java.time.LocalTime

/**
 * A daily quiet-hours window in local time. Supports windows that span midnight
 * (e.g. 22:00 to 07:00). Immutable value object with no Android dependencies.
 *
 * Port of QuietHoursSchedule.cs from the Windows application.
 */
data class QuietHoursSchedule(
    val enabled: Boolean,
    val start: LocalTime,
    val end: LocalTime
) {
    /** True if the window spans midnight (start is later than end). */
    val spansMidnight: Boolean
        get() = start > end

    /**
     * Determines whether [time] falls inside the quiet window. A disabled
     * schedule is never quiet.
     */
    fun isQuietAt(time: LocalTime): Boolean {
        if (!enabled) return false

        return if (spansMidnight) {
            // e.g. 22:00 -> 07:00 : quiet if t >= 22:00 OR t < 07:00
            time >= start || time < end
        } else {
            // Same-day window, e.g. 13:00 -> 14:30 : quiet if start <= t < end
            time >= start && time < end
        }
    }

    companion object {
        val DISABLED = QuietHoursSchedule(
            enabled = false,
            start = LocalTime.MIDNIGHT,
            end = LocalTime.MIDNIGHT
        )
    }
}
