package com.puneet.batteryguardian.battery

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Unit tests for the pure [BatterySnapshot] data object and its UNKNOWN sentinel,
 * plus the [BatteryStatus] and [BatteryHealthReading] value types.
 */
class BatterySnapshotTest {

    @Test
    fun `unknown sentinel has the documented defaults`() {
        val unknown = BatterySnapshot.UNKNOWN
        assertEquals(-1, unknown.percent)
        assertFalse(unknown.isOnAcPower)
        assertFalse(unknown.isCharging)
        assertFalse(unknown.isFull)
        assertEquals(BatteryStatus.UNKNOWN, unknown.status)
        assertNull(unknown.health)
    }

    @Test
    fun `snapshot retains all supplied fields`() {
        val health = BatteryHealthReading(designCapacity = 5000, fullChargeCapacity = 4000)
        val snapshot = BatterySnapshot(
            percent = 42,
            isOnAcPower = true,
            isCharging = true,
            isFull = false,
            status = BatteryStatus.CHARGING,
            health = health
        )
        assertEquals(42, snapshot.percent)
        assertTrue(snapshot.isOnAcPower)
        assertTrue(snapshot.isCharging)
        assertFalse(snapshot.isFull)
        assertEquals(BatteryStatus.CHARGING, snapshot.status)
        assertEquals(health, snapshot.health)
    }

    @Test
    fun `data class equality and copy behave correctly`() {
        val a = BatterySnapshot(50, false, false, false, BatteryStatus.DISCHARGING, null)
        val b = a.copy()
        assertEquals(a, b)
        assertEquals(a.hashCode(), b.hashCode())
        assertEquals(60, a.copy(percent = 60).percent)
    }

    @Test
    fun `all battery status values are available`() {
        // Guards against accidental removal/rename of enum constants.
        val values = BatteryStatus.values().toSet()
        assertTrue(values.containsAll(
            setOf(
                BatteryStatus.CHARGING,
                BatteryStatus.DISCHARGING,
                BatteryStatus.FULL,
                BatteryStatus.NOT_CHARGING,
                BatteryStatus.UNKNOWN
            )
        ))
        assertEquals(5, values.size)
    }

    @Test
    fun `health reading supports null capacities`() {
        val reading = BatteryHealthReading(designCapacity = null, fullChargeCapacity = 5000)
        assertNull(reading.designCapacity)
        assertEquals(5000L, reading.fullChargeCapacity)
    }

    @Test
    fun `health reading equality works for identical values`() {
        val a = BatteryHealthReading(5000, 4000)
        val b = BatteryHealthReading(5000, 4000)
        assertEquals(a, b)
        assertEquals(a.hashCode(), b.hashCode())
    }
}
