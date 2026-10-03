package com.puneet.batteryguardian.data

import android.content.Context
import android.content.SharedPreferences
import androidx.core.content.edit
import java.time.Instant

/**
 * Reads and writes [Settings] to SharedPreferences. All access is synchronous
 * and cheap; callers may read on any thread. Writes use apply() so they are
 * asynchronous and never block the UI.
 */
class SettingsRepository(context: Context) {

    private val prefs: SharedPreferences =
        context.applicationContext.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)

    fun load(): Settings = Settings(
        highBatteryThreshold = prefs.getInt(KEY_HIGH, 95),
        lowBatteryThreshold = prefs.getInt(KEY_LOW, 15),
        alertRepeatIntervalSeconds = prefs.getInt(KEY_REPEAT, 300),
        playSound = prefs.getBoolean(KEY_SOUND, true),
        speakAlert = prefs.getBoolean(KEY_SPEAK, true),
        showNotification = prefs.getBoolean(KEY_NOTIFY, true),
        vibrate = prefs.getBoolean(KEY_VIBRATE, true),
        checkForUpdatesAutomatically = prefs.getBoolean(KEY_AUTO_UPDATE, true),
        lastUpdateCheckUtc = readInstant(KEY_LAST_UPDATE),
        diagnosticLoggingEnabled = prefs.getBoolean(KEY_DIAG, false),
        snoozedUntilUtc = readInstant(KEY_SNOOZE_UNTIL),
        monitoringEnabled = prefs.getBoolean(KEY_MONITORING, false),
        historyEnabled = prefs.getBoolean(KEY_HISTORY_ENABLED, true),
        idealZoneLowPercent = prefs.getInt(KEY_IDEAL_LOW, 20),
        idealZoneHighPercent = prefs.getInt(KEY_IDEAL_HIGH, 80),
        quietHoursEnabled = prefs.getBoolean(KEY_QUIET_ENABLED, false),
        quietHoursStartMinutes = prefs.getInt(KEY_QUIET_START, 22 * 60),
        quietHoursEndMinutes = prefs.getInt(KEY_QUIET_END, 7 * 60),
        respectDoNotDisturb = prefs.getBoolean(KEY_RESPECT_DND, true),
        chargeLimitNudgeShown = prefs.getBoolean(KEY_CHARGE_NUDGE, false)
    )

    fun save(settings: Settings) {
        prefs.edit {
            putInt(KEY_HIGH, settings.highBatteryThreshold)
            putInt(KEY_LOW, settings.lowBatteryThreshold)
            putInt(KEY_REPEAT, settings.alertRepeatIntervalSeconds)
            putBoolean(KEY_SOUND, settings.playSound)
            putBoolean(KEY_SPEAK, settings.speakAlert)
            putBoolean(KEY_NOTIFY, settings.showNotification)
            putBoolean(KEY_VIBRATE, settings.vibrate)
            putBoolean(KEY_AUTO_UPDATE, settings.checkForUpdatesAutomatically)
            putInstant(KEY_LAST_UPDATE, settings.lastUpdateCheckUtc)
            putBoolean(KEY_DIAG, settings.diagnosticLoggingEnabled)
            putInstant(KEY_SNOOZE_UNTIL, settings.snoozedUntilUtc)
            putBoolean(KEY_MONITORING, settings.monitoringEnabled)
            putBoolean(KEY_HISTORY_ENABLED, settings.historyEnabled)
            putInt(KEY_IDEAL_LOW, settings.idealZoneLowPercent)
            putInt(KEY_IDEAL_HIGH, settings.idealZoneHighPercent)
            putBoolean(KEY_QUIET_ENABLED, settings.quietHoursEnabled)
            putInt(KEY_QUIET_START, settings.quietHoursStartMinutes)
            putInt(KEY_QUIET_END, settings.quietHoursEndMinutes)
            putBoolean(KEY_RESPECT_DND, settings.respectDoNotDisturb)
            putBoolean(KEY_CHARGE_NUDGE, settings.chargeLimitNudgeShown)
        }
    }

    fun update(transform: (Settings) -> Settings) {
        save(transform(load()))
    }

    fun setMonitoringEnabled(enabled: Boolean) = update { it.copy(monitoringEnabled = enabled) }

    fun setSnoozedUntil(until: Instant?) = update { it.copy(snoozedUntilUtc = until) }

    fun setLastUpdateCheck(instant: Instant) = update { it.copy(lastUpdateCheckUtc = instant) }

    fun clearSnooze() = update { it.copy(snoozedUntilUtc = null) }

    private fun SharedPreferences.Editor.putInstant(key: String, value: Instant?) {
        if (value == null) remove(key) else putLong(key, value.toEpochMilli())
    }

    private fun readInstant(key: String): Instant? =
        if (prefs.contains(key)) Instant.ofEpochMilli(prefs.getLong(key, 0L)) else null

    companion object {
        private const val PREFS_NAME = "battery_guardian_settings"
        private const val KEY_HIGH = "high_threshold"
        private const val KEY_LOW = "low_threshold"
        private const val KEY_REPEAT = "repeat_interval"
        private const val KEY_SOUND = "play_sound"
        private const val KEY_SPEAK = "speak_alert"
        private const val KEY_NOTIFY = "show_notification"
        private const val KEY_VIBRATE = "vibrate"
        private const val KEY_AUTO_UPDATE = "auto_update"
        private const val KEY_LAST_UPDATE = "last_update_check"
        private const val KEY_DIAG = "diagnostic_logging"
        private const val KEY_SNOOZE_UNTIL = "snoozed_until"
        private const val KEY_MONITORING = "monitoring_enabled"
        private const val KEY_HISTORY_ENABLED = "history_enabled"
        private const val KEY_IDEAL_LOW = "ideal_zone_low"
        private const val KEY_IDEAL_HIGH = "ideal_zone_high"
        private const val KEY_QUIET_ENABLED = "quiet_hours_enabled"
        private const val KEY_QUIET_START = "quiet_hours_start"
        private const val KEY_QUIET_END = "quiet_hours_end"
        private const val KEY_RESPECT_DND = "respect_dnd"
        private const val KEY_CHARGE_NUDGE = "charge_nudge_shown"
    }
}
