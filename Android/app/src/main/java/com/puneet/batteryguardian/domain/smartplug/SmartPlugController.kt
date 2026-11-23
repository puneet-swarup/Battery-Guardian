package com.puneet.batteryguardian.domain.smartplug

import com.puneet.batteryguardian.data.tuya.RelayState
import com.puneet.batteryguardian.data.tuya.SmartPlug
import com.puneet.batteryguardian.domain.signals.BatteryBand
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

 fun evaluate(band: BatteryBand): RelayState {
  if (!config.enabled) return lastRelay

  // Skip, never queue. A reading that waits behind a slow call is stale by the
  // time it runs, and the next tick brings a fresh one. Queueing here made the
  // plug look dead while an unreachable device held the lock.
  if (!lock.tryLock()) {
   lastError = "Previous check still running; this reading was skipped."
   return lastRelay
  }

  try {
   // Read the relay state, but never let a failed read stop the command. The
   // plug sits idle for hours before the low threshold arrives, which is exactly
   // when the read fails - and the commands are idempotent, so an unknown state
   // is no reason to sit still. This was the last reason the charger never came
   // back on: the decision had been taught to act on an unknown state, but the
   // read threw before it was ever asked.
   var readError: String? = null
   val relay = try {
    plug.getStatus()
   } catch (t: Throwable) {
    readError = t.message ?: t.javaClass.simpleName
    RelayState.UNKNOWN
   }
   lastRelay = relay

   val decision = SmartPlugDecision.evaluate(band, relay, config)
   when (decision.action) {
    SmartPlugDecision.Action.TURN_OFF -> { plug.turnOff(); lastRelay = RelayState.OFF }
    SmartPlugDecision.Action.TURN_ON -> { plug.turnOn(); lastRelay = RelayState.ON }
    else -> {}
   }

   // Nothing to do and the plug did not answer: report the failure rather than a
   // clean status, so an unreachable plug is never mistaken for a quiet one.
   if (readError != null && decision.action == SmartPlugDecision.Action.NONE) {
    lastError = readError
    return lastRelay
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

 /**
  * Sends one command and returns, for the shutdown path where there is no
  * reading to classify. Deliberately separate from [evaluate]: pretending the
  * battery is full to force a switch off would put a lie in the log.
  */
 fun applyNow(on: Boolean): RelayState {
  if (!config.enabled) return lastRelay
  if (!lock.tryLock()) {
   lastError = "Previous check still running; the shutdown command was skipped."
   return lastRelay
  }
  try {
   if (on) plug.turnOn() else plug.turnOff()
   lastRelay = if (on) RelayState.ON else RelayState.OFF
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
