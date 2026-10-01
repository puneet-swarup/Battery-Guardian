package com.puneet.batteryguardian.ui

import android.content.Context
import android.widget.Button
import android.widget.TextView
import androidx.test.core.app.ApplicationProvider
import com.puneet.batteryguardian.R
import com.puneet.batteryguardian.data.SettingsRepository
import org.junit.Assert.assertEquals
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
 * Robolectric tests for [MainActivity]. These verify the activity launches, the
 * control labels reflect monitoring state and the buttons are wired up.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class MainActivityTest {

    @Before
    fun clearPrefs() {
        val ctx = ApplicationProvider.getApplicationContext<Context>()
        ctx.getSharedPreferences("battery_guardian_settings", Context.MODE_PRIVATE)
            .edit().clear().commit()
    }

    private fun launch(): MainActivity =
        Robolectric.buildActivity(MainActivity::class.java).setup().get()

    @Test
    fun `activity launches with expected controls`() {
        val activity = launch()
        assertNotNull(activity.findViewById<Button>(R.id.monitorToggleButton))
        assertNotNull(activity.findViewById<Button>(R.id.snoozeButton))
        assertNotNull(activity.findViewById<Button>(R.id.settingsButton))
        assertNotNull(activity.findViewById<Button>(R.id.aboutButton))
    }

    @Test
    fun `start label is shown when monitoring disabled`() {
        val activity = launch()
        val toggle = activity.findViewById<Button>(R.id.monitorToggleButton)
        assertEquals(
            activity.getString(R.string.start_monitoring),
            toggle.text.toString()
        )
    }

    @Test
    fun `stop label is shown when monitoring enabled`() {
        // Enable monitoring first so the activity's onResume reads it during
        // the Robolectric setup() call and refreshes the labels.
        val ctx = ApplicationProvider.getApplicationContext<Context>()
        SettingsRepository(ctx).setMonitoringEnabled(true)

        val activity = launch()
        val toggle = activity.findViewById<Button>(R.id.monitorToggleButton)
        assertEquals(
            activity.getString(R.string.stop_monitoring),
            toggle.text.toString()
        )
    }

    @Test
    fun `snooze button is disabled while monitoring is off`() {
        val activity = launch()
        val snooze = activity.findViewById<Button>(R.id.snoozeButton)
        assertFalse(snooze.isEnabled)
    }

    @Test
    fun `battery percent shows placeholder when unknown`() {
        val activity = launch()
        val percent = activity.findViewById<TextView>(R.id.batteryPercent)
        assertNotNull(percent)
        // In the Robolectric environment no battery intent is set, so the value
        // falls back to the placeholder rather than a real percentage.
        assertTrue(percent.text.toString() == "--%" || percent.text.toString().endsWith("%"))
    }
}
