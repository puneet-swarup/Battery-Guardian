package com.puneet.batteryguardian.data.tuya

/** Relay state as reported by the physical device. */
enum class RelayState { UNKNOWN, ON, OFF }

/**
 * Logical interface the controller talks to. Keeps TCP, framing and crypto out
 * of the threshold logic, and lets tests drive a fake plug.
 *
 * Calls block: the only caller is `BatteryMonitorService`, which runs them on a
 * dedicated `smartplug-eval` thread.
 */
interface SmartPlug {
    fun getStatus(): RelayState
    fun turnOn()
    fun turnOff()
    fun testConnection(): Boolean
    fun close()
}
