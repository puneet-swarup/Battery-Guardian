package com.puneet.batteryguardian.ui

import android.content.Context
import androidx.test.core.app.ApplicationProvider
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.Robolectric
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

/**
 * Robolectric tests for [SettingsActivity]: it hosts the settings fragment and
 * honours the up-navigation contract.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class SettingsActivityTest {

    @Before
    fun clearPrefs() {
        val ctx = ApplicationProvider.getApplicationContext<Context>()
        ctx.getSharedPreferences("battery_guardian_settings", Context.MODE_PRIVATE)
            .edit().clear().commit()
    }

    @Test
    fun `activity launches and hosts the settings fragment`() {
        val activity = Robolectric.buildActivity(SettingsActivity::class.java).setup().get()
        assertNotNull(activity.findViewById(com.puneet.batteryguardian.R.id.settingsContainer))
        val fragment = activity.supportFragmentManager.findFragmentById(
            com.puneet.batteryguardian.R.id.settingsContainer
        )
        assertTrue(fragment is SettingsFragment)
    }

    @Test
    fun `navigating up finishes the activity`() {
        val activity = Robolectric.buildActivity(SettingsActivity::class.java).setup().get()
        assertFalse(activity.isFinishing)
        assertTrue(activity.onSupportNavigateUp())
        assertTrue(activity.isFinishing)
    }
}
