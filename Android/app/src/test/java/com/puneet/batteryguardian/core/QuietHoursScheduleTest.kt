package com.puneet.batteryguardian.core

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.LocalTime

/**
 * Tests for the pure quiet-hours window logic.
 * Ported from QuietHoursScheduleTests.cs and extended.
 */
class QuietHoursScheduleTest {

    private fun t(hour: Int, minute: Int = 0) = LocalTime.of(hour, minute)

    @Test
    fun `disabled schedule is never quiet`() {
        val schedule = QuietHoursSchedule.DISABLED
        assertFalse(schedule.enabled)
        assertFalse(schedule.isQuietAt(t(23)))
        assertFalse(schedule.isQuietAt(t(3)))
        assertFalse(schedule.isQuietAt(t(12)))
    }

    @Test
    fun `spans midnight true when start after end`() {
        assertTrue(QuietHoursSchedule(true, t(22), t(7)).spansMidnight)
    }

    @Test
    fun `spans midnight false when start before end`() {
        assertFalse(QuietHoursSchedule(true, t(13), t(14)).spansMidnight)
    }

    @Test
    fun `midnight spanning window classifies correctly`() {
        val schedule = QuietHoursSchedule(true, t(22), t(7))
        assertTrue(schedule.isQuietAt(t(22, 0)))
        assertTrue(schedule.isQuietAt(t(23, 30)))
        assertTrue(schedule.isQuietAt(t(0, 0)))
        assertTrue(schedule.isQuietAt(t(3, 0)))
        assertTrue(schedule.isQuietAt(t(6, 59)))
        assertFalse(schedule.isQuietAt(t(7, 0)))   // end is exclusive
        assertFalse(schedule.isQuietAt(t(12, 0)))
        assertFalse(schedule.isQuietAt(t(21, 59))) // just before start
    }

    @Test
    fun `same day window classifies correctly`() {
        val schedule = QuietHoursSchedule(true, t(13), t(14, 30))
        assertTrue(schedule.isQuietAt(t(13, 0)))    // start inclusive
        assertTrue(schedule.isQuietAt(t(13, 30)))
        assertTrue(schedule.isQuietAt(t(14, 29)))
        assertFalse(schedule.isQuietAt(t(14, 30)))  // end exclusive
        assertFalse(schedule.isQuietAt(t(12, 59)))
        assertFalse(schedule.isQuietAt(t(15, 0)))
    }

    @Test
    fun `start equals end disabled is never quiet`() {
        val schedule = QuietHoursSchedule(false, t(10), t(10))
        assertFalse(schedule.isQuietAt(t(10)))
    }
}
