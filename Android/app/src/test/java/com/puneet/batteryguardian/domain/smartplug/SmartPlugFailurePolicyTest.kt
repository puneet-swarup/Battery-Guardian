package com.puneet.batteryguardian.domain.smartplug

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A snooze means "stop telling me, keep protecting me". That promise is only
 * worth anything if a failed switch is still reported, once, without shouting.
 */
class SmartPlugFailurePolicyTest {

    @Test
    fun reportsAFailureThatHappenedDuringASnooze() {
        assertTrue(SmartPlugFailurePolicy.shouldNotify("plug did not answer", snoozed = true, alreadyNotified = false))
    }

    @Test
    fun reportsEachOutageOnlyOnce() {
        assertFalse(SmartPlugFailurePolicy.shouldNotify("plug did not answer", snoozed = true, alreadyNotified = true))
    }

    @Test
    fun staysQuietOutsideASnooze() {
        assertFalse(SmartPlugFailurePolicy.shouldNotify("plug did not answer", snoozed = false, alreadyNotified = false))
    }

    @Test
    fun staysQuietWhenTheSwitchWorked() {
        assertFalse(SmartPlugFailurePolicy.shouldNotify(null, snoozed = true, alreadyNotified = false))
    }
}
