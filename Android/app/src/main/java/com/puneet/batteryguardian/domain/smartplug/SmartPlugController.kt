package com.puneet.batteryguardian.domain.smartplug

import com.puneet.batteryguardian.data.tuya.RelayState
import com.puneet.batteryguardian.data.tuya.SmartPlug

class SmartPlugController(
 private val plug: SmartPlug,
 private val config: SmartPlugConfig
) {
 @Volatile
 var lastRelay: RelayState = RelayState.UNKNOWN
 private set

 @Volatile
 var lastError: String? = null
 private set

 @Volatile
 var lastSuccessEpochMs: Long = 0L
 private set

 private val lock = Any()

 fun evaluate(batteryPercent: Int, batteryKnown: Boolean): RelayState {
 if (!config.enabled) return lastRelay
 synchronized(lock) {
 return try {
 val relay = plug.getStatus()
 lastRelay = relay
 val decision = SmartPlugDecision.evaluate(batteryPercent, batteryKnown, relay, config)
 when (decision.action) {
 SmartPlugDecision.Action.TURN_OFF -> { plug.turnOff(); lastRelay = RelayState.OFF }
 SmartPlugDecision.Action.TURN_ON -> { plug.turnOn(); lastRelay = RelayState.ON }
 else -> {}
 }
 lastError = null
 lastSuccessEpochMs = System.currentTimeMillis()
 lastRelay
 } catch (t: Throwable) {
 lastError = t.message ?: t.javaClass.simpleName
 lastRelay
 }
 }
 }

 fun testConnection(): Boolean {
 synchronized(lock) {
 return try {
 val ok = plug.testConnection()
 if (ok) { lastError = null; lastSuccessEpochMs = System.currentTimeMillis() }
 ok
 } catch (t: Throwable) {
 lastError = t.message
 false
 }
 }
 }

 fun close() { try { plug.close() } catch (_: Throwable) {} }
}
