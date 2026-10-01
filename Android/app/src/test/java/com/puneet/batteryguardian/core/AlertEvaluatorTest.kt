package com.puneet.batteryguardian.core

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Unit tests for the pure alert logic. Ported from BatteryAlertEvaluatorTests.cs
 * and extended with edge-case coverage.
 */
class AlertEvaluatorTest {

    @Test
    fun `high alert fires when plugged in at or above threshold`() {
        val state = AlertEvaluator.evaluate(96, isOnAcPower = true, highThreshold = 95, lowThreshold = 15)
        assertTrue(state.highAlertShouldBeActive)
        assertFalse(state.lowAlertShouldBeActive)
        assertTrue(state.highAlertMessage.contains("96%"))
    }

    @Test
    fun `high alert fires exactly at threshold`() {
        val state = AlertEvaluator.evaluate(95, isOnAcPower = true, highThreshold = 95, lowThreshold = 15)
        assertTrue(state.highAlertShouldBeActive)
    }

    @Test
    fun `high alert does not fire below threshold`() {
        val state = AlertEvaluator.evaluate(94, isOnAcPower = true, highThreshold = 95, lowThreshold = 15)
        assertFalse(state.highAlertShouldBeActive)
    }

    @Test
    fun `high alert does not fire when on battery`() {
        val state = AlertEvaluator.evaluate(99, isOnAcPower = false, highThreshold = 95, lowThreshold = 15)
        assertFalse(state.highAlertShouldBeActive)
    }

    @Test
    fun `low alert fires when on battery at or below threshold`() {
        val state = AlertEvaluator.evaluate(14, isOnAcPower = false, highThreshold = 95, lowThreshold = 15)
        assertFalse(state.highAlertShouldBeActive)
        assertTrue(state.lowAlertShouldBeActive)
        assertTrue(state.lowAlertMessage.contains("14%"))
    }

    @Test
    fun `low alert fires exactly at threshold`() {
        val state = AlertEvaluator.evaluate(15, isOnAcPower = false, highThreshold = 95, lowThreshold = 15)
        assertTrue(state.lowAlertShouldBeActive)
    }

    @Test
    fun `low alert does not fire above threshold`() {
        val state = AlertEvaluator.evaluate(16, isOnAcPower = false, highThreshold = 95, lowThreshold = 15)
        assertFalse(state.lowAlertShouldBeActive)
    }

    @Test
    fun `low alert does not fire when plugged in`() {
        val state = AlertEvaluator.evaluate(5, isOnAcPower = true, highThreshold = 95, lowThreshold = 15)
        assertFalse(state.lowAlertShouldBeActive)
    }

    @Test
    fun `no alert in the safe middle band`() {
        val state = AlertEvaluator.evaluate(50, isOnAcPower = false, highThreshold = 95, lowThreshold = 15)
        assertFalse(state.anyActive)
        assertEquals("", state.activeMessage)
    }

    @Test
    fun `active message prefers high alert when both somehow active`() {
        // Constructed edge case: high takes precedence in activeMessage.
        val state = AlertState(
            highAlertShouldBeActive = true,
            lowAlertShouldBeActive = true,
            highAlertMessage = "HIGH",
            lowAlertMessage = "LOW"
        )
        assertEquals("HIGH", state.activeMessage)
    }

    // ---- Additional edge-case coverage ----

    @Test
    fun `uses default thresholds when none supplied`() {
        // Default high = 95, low = 15.
        assertTrue(
            AlertEvaluator.evaluate(96, isOnAcPower = true).highAlertShouldBeActive
        )
        assertTrue(
            AlertEvaluator.evaluate(10, isOnAcPower = false).lowAlertShouldBeActive
        )
        assertFalse(
            AlertEvaluator.evaluate(50, isOnAcPower = false).anyActive
        )
    }

    @Test
    fun `default threshold constants match documented values`() {
        assertEquals(95, AlertEvaluator.DEFAULT_HIGH_THRESHOLD)
        assertEquals(15, AlertEvaluator.DEFAULT_LOW_THRESHOLD)
    }

    @Test
    fun `zero percent on battery fires low alert`() {
        val state = AlertEvaluator.evaluate(0, isOnAcPower = false)
        assertTrue(state.lowAlertShouldBeActive)
        assertTrue(state.lowAlertMessage.contains("0%"))
    }

    @Test
    fun `one hundred percent plugged in fires high alert`() {
        val state = AlertEvaluator.evaluate(100, isOnAcPower = true)
        assertTrue(state.highAlertShouldBeActive)
        assertTrue(state.highAlertMessage.contains("100%"))
    }

    @Test
    fun `equal high and low thresholds do not both fire at the same percent`() {
        // At exactly 50 with high == low == 50: plugged in -> high only.
        val plugged = AlertEvaluator.evaluate(50, isOnAcPower = true, highThreshold = 50, lowThreshold = 50)
        assertTrue(plugged.highAlertShouldBeActive)
        assertFalse(plugged.lowAlertShouldBeActive)

        // On battery -> low only.
        val unplugged = AlertEvaluator.evaluate(50, isOnAcPower = false, highThreshold = 50, lowThreshold = 50)
        assertTrue(unplugged.lowAlertShouldBeActive)
        assertFalse(unplugged.highAlertShouldBeActive)
    }

    @Test
    fun `inactive branches produce empty messages`() {
        val state = AlertEvaluator.evaluate(50, isOnAcPower = false)
        assertEquals("", state.highAlertMessage)
        assertEquals("", state.lowAlertMessage)
    }

    @Test
    fun `high alert message contains actionable guidance`() {
        val state = AlertEvaluator.evaluate(97, isOnAcPower = true)
        assertTrue(state.highAlertMessage.contains("unplugging", ignoreCase = true))
    }

    @Test
    fun `low alert message contains actionable guidance`() {
        val state = AlertEvaluator.evaluate(5, isOnAcPower = false)
        assertTrue(state.lowAlertMessage.contains("connect", ignoreCase = true))
    }

    @Test
    fun `thresholds are configurable independently`() {
        // A low threshold that is high up the range.
        val state = AlertEvaluator.evaluate(40, isOnAcPower = false, highThreshold = 90, lowThreshold = 45)
        assertTrue(state.lowAlertShouldBeActive)
    }
}
