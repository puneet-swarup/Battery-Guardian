package com.puneet.batteryguardian.service

// Pure decision logic for the monitoring-watchdog. Given whether the
// user wants monitoring on and whether the service is currently running,
// it returns the action the activity should take. Kept free of Android
// framework types so it is unit-testable without a device.
enum class WatchdogAction {
    // Nothing to do.
    NONE,

    // User wants monitoring but the service is not running; start it.
    RESTART_SERVICE,

    // Service is running but the user turned monitoring off; stop it.
    STOP_SERVICE
}

object ServiceWatchdog {

    // Resolves the corrective action from the desired and actual states.
    fun resolve(monitoringDesired: Boolean, serviceRunning: Boolean): WatchdogAction =
        when {
            monitoringDesired && !serviceRunning -> WatchdogAction.RESTART_SERVICE
            !monitoringDesired && serviceRunning -> WatchdogAction.STOP_SERVICE
            else -> WatchdogAction.NONE
        }
}
