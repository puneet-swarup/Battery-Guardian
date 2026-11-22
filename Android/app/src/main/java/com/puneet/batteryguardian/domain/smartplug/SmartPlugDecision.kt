package com.puneet.batteryguardian.domain.smartplug

import com.puneet.batteryguardian.data.tuya.RelayState
import com.puneet.batteryguardian.domain.signals.BatteryBand

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
 if (highThreshold - lowThreshold < MINIMUM_GAP_PERCENT)
  return "Thresholds must be at least $MINIMUM_GAP_PERCENT points apart."
 return null
 }

 companion object {
  /**
   * How far apart the two levels must be. A relay that switches on a
   * one-point change chatters as the battery moves, which wears the
   * contacts and cycles the charger.
   */
  const val MINIMUM_GAP_PERCENT = 5
 }
}

object SmartPlugDecision {
 enum class Action { NONE, TURN_ON, TURN_OFF }
 data class Decision(val action: Action, val reason: String)

 /**
  * Decides what to do with the relay, given where the reading sits.
  *
  * Level-based, not edge-based, on purpose: the plug must end up correct even if
  * the app missed the moment a level was crossed - while it was starting, or
  * asleep. An alert wants the edge; a switch wants the level.
  */
 fun evaluate(band: BatteryBand, relay: RelayState, config: SmartPlugConfig): Decision {
 if (!config.enabled) return Decision(Action.NONE, "Disabled.")
 if (band == BatteryBand.UNKNOWN) return Decision(Action.NONE, "Battery unknown.")
 // The relay state decides whether a command is needed, never whether one is
 // allowed. Requiring a successful read deadlocked the feature: when the plug
 // did not answer, the app did nothing, so turning the charger back on had to
 // be done by hand.
 if (band == BatteryBand.HIGH) {
  return if (relay == RelayState.OFF) Decision(Action.NONE, "Battery >= high; plug already OFF.")
  else Decision(Action.TURN_OFF, "Battery >= high; turn OFF.")
 }
 if (band == BatteryBand.LOW) {
  return if (relay == RelayState.ON) Decision(Action.NONE, "Battery <= low; plug already ON.")
  else Decision(Action.TURN_ON, "Battery <= low; turn ON.")
 }
 return Decision(Action.NONE, "Within thresholds.")
 }
}
