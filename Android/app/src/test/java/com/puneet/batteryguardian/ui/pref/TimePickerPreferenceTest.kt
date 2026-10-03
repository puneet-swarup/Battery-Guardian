package com.puneet.batteryguardian.ui.pref

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * Tests for the pure time-formatting helper behind [TimePickerPreference].
 * The dialog itself is an Android component, but the value the user sees is
 * produced by this function, so it is worth covering directly.
 */
class TimePickerPreferenceTest {

    @Test
    fun `formats midnight`() {
        assertEquals("00:00", TimePickerPreference.formatTime(0))
    }

    @Test
    fun `formats 22 00 as default quiet start`() {
        assertEquals("22:00", TimePickerPreference.formatTime(22 * 60))
    }

    @Test
    fun `formats 07 00 as default quiet end`() {
        assertEquals("07:00", TimePickerPreference.formatTime(7 * 60))
    }

    @Test
    fun `pads single-digit hours and minutes`() {
        assertEquals("05:07", TimePickerPreference.formatTime(5 * 60 + 7))
    }

    @Test
    fun `formats last minute of the day`() {
        assertEquals("23:59", TimePickerPreference.formatTime(23 * 60 + 59))
    }

    @Test
    fun `wraps values beyond a day`() {
        assertEquals("01:00", TimePickerPreference.formatTime(25 * 60))
    }

    @Test
    fun `wraps negative values into the previous day`() {
        assertEquals("23:00", TimePickerPreference.formatTime(-60))
    }
}
