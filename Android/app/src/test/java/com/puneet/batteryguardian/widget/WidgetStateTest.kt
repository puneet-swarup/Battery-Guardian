package com.puneet.batteryguardian.widget

import com.puneet.batteryguardian.battery.BatteryHealthReading
import com.puneet.batteryguardian.battery.BatterySnapshot
import com.puneet.batteryguardian.battery.BatteryStatus
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * Tests for the widget's pure presentation logic. No Android widget framework is
 * involved, so these run as plain JVM tests.
 */
class WidgetStateTest {

    private fun snapshot(
        percent: Int = 50,
        onAc: Boolean = false,
        charging: Boolean = false,
        full: Boolean = false,
        health: BatteryHealthReading? = null
    ) = BatterySnapshot(
        percent = percent,
        isOnAcPower = onAc,
        isCharging = charging,
        isFull = full,
        status = BatteryStatus.DISCHARGING,
        health = health
    )

    @Test
    fun `level text shows percent`() {
        val state = WidgetState.from(snapshot(percent = 73))
        assertEquals("73%", state.levelText)
    }

    @Test
    fun `unknown percent shows placeholder`() {
        val state = WidgetState.from(snapshot(percent = -1))
        assertEquals(WidgetState.LEVEL_UNKNOWN, state.levelText)
    }

    @Test
    fun `charging snapshot shows charging status`() {
        val state = WidgetState.from(snapshot(charging = true, onAc = true))
        assertEquals(WidgetState.STATUS_CHARGING, state.statusText)
    }

    @Test
    fun `full snapshot shows charging status`() {
        val state = WidgetState.from(snapshot(full = true, onAc = true))
        assertEquals(WidgetState.STATUS_CHARGING, state.statusText)
    }

    @Test
    fun `discharging snapshot shows on battery status`() {
        val state = WidgetState.from(snapshot(onAc = false))
        assertEquals(WidgetState.STATUS_ON_BATTERY, state.statusText)
    }

    @Test
    fun `health text is null when unavailable`() {
        val state = WidgetState.from(snapshot(health = null))
        assertNull(state.healthText)
    }

    @Test
    fun `health text is computed from capacities`() {
        val state = WidgetState.from(
            snapshot(health = BatteryHealthReading(designCapacity = 5000, fullChargeCapacity = 4000))
        )
        assertEquals("Health: 80%", state.healthText)
    }

    @Test
    fun `health text is null when capacities are missing`() {
        val state = WidgetState.from(
            snapshot(health = BatteryHealthReading(designCapacity = null, fullChargeCapacity = null))
        )
        assertNull(state.healthText)
    }
}
