package com.puneet.batteryguardian.core

/**
 * Maps a device manufacturer (Build.MANUFACTURER) to the charge-limit support
 * it offers, including the best-known Settings action to deep-link the user to.
 *
 * Pure function of the manufacturer string, so it is fully unit-testable without
 * an emulator or a real device of each brand.
 *
 * Android does not permit third-party apps to change these firmware settings;
 * the app therefore guides the user to the OEM screen rather than setting the
 * value itself. This is the honest, platform-respecting implementation of the
 * "charge limit integration" feature.
 */
object ChargeLimitResolver {

    /** Samsung "Protect battery" toggle (caps charge at 85%). */
    private const val SAMSUNG_GUIDANCE =
        "Samsung devices offer \"Protect battery\" under Settings > Battery and device care > Battery. " +
            "Turn it on to cap charging at 85% and extend battery lifespan."

    /** Pixel "Charge limit" (80%). */
    private const val PIXEL_GUIDANCE =
        "Pixel devices offer a charge limit (80%) under Settings > Battery > Charging optimisation. " +
            "Enable it to reduce time spent at 100%."

    /** OnePlus / Oppo "Optimized charging" + charge limit. */
    private const val ONEPLUS_GUIDANCE =
        "OnePlus/Oppo devices offer charging optimisation under Settings > Battery. " +
            "Enable it to slow charging overnight and reduce time at full charge."

    private const val XIAOMI_GUIDANCE =
        "Xiaomi devices offer battery protection under Settings > Battery & performance. " +
            "Enable it to cap overnight charging."

    private const val GENERIC_GUIDANCE =
        "Check your device's Battery settings for a charge-limit or battery-protection option."

    /**
     * Resolves support for the supplied manufacturer. The comparison is
     * case-insensitive and tolerant of the many spellings OEMs use.
     */
    fun resolve(manufacturer: String?): ChargeLimitSupport {
        val m = manufacturer?.trim()?.lowercase().orEmpty()

        return when {
            m.contains("samsung") -> ChargeLimitSupport(
                isSupported = true,
                vendorName = "Samsung",
                settingsAction = "com.samsung.android.lool",
                guidance = SAMSUNG_GUIDANCE
            )

            m.contains("google") -> ChargeLimitSupport(
                isSupported = true,
                vendorName = "Google Pixel",
                settingsAction = "com.android.settings",
                guidance = PIXEL_GUIDANCE
            )

            m.contains("oneplus") || m.contains("oppo") || m.contains("realme") ->
                ChargeLimitSupport(
                    isSupported = true,
                    vendorName = "OnePlus/Oppo",
                    settingsAction = "com.android.settings",
                    guidance = ONEPLUS_GUIDANCE
                )

            m.contains("xiaomi") || m.contains("redmi") || m.contains("poco") ->
                ChargeLimitSupport(
                    isSupported = true,
                    vendorName = "Xiaomi",
                    settingsAction = "com.android.settings",
                    guidance = XIAOMI_GUIDANCE
                )

            else -> ChargeLimitSupport(
                isSupported = false,
                vendorName = manufacturer?.trim().orEmpty(),
                settingsAction = "com.android.settings",
                guidance = GENERIC_GUIDANCE
            )
        }
    }
}
