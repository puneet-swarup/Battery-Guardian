package com.puneet.batteryguardian.domain.smartplug

/**
 * When a failed plug command should interrupt the user.
 *
 * A snooze means "stop telling me, keep protecting me" - so the reminders go
 * quiet while the plug carries on switching the charger. That promise only holds
 * if a failure is still reported: silently failing to switch is the one thing a
 * snooze must not hide. Kept pure so the rule is testable without Android.
 */
object SmartPlugFailurePolicy {

    /**
     * True when the last evaluation failed, the user is snoozed, and this
     * failure has not been reported yet. One notice per outage, not one per
     * reading.
     */
    fun shouldNotify(lastError: String?, snoozed: Boolean, alreadyNotified: Boolean): Boolean {
        if (lastError.isNullOrBlank()) return false
        return snoozed && !alreadyNotified
    }
}
