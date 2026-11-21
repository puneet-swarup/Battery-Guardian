package com.puneet.batteryguardian.update

import com.puneet.batteryguardian.data.Settings
import java.time.Duration
import java.time.Instant

/**
 * Decides whether an automatic update check is due.
 *
 * The check itself lives in [UpdateCheckReceiver]; this is only the policy, kept
 * pure so it can be tested without Android. It exists because the receiver was
 * declared in the manifest and nothing ever sent it the broadcast, so the
 * automatic check never ran on any install.
 */
object UpdateCheckPolicy {

    /** How often an automatic check is allowed. */
    const val INTERVAL_HOURS = 24L

    fun isDue(settings: Settings, now: Instant): Boolean {
        if (!settings.checkForUpdatesAutomatically) return false

        val last = settings.lastUpdateCheckUtc ?: return true
        if (last.isAfter(now)) return true // clock moved backwards; do not wait a day

        return Duration.between(last, now).toHours() >= INTERVAL_HOURS
    }
}
