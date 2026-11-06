package com.puneet.batteryguardian.service

import org.junit.Assert.assertEquals
import org.junit.Test

// Pure unit tests for ServiceActionPlan - the decision logic behind the
// service's onStartCommand. These cover the Snooze and Stop CTA behaviour
// (including the snooze-duration default) without constructing the Android
// Service, so they are fast and free of framework dependencies.
class ServiceActionPlanTest {

    @Test
    fun `stop action resolves to STOP`() {
        val plan = ServiceActionPlan.resolve(BatteryMonitorService.ACTION_STOP, 0)
        assertEquals(ServiceAction.STOP, plan.action)
    }

    @Test
    fun `snooze action resolves to SNOOZE`() {
        val plan = ServiceActionPlan.resolve(BatteryMonitorService.ACTION_SNOOZE, 45)
        assertEquals(ServiceAction.SNOOZE, plan.action)
    }

    @Test
    fun `snooze action keeps a positive duration`() {
        val plan = ServiceActionPlan.resolve(BatteryMonitorService.ACTION_SNOOZE, 45)
        assertEquals(45, plan.snoozeMinutes)
    }

    @Test
    fun `snooze action falls back to default for a non-positive duration`() {
        val plan = ServiceActionPlan.resolve(BatteryMonitorService.ACTION_SNOOZE, 0)
        assertEquals(ServiceActionPlan.DEFAULT_SNOOZE_MINUTES, plan.snoozeMinutes)
    }

    @Test
    fun `snooze action falls back to default for a negative duration`() {
        val plan = ServiceActionPlan.resolve(BatteryMonitorService.ACTION_SNOOZE, -10)
        assertEquals(ServiceActionPlan.DEFAULT_SNOOZE_MINUTES, plan.snoozeMinutes)
    }

    @Test
    fun `start action resolves to START`() {
        val plan = ServiceActionPlan.resolve(BatteryMonitorService.ACTION_START, 0)
        assertEquals(ServiceAction.START, plan.action)
    }

    @Test
    fun `null action resolves to START`() {
        val plan = ServiceActionPlan.resolve(null, 0)
        assertEquals(ServiceAction.START, plan.action)
    }

    @Test
    fun `unknown action resolves to START`() {
        val plan = ServiceActionPlan.resolve("com.example.UNKNOWN", 0)
        assertEquals(ServiceAction.START, plan.action)
    }
}
