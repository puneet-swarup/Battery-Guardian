package com.puneet.batteryguardian.domain.smartplug

import com.puneet.batteryguardian.data.tuya.RelayState

data class SmartPlugConfig(
 val enabled: Boolean = false,
 val highThreshold: Int = 95,
 val lowThreshold: Int = 15,
 val deviceId: String = "",
 val host: String = "",
 val port: Int = 6668,
 val relayDp: Int = 1,
 val protocolVersion: String = "3.5"
) {
 fun validationError(): String? {
 if (deviceId.isBlank()) return "Device ID is required."
 if (host.isBlank()) return "Device IP address is required."
 if (port !in 1..65535) return "Port must be 1-65535."
 if (lowThreshold < 0 || highThreshold > 100) return "Thresholds must be 0-100."
 if (lowThreshold >= highThreshold) return "Low threshold must be below high threshold."
 return null
 }
}

object SmartPlugDecision {
 enum class Action { NONE, TURN_ON, TURN_OFF, QUERY }
 data class Decision(val action: Action, val reason: String)

 fun evaluate(batteryPercent: Int, batteryKnown: Boolean, relay: RelayState, config: SmartPlugConfig): Decision {
 if (!config.enabled) return Decision(Action.NONE, "Disabled.")
 if (!batteryKnown || batteryPercent < 0 || batteryPercent > 100) return Decision(Action.NONE, "Battery unknown.")
 if (relay == RelayState.UNKNOWN) return Decision(Action.QUERY, "Relay unknown; query first.")
 if (batteryPercent >= config.highThreshold && relay == RelayState.ON) return Decision(Action.TURN_OFF, "Battery >= high; turn OFF.")
 if (batteryPercent <= config.lowThreshold && relay == RelayState.OFF) return Decision(Action.TURN_ON, "Battery <= low; turn ON.")
 return Decision(Action.NONE, "Within thresholds.")
 }
}
