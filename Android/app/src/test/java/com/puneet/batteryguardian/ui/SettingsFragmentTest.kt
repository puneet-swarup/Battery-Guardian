package com.puneet.batteryguardian.ui

import android.content.Context
import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.preference.EditTextPreference
import androidx.preference.Preference
import androidx.preference.SwitchPreferenceCompat
import androidx.test.core.app.ApplicationProvider
import com.puneet.batteryguardian.R
import com.puneet.batteryguardian.data.SettingsRepository
import com.puneet.batteryguardian.ui.pref.SliderInputPreference
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

/**
 * Robolectric tests for [SettingsFragment]. Exercises the preference binding and
 * the cross-field validation rules (high must exceed low, repeat interval range).
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class SettingsFragmentTest {

    private lateinit var context: Context

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        context.getSharedPreferences("battery_guardian_settings", Context.MODE_PRIVATE)
            .edit().clear().commit()
    }

    private fun launchFragment(): SettingsFragment {
        val scenario = launchFragmentInContainer<SettingsFragment>(themeResId = R.style.Theme_BatteryGuardian)
        var fragment: SettingsFragment? = null
        scenario.onFragment { fragment = it }
        return fragment!!
    }

    @Test
    fun `preferences reflect stored settings`() {
        SettingsRepository(context).save(
            com.puneet.batteryguardian.data.Settings(
                highBatteryThreshold = 90,
                lowBatteryThreshold = 20,
                alertRepeatIntervalSeconds = 600
            )
        )
        val fragment = launchFragment()

        val high = fragment.findPreference<SliderInputPreference>("high_threshold")
        assertEquals(90, high?.getValue())
        val low = fragment.findPreference<SliderInputPreference>("low_threshold")
        assertEquals(20, low?.getValue())
        val repeat = fragment.findPreference<EditTextPreference>("repeat_interval")
        assertEquals("600", repeat?.summary)
    }

    @Test
    fun `switch preferences reflect stored booleans`() {
        SettingsRepository(context).update { it.copy(playSound = false, vibrate = false) }
        val fragment = launchFragment()

        val sound = fragment.findPreference<SwitchPreferenceCompat>("play_sound")
        assertFalse(sound?.isChecked ?: true)
        val vibrate = fragment.findPreference<SwitchPreferenceCompat>("vibrate")
        assertFalse(vibrate?.isChecked ?: true)
    }

    @Test
    fun `version preference is non selectable and populated`() {
        val fragment = launchFragment()
        val version = fragment.findPreference<Preference>("version")
        assertFalse(version?.isSelectable ?: true)
        assertTrue((version?.summary ?: "").isNotEmpty())
    }

    @Test
    fun `high threshold below low is rejected`() {
        val fragment = launchFragment()
        val high = fragment.findPreference<SliderInputPreference>("high_threshold")!!
        // Low defaults to 15, so 10 must be rejected.
        val accepted = high.callChangeListener(10)
        assertFalse(accepted)
    }

    @Test
    fun `high threshold above low is accepted and saved`() {
        val fragment = launchFragment()
        val high = fragment.findPreference<SliderInputPreference>("high_threshold")!!
        val accepted = high.callChangeListener(90)
        assertTrue(accepted)
        assertEquals(90, SettingsRepository(context).load().highBatteryThreshold)
    }

    @Test
    fun `low threshold above high is rejected`() {
        val fragment = launchFragment()
        val low = fragment.findPreference<SliderInputPreference>("low_threshold")!!
        // High defaults to 95, so 99 must be rejected.
        val accepted = low.callChangeListener(99)
        assertFalse(accepted)
    }

    @Test
    fun `low threshold below high is accepted and saved`() {
        val fragment = launchFragment()
        val low = fragment.findPreference<SliderInputPreference>("low_threshold")!!
        val accepted = low.callChangeListener(25)
        assertTrue(accepted)
        assertEquals(25, SettingsRepository(context).load().lowBatteryThreshold)
    }

    @Test
    fun `repeat interval accepts a valid value`() {
        val fragment = launchFragment()
        val repeat = fragment.findPreference<EditTextPreference>("repeat_interval")!!
        val accepted = repeat.callChangeListener("120")
        assertTrue(accepted)
        assertEquals(120, SettingsRepository(context).load().alertRepeatIntervalSeconds)
    }

    @Test
    fun `repeat interval rejects out of range values`() {
        val fragment = launchFragment()
        val repeat = fragment.findPreference<EditTextPreference>("repeat_interval")!!
        assertFalse(repeat.callChangeListener("10"))
        assertFalse(repeat.callChangeListener("99999"))
    }

    @Test
    fun `repeat interval rejects non numeric input`() {
        val fragment = launchFragment()
        val repeat = fragment.findPreference<EditTextPreference>("repeat_interval")!!
        assertFalse(repeat.callChangeListener("abc"))
    }
}
