package com.puneet.batteryguardian.domain.smartplug

import com.puneet.batteryguardian.data.tuya.RelayState
import com.puneet.batteryguardian.data.tuya.SmartPlug
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit

/**
 * The controller owns state and timing, not just the decision. These exist
 * because the decision tests passed while the app did nothing: a slow plug
 * held the lock and every reading queued behind it.
 */
class SmartPlugControllerTest {
 private val cfg = SmartPlugConfig(
  enabled = true,
  highThreshold = 95,
  lowThreshold = 15,
  deviceId = "id",
  host = "1.2.3.4"
 )

 @Test
 fun aSlowEvaluationIsSkippedNotQueued() {
  val plug = BlockingPlug()
  val controller = SmartPlugController(plug, cfg)
  val pool = Executors.newSingleThreadExecutor()

  try {
   val first = pool.submit { controller.evaluate(50, true) }
   assertTrue("the first evaluation never reached the plug", plug.entered.await(5, TimeUnit.SECONDS))

   val started = System.currentTimeMillis()
   controller.evaluate(50, true)
   val elapsed = System.currentTimeMillis() - started

   assertTrue("the second reading waited instead of being skipped: $elapsed ms", elapsed < 2000)

   plug.release.countDown()
   first.get(5, TimeUnit.SECONDS)
  } finally {
   pool.shutdownNow()
  }
 }

 private class BlockingPlug : SmartPlug {
  val entered = CountDownLatch(1)
  val release = CountDownLatch(1)

  override fun getStatus(): RelayState {
   entered.countDown()
   release.await(20, TimeUnit.SECONDS)
   return RelayState.ON
  }

  override fun turnOn() {}
  override fun turnOff() {}
  override fun testConnection() = true
  override fun close() {}
 }
}
