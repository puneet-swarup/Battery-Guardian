package com.puneet.batteryguardian.update

import android.content.Context
import android.content.Intent
import androidx.test.core.app.ApplicationProvider
import com.puneet.batteryguardian.data.SettingsRepository
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

/**
 * Robolectric tests for [UpdateCheckReceiver]. The network call is not exercised
 * (that would require a live server); instead we verify the guard that skips the
 * check entirely when automatic updates are disabled.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class UpdateCheckReceiverTest {

    private lateinit var context: Context
    private lateinit var receiver: UpdateCheckReceiver

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        context.getSharedPreferences("battery_guardian_settings", Context.MODE_PRIVATE)
            .edit().clear().commit()
        receiver = UpdateCheckReceiver()
    }

    @Test
    fun `does nothing when automatic updates are disabled`() {
        // Default settings have auto-update enabled; disable it explicitly.
        SettingsRepository(context).update { it.copy(checkForUpdatesAutomatically = false) }

        receiver.onReceive(context, Intent(UpdateCheckReceiver.ACTION_CHECK_UPDATE))

        // With auto-update off the receiver returns immediately and never stamps
        // a last-check time.
        assertNull(SettingsRepository(context).load().lastUpdateCheckUtc)
    }

    @Test
    fun `action constant is stable`() {
        org.junit.Assert.assertEquals(
            "com.puneet.batteryguardian.action.CHECK_UPDATE",
            UpdateCheckReceiver.ACTION_CHECK_UPDATE
        )
    }
}
