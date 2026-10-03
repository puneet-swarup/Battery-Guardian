package com.puneet.batteryguardian.core

import java.util.Locale

/**
 * Builds the human-readable history summary shown in the app. Pure function of
 * the statistics and zone bounds, so it is fully unit-testable without Android.
 */
object BatteryHistoryFormatter {

    /**
     * Renders a multi-line summary. Returns an explanatory message when no data
     * has been recorded yet.
     */
    fun format(stats: BatteryHistoryStats, idealLow: Int, idealHigh: Int): String {
        if (stats.totalEntries == 0) {
            return "No battery history has been recorded yet. " +
                "Leave monitoring on and readings will accumulate over time."
        }

        return buildString {
            appendLine("Recorded readings: ${stats.totalEntries}")
            appendLine("Days covered: ${stats.daysCovered}")
            appendLine("Average charge: ${stats.averagePercent}%")
            appendLine("Range: ${stats.minPercent}% - ${stats.maxPercent}%")
            appendLine()
            appendLine("Ideal zone ($idealLow-$idealHigh%): ${formatDuration(stats.timeInIdealZone)}")
            appendLine("Above $idealHigh%: ${formatDuration(stats.timeAboveHighZone)}")
            appendLine("Below $idealLow%: ${formatDuration(stats.timeBelowLowZone)}")
            appendLine()
            append(
                "Estimated wear from current habits: ~" +
                    String.format(Locale.US, "%.2f", stats.estimatedAnnualWearPercent) +
                    "% per year"
            )
        }
    }

    /** Formats a Duration as "Xh Ym" (or "Ym" under an hour). */
    fun formatDuration(duration: java.time.Duration): String {
        val totalMinutes = duration.toMinutes()
        val hours = totalMinutes / 60
        val minutes = totalMinutes % 60
        return if (hours >= 1) "${hours}h ${minutes}m" else "${minutes}m"
    }
}
