package com.puneet.batteryguardian.core

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * Unit tests for battery health maths. Ported from BatteryHealthInfoTests.cs and
 * extended with boundary and label coverage.
 */
class BatteryHealthTest {

    @Test
    fun `health percent is full over design`() {
        val health = BatteryHealth(designCapacity = 5000, fullChargeCapacity = 4500)
        assertEquals(90, health.healthPercent)
        assertEquals(HealthLabel.GOOD, health.healthLabel)
    }

    @Test
    fun `health percent rounds to nearest integer`() {
        val health = BatteryHealth(designCapacity = 5000, fullChargeCapacity = 4350)
        assertEquals(87, health.healthPercent)
    }

    @Test
    fun `fair label at sixty to seventy nine`() {
        val health = BatteryHealth(designCapacity = 5000, fullChargeCapacity = 3000)
        assertEquals(60, health.healthPercent)
        assertEquals(HealthLabel.FAIR, health.healthLabel)
    }

    @Test
    fun `poor label below sixty`() {
        val health = BatteryHealth(designCapacity = 5000, fullChargeCapacity = 2000)
        assertEquals(40, health.healthPercent)
        assertEquals(HealthLabel.POOR, health.healthLabel)
    }

    @Test
    fun `health is clamped to one hundred`() {
        val health = BatteryHealth(designCapacity = 5000, fullChargeCapacity = 6000)
        assertEquals(100, health.healthPercent)
    }

    @Test
    fun `from returns null when design capacity is zero`() {
        assertNull(BatteryHealth.from(designCapacity = 0, fullChargeCapacity = 4500))
    }

    @Test
    fun `from returns null when full charge capacity is zero`() {
        assertNull(BatteryHealth.from(designCapacity = 5000, fullChargeCapacity = 0))
    }

    @Test
    fun `from returns null for negative values`() {
        assertNull(BatteryHealth.from(designCapacity = -1, fullChargeCapacity = 4500))
    }

    @Test
    fun `from returns a health when both values valid`() {
        val health = BatteryHealth.from(designCapacity = 5000, fullChargeCapacity = 4000)
        assertEquals(80, health?.healthPercent)
    }

    // ---- Boundary / label coverage ----

    @Test
    fun `label is GOOD exactly at eighty percent`() {
        val health = BatteryHealth(designCapacity = 100, fullChargeCapacity = 80)
        assertEquals(80, health.healthPercent)
        assertEquals(HealthLabel.GOOD, health.healthLabel)
    }

    @Test
    fun `label is FAIR at seventy nine percent`() {
        val health = BatteryHealth(designCapacity = 100, fullChargeCapacity = 79)
        assertEquals(79, health.healthPercent)
        assertEquals(HealthLabel.FAIR, health.healthLabel)
    }

    @Test
    fun `label is FAIR exactly at sixty percent`() {
        val health = BatteryHealth(designCapacity = 100, fullChargeCapacity = 60)
        assertEquals(60, health.healthPercent)
        assertEquals(HealthLabel.FAIR, health.healthLabel)
    }

    @Test
    fun `label is POOR at fifty nine percent`() {
        val health = BatteryHealth(designCapacity = 100, fullChargeCapacity = 59)
        assertEquals(59, health.healthPercent)
        assertEquals(HealthLabel.POOR, health.healthLabel)
    }

    @Test
    fun `label is POOR at one percent`() {
        val health = BatteryHealth(designCapacity = 1000, fullChargeCapacity = 10)
        assertEquals(1, health.healthPercent)
        assertEquals(HealthLabel.POOR, health.healthLabel)
    }

    @Test
    fun `one hundred percent is labeled GOOD`() {
        val health = BatteryHealth(designCapacity = 1000, fullChargeCapacity = 1000)
        assertEquals(100, health.healthPercent)
        assertEquals(HealthLabel.GOOD, health.healthLabel)
    }

    @Test
    fun `constructor with zero design reports unavailable percent`() {
        // Direct construction bypasses the `from` guard.
        val health = BatteryHealth(designCapacity = 0, fullChargeCapacity = 500)
        assertEquals(BatteryHealth.UNAVAILABLE, health.healthPercent)
        assertEquals(HealthLabel.UNAVAILABLE, health.healthLabel)
    }

    @Test
    fun `constructor with zero full reports unavailable percent`() {
        val health = BatteryHealth(designCapacity = 500, fullChargeCapacity = 0)
        assertEquals(BatteryHealth.UNAVAILABLE, health.healthPercent)
        assertEquals(HealthLabel.UNAVAILABLE, health.healthLabel)
    }

    @Test
    fun `from accepts equal design and full capacities`() {
        val health = BatteryHealth.from(designCapacity = 4000, fullChargeCapacity = 4000)
        assertEquals(100, health?.healthPercent)
    }

    @Test
    fun `from rejects negative full charge capacity`() {
        assertNull(BatteryHealth.from(designCapacity = 5000, fullChargeCapacity = -10))
    }

    @Test
    fun `unavailable sentinel is minus one`() {
        assertEquals(-1, BatteryHealth.UNAVAILABLE)
    }
}
