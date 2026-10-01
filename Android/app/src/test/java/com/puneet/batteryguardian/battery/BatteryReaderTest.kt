package com.puneet.batteryguardian.battery

import android.content.Context
import android.content.Intent
import android.os.BatteryManager
import androidx.test.core.app.ApplicationProvider
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

/**
 * Robolectric tests for [BatteryReader.parseIntent]. Constructed
 * ACTION_BATTERY_CHANGED intents are fed to the parser so the percent maths and
 * status mapping can be verified on the JVM without a device.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class BatteryReaderTest {

    private lateinit var context: Context
    private lateinit var reader: BatteryReader

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        reader = BatteryReader(context)
    }

    private fun intent(
        level: Int = -1,
        scale: Int = -1,
        status: Int = -1,
        plugged: Int = 0
    ): Intent = Intent(Intent.ACTION_BATTERY_CHANGED).apply {
        putExtra(BatteryManager.EXTRA_LEVEL, level)
        putExtra(BatteryManager.EXTRA_SCALE, scale)
        putExtra(BatteryManager.EXTRA_STATUS, status)
        putExtra(BatteryManager.EXTRA_PLUGGED, plugged)
    }

    @Test
    fun `computes percent from level and scale`() {
        val snapshot = reader.parseIntent(
            intent(level = 50, scale = 100, status = BatteryManager.BATTERY_STATUS_DISCHARGING),
            health = null
        )
        assertEquals(50, snapshot.percent)
    }

    @Test
    fun `scales a non hundred scale value`() {
        val snapshot = reader.parseIntent(
            intent(level = 1, scale = 4, status = BatteryManager.BATTERY_STATUS_DISCHARGING),
            health = null
        )
        assertEquals(25, snapshot.percent)
    }

    @Test
    fun `missing level yields minus one percent`() {
        val snapshot = reader.parseIntent(
            intent(level = -1, scale = 100, status = BatteryManager.BATTERY_STATUS_DISCHARGING),
            health = null
        )
        assertEquals(-1, snapshot.percent)
    }

    @Test
    fun `non positive scale yields minus one percent`() {
        val snapshot = reader.parseIntent(
            intent(level = 50, scale = 0, status = BatteryManager.BATTERY_STATUS_DISCHARGING),
            health = null
        )
        assertEquals(-1, snapshot.percent)
    }

    @Test
    fun `maps charging status and plug state`() {
        val snapshot = reader.parseIntent(
            intent(60, 100, BatteryManager.BATTERY_STATUS_CHARGING, BatteryManager.BATTERY_PLUGGED_AC),
            health = null
        )
        assertEquals(BatteryStatus.CHARGING, snapshot.status)
        assertTrue(snapshot.isCharging)
        assertTrue(snapshot.isOnAcPower)
        assertFalse(snapshot.isFull)
    }

    @Test
    fun `maps discharging status`() {
        val snapshot = reader.parseIntent(
            intent(60, 100, BatteryManager.BATTERY_STATUS_DISCHARGING, 0),
            health = null
        )
        assertEquals(BatteryStatus.DISCHARGING, snapshot.status)
        assertFalse(snapshot.isOnAcPower)
        assertFalse(snapshot.isCharging)
    }

    @Test
    fun `maps full status`() {
        val snapshot = reader.parseIntent(
            intent(100, 100, BatteryManager.BATTERY_STATUS_FULL, BatteryManager.BATTERY_PLUGGED_AC),
            health = null
        )
        assertEquals(BatteryStatus.FULL, snapshot.status)
        assertTrue(snapshot.isFull)
    }

    @Test
    fun `maps not charging status`() {
        val snapshot = reader.parseIntent(
            intent(70, 100, BatteryManager.BATTERY_STATUS_NOT_CHARGING, 0),
            health = null
        )
        assertEquals(BatteryStatus.NOT_CHARGING, snapshot.status)
    }

    @Test
    fun `maps unknown status for an unrecognised value`() {
        val snapshot = reader.parseIntent(intent(70, 100, 9999, 0), health = null)
        assertEquals(BatteryStatus.UNKNOWN, snapshot.status)
    }

    @Test
    fun `usb plugged counts as on ac power`() {
        val snapshot = reader.parseIntent(
            intent(70, 100, BatteryManager.BATTERY_STATUS_CHARGING, BatteryManager.BATTERY_PLUGGED_USB),
            health = null
        )
        assertTrue(snapshot.isOnAcPower)
    }

    @Test
    fun `health reading is passed through`() {
        val health = BatteryHealthReading(designCapacity = 5000, fullChargeCapacity = 4500)
        val snapshot = reader.parseIntent(
            intent(70, 100, BatteryManager.BATTERY_STATUS_DISCHARGING, 0),
            health = health
        )
        assertEquals(health, snapshot.health)
    }

    @Test
    fun `null health is preserved when unavailable`() {
        val snapshot = reader.parseIntent(
            intent(70, 100, BatteryManager.BATTERY_STATUS_DISCHARGING, 0),
            health = null
        )
        assertNull(snapshot.health)
    }
}
