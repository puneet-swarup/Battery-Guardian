package com.puneet.batteryguardian.notify

import com.puneet.batteryguardian.data.Settings

/**
 * Abstraction over the alert delivery mechanism. Allows [AlertCoordinator] to be
 * unit tested with a fake implementation that records calls instead of touching
 * notifications, sound, TTS or vibration.
 */
interface AlertSink {
    /**
     * Delivers an alert. When [suppressAudible] is true, the notification is
     * still shown but the sound, speech and vibration channels are skipped
     * (quiet hours / Do Not Disturb).
     */
    fun notify(message: String, settings: Settings, suppressAudible: Boolean = false)

    fun release()
}
