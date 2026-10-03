package com.puneet.batteryguardian.core

import java.time.Duration

/**
 * Aggregated statistics derived from a set of battery history entries.
 * Pure data object produced by [BatteryHistoryAnalyzer].
 */
data class BatteryHistoryStats(
    val totalEntries: Int = 0,
    val daysCovered: Int = 0,
    val averagePercent: Int = 0,
    val minPercent: Int = 0,
    val maxPercent: Int = 0,
    val timeAboveHighZone: Duration = Duration.ZERO,
    val timeBelowLowZone: Duration = Duration.ZERO,
    val timeInIdealZone: Duration = Duration.ZERO,
    val estimatedAnnualWearPercent: Double = 0.0
)

/**
 * Pure analytics over battery history. No I/O, no Android APIs, no timers — fully
 * unit testable. Single responsibility: turn a sequence of readings into summary
 * statistics.
 *
 * Direct port of BatteryHistoryAnalyzer.cs from the Windows application.
 */
object BatteryHistoryAnalyzer {

    // Heuristic wear coefficients (% capacity lost per hour spent in each zone).
    private const val WEAR_PER_HOUR_ABOVE_HIGH = 0.0040
    private const val WEAR_PER_HOUR_BELOW_LOW = 0.0030

    /**
     * Analyse the given readings. Entries are sorted by timestamp internally.
     * The zone thresholds default to the ideal-window edges (e.g. 80/20).
     */
    fun analyze(
        entries: List<BatteryHistoryEntry>?,
        highZoneThreshold: Int = 80,
        lowZoneThreshold: Int = 20
    ): BatteryHistoryStats {
        val ordered = (entries ?: emptyList())
            .sortedBy { it.utc }

        if (ordered.isEmpty()) return BatteryHistoryStats()

        val min = ordered.minOf { it.percent }
        val max = ordered.maxOf { it.percent }
        val avg = Math.round(ordered.map { it.percent }.average()).toInt()

        var aboveMillis = 0L
        var belowMillis = 0L
        var idealMillis = 0L

        // Each entry (except the last) contributes the duration until the next
        // reading, classified by the percentage observed at that entry.
        for (i in 0 until ordered.size - 1) {
            val current = ordered[i]
            val next = ordered[i + 1]
            val spanMillis = Duration.between(current.utc, next.utc).toMillis().coerceAtLeast(0L)

            when {
                current.percent > highZoneThreshold -> aboveMillis += spanMillis
                current.percent < lowZoneThreshold -> belowMillis += spanMillis
                else -> idealMillis += spanMillis
            }
        }

        val totalSpanDays = Duration.between(ordered.first().utc, ordered.last().utc).toDays()
        val days = maxOf(1, Math.ceil(totalSpanDays.toDouble()).toInt())

        val aboveHours = aboveMillis / 3_600_000.0
        val belowHours = belowMillis / 3_600_000.0
        val wear = aboveHours * WEAR_PER_HOUR_ABOVE_HIGH + belowHours * WEAR_PER_HOUR_BELOW_LOW
        val annualWear = wear * (365.0 / days)

        return BatteryHistoryStats(
            totalEntries = ordered.size,
            daysCovered = days,
            averagePercent = avg,
            minPercent = min,
            maxPercent = max,
            timeAboveHighZone = Duration.ofMillis(aboveMillis),
            timeBelowLowZone = Duration.ofMillis(belowMillis),
            timeInIdealZone = Duration.ofMillis(idealMillis),
            estimatedAnnualWearPercent = Math.round(annualWear * 100.0) / 100.0
        )
    }
}
