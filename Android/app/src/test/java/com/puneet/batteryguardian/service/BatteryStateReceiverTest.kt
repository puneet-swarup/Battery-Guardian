package com.puneet.batteryguardian.service

import android.content.Context
import android.content.Intent
import androidx.test.core.app.ApplicationProvider
import com.puneet.batteryguardian.battery.BatterySnapshot
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

/**
 * Robolectric tests for [BatteryStateReceiver]. Verifies the callback path used
 * by the foreground service and the ignore-behaviour for unrelated actions.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class BatteryStateReceiverTest {

    private lateinit var context: Context

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        context.getSharedPreferences("battery_guardian_settings", Context.MODE_PRIVATE)
            .edit().clear().commit()
    }

    @Test
    fun `ignores non battery actions`() {
        var invoked = false
        val receiver = BatteryStateReceiver { invoked = true }
        receiver.onReceive(context, Intent(Intent.ACTION_AIRPLANE_MODE_CHANGED))
        assertEquals(false, invoked)
    }

    @Test
    fun `invokes callback on battery changed`() {
        var captured: BatterySnapshot? = null
        val receiver = BatteryStateReceiver { captured = it }
        receiver.onReceive(context, Intent(Intent.ACTION_BATTERY_CHANGED))
        assertNotNull(captured)
    }

    @Test
    fun `without callback handles battery changed without throwing`() {
        val receiver = BatteryStateReceiver()
        // Monitoring is disabled by default, so no service is started; the call
        // must simply return without error.
        receiver.onReceive(context, Intent(Intent.ACTION_BATTERY_CHANGED))
    }

    @Test
    fun `without callback does nothing when monitoring disabled`() {
        val receiver = BatteryStateReceiver()
        receiver.onReceive(context, Intent(Intent.ACTION_BATTERY_CHANGED))
        assertEquals(false, com.puneet.batteryguardian.data.SettingsRepository(context).load().monitoringEnabled)
    }
}
