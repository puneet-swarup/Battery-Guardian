package com.puneet.batteryguardian.service

// Pure decision logic for how BatteryMonitorService reacts to an incoming
// onStartCommand intent. Kept free of Android framework types so it can be
// unit tested without constructing the Service (which would initialise
// TextToSpeech and other heavy components).
enum class ServiceAction {
    START,
    STOP,
    SNOOZE
}

data class ServiceActionPlan(
    val action: ServiceAction,
    val snoozeMinutes: Int
) {
    companion object {
        const val DEFAULT_SNOOZE_MINUTES = 30

        // Resolves the plan for the given intent action and raw snooze extra.
        // Unknown or null actions fall back to START, matching the historical
        // behaviour of the service.
        fun resolve(intentAction: String?, rawSnoozeMinutes: Int): ServiceActionPlan =
            when (intentAction) {
                BatteryMonitorService.ACTION_STOP ->
                    ServiceActionPlan(ServiceAction.STOP, DEFAULT_SNOOZE_MINUTES)

                BatteryMonitorService.ACTION_SNOOZE -> {
                    val minutes = if (rawSnoozeMinutes > 0) rawSnoozeMinutes else DEFAULT_SNOOZE_MINUTES
                    ServiceActionPlan(ServiceAction.SNOOZE, minutes)
                }

                else -> ServiceActionPlan(ServiceAction.START, DEFAULT_SNOOZE_MINUTES)
            }
    }
}
