package com.puneet.batteryguardian.domain.smartplug

import com.puneet.batteryguardian.data.tuya.RelayState
import com.puneet.batteryguardian.data.tuya.SmartPlug
import java.util.concurrent.locks.ReentrantLock

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

 private val lock = ReentrantLock()

 fun evaluate(batteryPercent: Int, batteryKnown: Boolean): RelayState {
  if (!config.enabled) return lastRelay

  // Skip, never queue. A reading that waits behind a slow call is stale by the
  // time it runs, and the next tick brings a fresh one. Queueing here made the
  // plug look dead while an unreachable device held the lock.
  if (!lock.tryLock()) {
   lastError = "Previous check still running; this reading was skipped."
   return lastRelay
  }

  try {
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
   return lastRelay
  } catch (t: Throwable) {
   lastError = t.message ?: t.javaClass.simpleName
   return lastRelay
  } finally {
   lock.unlock()
  }
 }

 fun testConnection(): Boolean {
  lock.lock()
  try {
   val ok = plug.testConnection()
   if (ok) { lastError = null; lastSuccessEpochMs = System.currentTimeMillis() }
   return ok
  } catch (t: Throwable) {
   lastError = t.message
   return false
  } finally {
   lock.unlock()
  }
 }

 fun close() { try { plug.close() } catch (_: Throwable) {} }
}
