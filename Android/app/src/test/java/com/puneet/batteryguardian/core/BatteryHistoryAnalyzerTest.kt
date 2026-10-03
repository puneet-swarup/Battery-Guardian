package com.puneet.batteryguardian.core

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Duration
import java.time.Instant

/**
 * Tests for the pure battery-history analytics. No I/O, no Android APIs -
 * deterministic inputs produce deterministic statistics.
 *
 * Ported from BatteryHistoryAnalyzerTests.cs and extended.
 */
class BatteryHistoryAnalyzerTest {

    private val t0: Instant = Instant.parse("2026-01-01T12:00:00Z")

    private fun entry(minutesOffset: Long, percent: Int, ac: Boolean = false) =
        BatteryHistoryEntry(t0.plusSeconds(minutesOffset * 60), percent, ac)

    @Test
    fun `empty input returns zeroed stats`() {
        val stats = BatteryHistoryAnalyzer.analyze(emptyList())
        assertEquals(0, stats.totalEntries)
        assertEquals(0, stats.averagePercent)
        assertEquals(Duration.ZERO, stats.timeAboveHighZone)
    }

    @Test
    fun `null input does not throw`() {
        val stats = BatteryHistoryAnalyzer.analyze(null)
        assertEquals(0, stats.totalEntries)
    }

    @Test
    fun `computes min max and average`() {
        val stats = BatteryHistoryAnalyzer.analyze(
            listOf(entry(0, 40), entry(10, 60), entry(20, 80))
        )
        assertEquals(40, stats.minPercent)
        assertEquals(80, stats.maxPercent)
        assertEquals(60, stats.averagePercent)
    }

    @Test
    fun `entries out of order are sorted by timestamp`() {
        val stats = BatteryHistoryAnalyzer.analyze(
            listOf(entry(20, 30), entry(0, 90), entry(10, 60))
        )
        assertEquals(90, stats.maxPercent)
        assertEquals(30, stats.minPercent)
    }

    @Test
    fun `time above high zone is accumulated`() {
        val stats = BatteryHistoryAnalyzer.analyze(
            listOf(entry(0, 90), entry(30, 95)),
            highZoneThreshold = 80,
            lowZoneThreshold = 20
        )
        assertEquals(Duration.ofMinutes(30), stats.timeAboveHighZone)
        assertEquals(Duration.ZERO, stats.timeBelowLowZone)
    }

    @Test
    fun `time below low zone is accumulated`() {
        val stats = BatteryHistoryAnalyzer.analyze(
            listOf(entry(0, 10), entry(20, 5)),
            highZoneThreshold = 80,
            lowZoneThreshold = 20
        )
        assertEquals(Duration.ofMinutes(20), stats.timeBelowLowZone)
        assertEquals(Duration.ZERO, stats.timeAboveHighZone)
    }

    @Test
    fun `time in ideal zone is accumulated`() {
        val stats = BatteryHistoryAnalyzer.analyze(
            listOf(entry(0, 50), entry(45, 55)),
            highZoneThreshold = 80,
            lowZoneThreshold = 20
        )
        assertEquals(Duration.ofMinutes(45), stats.timeInIdealZone)
        assertEquals(Duration.ZERO, stats.timeAboveHighZone)
        assertEquals(Duration.ZERO, stats.timeBelowLowZone)
    }

    @Test
    fun `zone boundaries are inclusive of ideal`() {
        val stats = BatteryHistoryAnalyzer.analyze(
            listOf(entry(0, 80), entry(10, 20), entry(20, 50)),
            highZoneThreshold = 80,
            lowZoneThreshold = 20
        )
        assertEquals(Duration.ZERO, stats.timeAboveHighZone)
        assertEquals(Duration.ZERO, stats.timeBelowLowZone)
        assertEquals(Duration.ofMinutes(20), stats.timeInIdealZone)
    }

    @Test
    fun `non increasing timestamp contributes zero span`() {
        val stats = BatteryHistoryAnalyzer.analyze(
            listOf(entry(0, 90), entry(0, 90))
        )
        assertEquals(Duration.ZERO, stats.timeAboveHighZone)
    }

    @Test
    fun `days covered is at least one`() {
        val stats = BatteryHistoryAnalyzer.analyze(listOf(entry(0, 50)))
        assertTrue(stats.daysCovered >= 1)
    }

    @Test
    fun `annual wear is non negative`() {
        val stats = BatteryHistoryAnalyzer.analyze(
            listOf(entry(0, 95), entry(60, 95), entry(120, 95))
        )
        assertTrue(stats.estimatedAnnualWearPercent >= 0.0)
    }

    @Test
    fun `more time above high zone yields higher wear estimate`() {
        val shortAbove = BatteryHistoryAnalyzer.analyze(listOf(entry(0, 95), entry(30, 95)))
        val longAbove = BatteryHistoryAnalyzer.analyze(listOf(entry(0, 95), entry(600, 95)))
        assertTrue(longAbove.estimatedAnnualWearPercent > shortAbove.estimatedAnnualWearPercent)
    }

    @Test
    fun `custom zone thresholds are respected`() {
        val strict = BatteryHistoryAnalyzer.analyze(
            listOf(entry(0, 70), entry(10, 70)),
            highZoneThreshold = 60,
            lowZoneThreshold = 20
        )
        val relaxed = BatteryHistoryAnalyzer.analyze(
            listOf(entry(0, 70), entry(10, 70)),
            highZoneThreshold = 90,
            lowZoneThreshold = 20
        )
        assertEquals(Duration.ofMinutes(10), strict.timeAboveHighZone)
        assertEquals(Duration.ofMinutes(10), relaxed.timeInIdealZone)
    }
}
