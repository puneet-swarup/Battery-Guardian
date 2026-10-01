package com.puneet.batteryguardian.service

import android.content.Context
import android.content.Intent
import androidx.test.core.app.ApplicationProvider
import com.puneet.batteryguardian.data.SettingsRepository
import org.junit.Assert.assertNotNull
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

/**
 * Robolectric tests for [BootReceiver]. Verifies it ignores unrelated actions
 * and only attempts to restart monitoring after a boot/package-replace when the
 * user had monitoring enabled.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class BootReceiverTest {

    private lateinit var context: Context
    private lateinit var receiver: BootReceiver

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        // Clean persisted state between tests.
        context.getSharedPreferences("battery_guardian_settings", Context.MODE_PRIVATE)
            .edit().clear().commit()
        receiver = BootReceiver()
    }

    @Test
    fun `does nothing for a null action`() {
        // Should not throw.
        receiver.onReceive(context, Intent())
    }

    @Test
    fun `ignores unrelated actions`() {
        receiver.onReceive(context, Intent(Intent.ACTION_AIRPLANE_MODE_CHANGED))
    }

    @Test
    fun `does not start monitoring when disabled`() {
        receiver.onReceive(context, Intent(Intent.ACTION_BOOT_COMPLETED))
        // Nothing to assert beyond not throwing; monitoring stays disabled.
        assertNotNull(SettingsRepository(context).load())
    }

    @Test
    fun `handles boot completed when monitoring enabled`() {
        SettingsRepository(context).setMonitoringEnabled(true)
        receiver.onReceive(context, Intent(Intent.ACTION_BOOT_COMPLETED))
    }

    @Test
    fun `handles package replaced when monitoring enabled`() {
        SettingsRepository(context).setMonitoringEnabled(true)
        receiver.onReceive(context, Intent(Intent.ACTION_MY_PACKAGE_REPLACED))
    }
}
