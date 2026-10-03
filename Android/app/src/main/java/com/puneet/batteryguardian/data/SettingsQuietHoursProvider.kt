package com.puneet.batteryguardian.data

import com.puneet.batteryguardian.core.QuietHoursProvider
import com.puneet.batteryguardian.core.QuietHoursSchedule
import java.time.LocalTime

/**
 * Adapts [Settings] into a [QuietHoursSchedule]. Keeps the mapping in one place
 * so the evaluator never has to know about the persisted settings shape.
 *
 * Port of SettingsQuietHoursProvider.cs from the Windows application.
 */
class SettingsQuietHoursProvider(
    private val settingsAccessor: () -> Settings
) : QuietHoursProvider {

    override fun getSchedule(): QuietHoursSchedule {
        val settings = settingsAccessor()
        return QuietHoursSchedule(
            enabled = settings.quietHoursEnabled,
            start = LocalTime.ofSecondOfDay(settings.quietHoursStartMinutes * 60L),
            end = LocalTime.ofSecondOfDay(settings.quietHoursEndMinutes * 60L)
        )
    }
}
