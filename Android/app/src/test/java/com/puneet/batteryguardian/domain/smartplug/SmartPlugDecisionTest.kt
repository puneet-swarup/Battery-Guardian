package com.puneet.batteryguardian.domain.smartplug

import com.puneet.batteryguardian.data.tuya.RelayState
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test

class SmartPlugDecisionTest {
 private val cfg = SmartPlugConfig(enabled = true, highThreshold = 95, lowThreshold = 15)

 @Test
 fun thresholdsTooCloseTogetherAreRejected() {
  // A five-point gap is the floor: below it the relay clicks on every percent.
  // device id and host are required, so set them: this test is about the gap
  val narrow = cfg.copy(highThreshold = 92, lowThreshold = 90, deviceId = "id", host = "1.2.3.4")
  assertNotNull(narrow.validationError())

  val justEnough = cfg.copy(highThreshold = 95, lowThreshold = 90, deviceId = "id", host = "1.2.3.4")
  assertNull(justEnough.validationError())
 }

 /**
  * Every combination of battery position and relay state, asserted as an
  * outcome rather than as a mechanism.
  *
  * The bug this replaces was invisible for two releases: the only unknown-relay
  * test sat between the thresholds, where doing nothing is correct, and every
  * turn-on test supplied a readable state.
  */
 @Test
 fun decisionMatrix() {
  val rows = listOf(
   Triple(96, RelayState.ON, SmartPlugDecision.Action.TURN_OFF),
   Triple(96, RelayState.OFF, SmartPlugDecision.Action.NONE),
   Triple(96, RelayState.UNKNOWN, SmartPlugDecision.Action.TURN_OFF),
   Triple(50, RelayState.ON, SmartPlugDecision.Action.NONE),
   Triple(50, RelayState.OFF, SmartPlugDecision.Action.NONE),
   Triple(50, RelayState.UNKNOWN, SmartPlugDecision.Action.NONE),
   Triple(10, RelayState.ON, SmartPlugDecision.Action.NONE),
   Triple(10, RelayState.OFF, SmartPlugDecision.Action.TURN_ON),
   Triple(10, RelayState.UNKNOWN, SmartPlugDecision.Action.TURN_ON)
  )

  for ((percent, relay, expected) in rows) {
   assertEquals(
    "at $percent percent with the relay $relay",
    expected,
    SmartPlugDecision.evaluate(percent, true, relay, cfg).action
   )
  }
 }

 @Test
 fun disabledConfigProducesNoAction() {
 val d = SmartPlugDecision.evaluate(99, true, RelayState.ON, cfg.copy(enabled = false))
 assertEquals(SmartPlugDecision.Action.NONE, d.action)
 }

 @Test
 fun highThresholdTurnsRelayOffWhenOn() {
 val d = SmartPlugDecision.evaluate(95, true, RelayState.ON, cfg)
 assertEquals(SmartPlugDecision.Action.TURN_OFF, d.action)
 }

 @Test
 fun aboveHighThresholdTurnsOff() {
 val d = SmartPlugDecision.evaluate(100, true, RelayState.ON, cfg)
 assertEquals(SmartPlugDecision.Action.TURN_OFF, d.action)
 }

 @Test
 fun lowThresholdTurnsRelayOnWhenOff() {
 val d = SmartPlugDecision.evaluate(15, true, RelayState.OFF, cfg)
 assertEquals(SmartPlugDecision.Action.TURN_ON, d.action)
 }

 @Test
 fun belowLowThresholdTurnsOn() {
 val d = SmartPlugDecision.evaluate(5, true, RelayState.OFF, cfg)
 assertEquals(SmartPlugDecision.Action.TURN_ON, d.action)
 }

 @Test
 fun betweenThresholdsRetainsOn() {
 val d = SmartPlugDecision.evaluate(50, true, RelayState.ON, cfg)
 assertEquals(SmartPlugDecision.Action.NONE, d.action)
 }

 @Test
 fun betweenThresholdsRetainsOff() {
 val d = SmartPlugDecision.evaluate(50, true, RelayState.OFF, cfg)
 assertEquals(SmartPlugDecision.Action.NONE, d.action)
 }

 @Test
 fun unknownBatteryCausesNoAction() {
 val d = SmartPlugDecision.evaluate(-1, false, RelayState.ON, cfg)
 assertEquals(SmartPlugDecision.Action.NONE, d.action)
 }

 @Test
 fun highThresholdButRelayAlreadyOffDoesNothing() {
 val d = SmartPlugDecision.evaluate(96, true, RelayState.OFF, cfg)
 assertEquals(SmartPlugDecision.Action.NONE, d.action)
 }

 @Test
 fun lowThresholdButRelayAlreadyOnDoesNothing() {
 val d = SmartPlugDecision.evaluate(10, true, RelayState.ON, cfg)
 assertEquals(SmartPlugDecision.Action.NONE, d.action)
 }

 @Test
 fun anUnknownRelayStateStillTurnsThePlugOffAtTheHighThreshold() {
  val d = SmartPlugDecision.evaluate(96, true, RelayState.UNKNOWN, cfg)
  assertEquals(SmartPlugDecision.Action.TURN_OFF, d.action)
 }

 @Test
 fun anUnknownRelayStateStillTurnsThePlugOnAtTheLowThreshold() {
  val d = SmartPlugDecision.evaluate(10, true, RelayState.UNKNOWN, cfg)
  assertEquals(SmartPlugDecision.Action.TURN_ON, d.action)
 }

 @Test
 fun betweenThresholdsAnUnknownRelayStateDoesNothing() {
  val d = SmartPlugDecision.evaluate(50, true, RelayState.UNKNOWN, cfg)
  assertEquals(SmartPlugDecision.Action.NONE, d.action)
 }

 @Test
 fun invalidConfigDetected() {
 val bad = SmartPlugConfig(enabled = true, highThreshold = 10, lowThreshold = 90, deviceId = "id", host = "1.2.3.4")
 assertEquals(true, bad.validationError() != null)
 }
}
