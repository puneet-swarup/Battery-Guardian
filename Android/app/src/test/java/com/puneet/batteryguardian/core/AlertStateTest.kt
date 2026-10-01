package com.puneet.batteryguardian.core

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Exhaustive unit tests for the pure [AlertState] data object, covering every
 * branch of [AlertState.anyActive] and [AlertState.activeMessage].
 */
class AlertStateTest {

    private fun state(high: Boolean, low: Boolean) = AlertState(
        highAlertShouldBeActive = high,
        lowAlertShouldBeActive = low,
        highAlertMessage = "HIGH",
        lowAlertMessage = "LOW"
    )

    @Test
    fun `anyActive is false when no alerts are active`() {
        assertFalse(state(high = false, low = false).anyActive)
    }

    @Test
    fun `anyActive is true when only high is active`() {
        assertTrue(state(high = true, low = false).anyActive)
    }

    @Test
    fun `anyActive is true when only low is active`() {
        assertTrue(state(high = true, low = true).anyActive)
    }

    @Test
    fun `activeMessage is empty when nothing is active`() {
        assertEquals("", state(high = false, low = false).activeMessage)
    }

    @Test
    fun `activeMessage returns high message when only high is active`() {
        assertEquals("HIGH", state(high = true, low = false).activeMessage)
    }

    @Test
    fun `activeMessage returns low message when only low is active`() {
        assertEquals("LOW", state(high = false, low = true).activeMessage)
    }

    @Test
    fun `activeMessage prefers high when both are active`() {
        assertEquals("HIGH", state(high = true, low = true).activeMessage)
    }

    @Test
    fun `data class equality and copy work as expected`() {
        val a = state(high = true, low = false)
        val b = a.copy()
        assertEquals(a, b)
        assertEquals(a.hashCode(), b.hashCode())
        assertFalse(a == a.copy(lowAlertShouldBeActive = true))
    }
}
