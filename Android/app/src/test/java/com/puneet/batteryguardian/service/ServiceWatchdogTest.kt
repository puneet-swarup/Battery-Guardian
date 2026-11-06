package com.puneet.batteryguardian.service

import org.junit.Assert.assertEquals
import org.junit.Test

/** Pure tests for the monitoring watchdog. */
class ServiceWatchdogTest {

    @Test
    fun `nothing to do when desired and running already agree`() {
        assertEquals(WatchdogAction.NONE, ServiceWatchdog.resolve(true, true))
    }

    @Test
    fun `nothing to do when desired and running are both off`() {
        assertEquals(WatchdogAction.NONE, ServiceWatchdog.resolve(false, false))
    }

    @Test
    fun `wants monitoring but service dead triggers restart`() {
        assertEquals(WatchdogAction.RESTART_SERVICE, ServiceWatchdog.resolve(true, false))
    }

    @Test
    fun `service running but user turned off triggers stop`() {
        assertEquals(WatchdogAction.STOP_SERVICE, ServiceWatchdog.resolve(false, true))
    }
}
