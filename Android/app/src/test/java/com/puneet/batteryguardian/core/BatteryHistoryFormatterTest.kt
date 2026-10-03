package com.puneet.batteryguardian.core

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Duration

/** Tests for the pure history summary formatter. */
class BatteryHistoryFormatterTest {

    @Test
    fun `empty stats returns explanatory message`() {
        val text = BatteryHistoryFormatter.format(BatteryHistoryStats(), 20, 80)
        assertTrue(text.contains("No battery history"))
    }

    @Test
    fun `summary contains the key figures`() {
        val stats = BatteryHistoryStats(
            totalEntries = 120,
            daysCovered = 3,
            averagePercent = 62,
            minPercent = 15,
            maxPercent = 98,
            timeInIdealZone = Duration.ofMinutes(90),
            timeAboveHighZone = Duration.ofMinutes(30),
            timeBelowLowZone = Duration.ofMinutes(10),
            estimatedAnnualWearPercent = 1.5
        )
        val text = BatteryHistoryFormatter.format(stats, 20, 80)
        assertTrue(text.contains("Recorded readings: 120"))
        assertTrue(text.contains("Days covered: 3"))
        assertTrue(text.contains("Average charge: 62%"))
        assertTrue(text.contains("Range: 15% - 98%"))
        assertTrue(text.contains("Ideal zone (20-80%): 1h 30m"))
        assertTrue(text.contains("Above 80%: 30m"))
        assertTrue(text.contains("Below 20%: 10m"))
        assertTrue(text.contains("1.50% per year"))
    }

    @Test
    fun `formatDuration shows hours and minutes`() {
        assertEquals("2h 5m", BatteryHistoryFormatter.formatDuration(Duration.ofMinutes(125)))
    }

    @Test
    fun `formatDuration shows minutes under an hour`() {
        assertEquals("45m", BatteryHistoryFormatter.formatDuration(Duration.ofMinutes(45)))
    }

    @Test
    fun `formatDuration shows zero minutes`() {
        assertEquals("0m", BatteryHistoryFormatter.formatDuration(Duration.ZERO))
    }
}
