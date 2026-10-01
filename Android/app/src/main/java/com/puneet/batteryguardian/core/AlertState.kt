package com.puneet.batteryguardian.core

/**
 * The desired state of alerts based on the current battery readings.
 * Pure data object - no Android dependencies, so it is trivially unit-testable.
 */
data class AlertState(
    val highAlertShouldBeActive: Boolean,
    val lowAlertShouldBeActive: Boolean,
    val highAlertMessage: String,
    val lowAlertMessage: String
) {
    /** True when any alert should currently be shown. */
    val anyActive: Boolean
        get() = highAlertShouldBeActive || lowAlertShouldBeActive

    /** The message for whichever alert is active (high takes precedence). */
    val activeMessage: String
        get() = when {
            highAlertShouldBeActive -> highAlertMessage
            lowAlertShouldBeActive -> lowAlertMessage
            else -> ""
        }
}
