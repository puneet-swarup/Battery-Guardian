package com.puneet.batteryguardian.core

/**
 * Describes whether the current device exposes a system-level battery charge
 * limit (e.g. Samsung "Protect battery", Pixel "Charge limit to 80%", OnePlus
 * "Optimized charging").
 *
 * Android does not allow third-party apps to set these values, so this is a
 * *detection + guidance* feature: we detect the OEM setting and deep-link the
 * user to the correct settings screen, and we suppress our own high-charge nag
 * when the user already has a charge limit active.
 *
 * Pure data object with no Android dependencies, so the mapping logic is
 * unit-testable.
 */
data class ChargeLimitSupport(
    val isSupported: Boolean,
    val vendorName: String,
    val settingsAction: String?,
    val guidance: String
) {
    companion object {
        val UNSUPPORTED = ChargeLimitSupport(
            isSupported = false,
            vendorName = "",
            settingsAction = null,
            guidance = "This device does not expose a system battery charge limit."
        )
    }
}
