package com.puneet.batteryguardian.ui

import android.os.Bundle
import androidx.preference.EditTextPreference
import androidx.preference.Preference
import androidx.preference.PreferenceFragmentCompat
import androidx.preference.SwitchPreferenceCompat
import com.puneet.batteryguardian.BuildConfig
import com.puneet.batteryguardian.R
import com.puneet.batteryguardian.data.DiagnosticLog
import com.puneet.batteryguardian.data.Settings
import com.puneet.batteryguardian.data.SettingsRepository
import com.puneet.batteryguardian.ui.pref.SliderInputPreference

/**
 * Preference screen backed by [SettingsRepository]. Reads current values into
 * the UI and writes every change back to SharedPreferences.
 *
 * Keys match res/xml/settings_preferences.xml exactly.
 */
class SettingsFragment : PreferenceFragmentCompat() {

    private lateinit var repository: SettingsRepository

    override fun onCreatePreferences(savedInstanceState: Bundle?, rootKey: String?) {
        setPreferencesFromResource(R.xml.settings_preferences, rootKey)
        repository = SettingsRepository(requireContext())

        val current = repository.load()
        bindHighThreshold(current)
        bindLowThreshold(current)
        bindRepeatInterval(current)
        bindSwitch("play_sound", current.playSound) { v -> save { it.copy(playSound = v) } }
        bindSwitch("speak_alert", current.speakAlert) { v -> save { it.copy(speakAlert = v) } }
        bindSwitch("show_notification", current.showNotification) { v -> save { it.copy(showNotification = v) } }
        bindSwitch("vibrate", current.vibrate) { v -> save { it.copy(vibrate = v) } }
        bindSwitch("auto_update", current.checkForUpdatesAutomatically) { v ->
            save { it.copy(checkForUpdatesAutomatically = v) }
        }
        bindSwitch("diagnostic_logging", current.diagnosticLoggingEnabled) { v ->
            DiagnosticLog.get(requireContext()).enabled = v
            save { it.copy(diagnosticLoggingEnabled = v) }
        }
        bindVersion()
    }

    private fun bindHighThreshold(settings: Settings) {
        findPreference<SliderInputPreference>("high_threshold")?.apply {
            setValue(settings.highBatteryThreshold)
            // Reject a value at or below the current low threshold.
            setOnPreferenceChangeListener { _, newValue ->
                val v = (newValue as? Int) ?: return@setOnPreferenceChangeListener false
                if (v <= repository.load().lowBatteryThreshold) {
                    return@setOnPreferenceChangeListener false
                }
                save { it.copy(highBatteryThreshold = v) }
                true
            }
        }
    }

    private fun bindLowThreshold(settings: Settings) {
        findPreference<SliderInputPreference>("low_threshold")?.apply {
            setValue(settings.lowBatteryThreshold)
            setOnPreferenceChangeListener { _, newValue ->
                val v = (newValue as? Int) ?: return@setOnPreferenceChangeListener false
                if (v >= repository.load().highBatteryThreshold) {
                    return@setOnPreferenceChangeListener false
                }
                save { it.copy(lowBatteryThreshold = v) }
                true
            }
        }
    }

    private fun bindRepeatInterval(settings: Settings) {
        findPreference<EditTextPreference>("repeat_interval")?.apply {
            text = settings.alertRepeatIntervalSeconds.toString()
            summary = settings.alertRepeatIntervalSeconds.toString()
            setOnBindEditTextListener { editText ->
                // Ensure the typed value is clearly visible against the dark dialog.
                editText.setSelectAllOnFocus(true)
            }
            setOnPreferenceChangeListener { pref, newValue ->
                val v = (newValue as? String)?.trim()?.toIntOrNull()
                    ?: return@setOnPreferenceChangeListener false
                if (v < 30 || v > 7200) return@setOnPreferenceChangeListener false
                (pref as EditTextPreference).summary = v.toString()
                save { it.copy(alertRepeatIntervalSeconds = v) }
                true
            }
        }
    }

    private fun bindSwitch(key: String, current: Boolean, onChange: (Boolean) -> Unit) {
        findPreference<SwitchPreferenceCompat>(key)?.apply {
            isChecked = current
            setOnPreferenceChangeListener { _, newValue ->
                onChange(newValue as Boolean)
                true
            }
        }
    }

    private fun bindVersion() {
        findPreference<Preference>("version")?.apply {
            summary = getString(
                R.string.about_version_fmt,
                BuildConfig.VERSION_NAME,
                BuildConfig.VERSION_CODE
            )
            isSelectable = false
        }
    }

    private fun save(transform: (Settings) -> Settings) {
        try {
            repository.update(transform)
        } catch (t: Throwable) {
            DiagnosticLog.get(requireContext()).writeException("SettingsFragment.save", t)
        }
    }
}
