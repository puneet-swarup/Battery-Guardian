package com.puneet.batteryguardian.notify

import com.puneet.batteryguardian.data.Settings

/**
 * Abstraction over the alert delivery mechanism. Allows [AlertCoordinator] to be
 * unit tested with a fake implementation that records calls instead of touching
 * notifications, sound, TTS or vibration.
 */
interface AlertSink {
    fun notify(message: String, settings: Settings)
    fun release()
}
