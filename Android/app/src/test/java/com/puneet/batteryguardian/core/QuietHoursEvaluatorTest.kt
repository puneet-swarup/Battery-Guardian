package com.puneet.batteryguardian.core

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.LocalTime

/**
 * Tests for the quiet-hours orchestration using injected fakes - no OS access,
 * fully deterministic.
 * Ported from QuietHoursEvaluatorTests.cs and extended.
 */
class QuietHoursEvaluatorTest {

    private class FakeScheduleProvider(var currentSchedule: QuietHoursSchedule) : QuietHoursProvider {
        var throwOnRead = false
        override fun getSchedule(): QuietHoursSchedule {
            if (throwOnRead) throw IllegalStateException("boom")
            return currentSchedule
        }
    }

    private class FakeDnd(var active: Boolean = false) : DoNotDisturbDetector {
        var throwOnRead = false
        override fun isDoNotDisturbActive(): Boolean {
            if (throwOnRead) throw IllegalStateException("boom")
            return active
        }
    }

    private fun evaluator(
        schedule: QuietHoursSchedule = QuietHoursSchedule.DISABLED,
        dnd: FakeDnd = FakeDnd(),
        time: LocalTime = LocalTime.of(12, 0)
    ): Triple<QuietHoursEvaluator, FakeScheduleProvider, FakeDnd> {
        val provider = FakeScheduleProvider(schedule)
        return Triple(QuietHoursEvaluator(provider, dnd) { time }, provider, dnd)
    }

    @Test
    fun `no quiet no dnd is not suppressed`() {
        val (eval, _, _) = evaluator()
        assertFalse(eval.isAudibleAlertSuppressed())
    }

    @Test
    fun `within quiet hours is suppressed`() {
        val (eval, _, _) = evaluator(
            schedule = QuietHoursSchedule(true, LocalTime.of(22, 0), LocalTime.of(7, 0)),
            time = LocalTime.of(23, 0)
        )
        assertTrue(eval.isAudibleAlertSuppressed())
    }

    @Test
    fun `dnd active is suppressed`() {
        val (eval, _, dnd) = evaluator(dnd = FakeDnd(active = true))
        assertTrue(eval.isAudibleAlertSuppressed())
    }

    @Test
    fun `dnd throwing fails safe to not suppressed`() {
        val dnd = FakeDnd().apply { throwOnRead = true }
        val (eval, _, _) = evaluator(dnd = dnd)
        assertFalse(eval.isAudibleAlertSuppressed())
    }

    @Test
    fun `isWithinQuietHours ignores dnd`() {
        val (eval, _, _) = evaluator(dnd = FakeDnd(active = true))
        assertFalse(eval.isWithinQuietHours())
    }

    @Test
    fun `outside window is not quiet`() {
        val (eval, _, _) = evaluator(
            schedule = QuietHoursSchedule(true, LocalTime.of(22, 0), LocalTime.of(7, 0)),
            time = LocalTime.of(12, 0)
        )
        assertFalse(eval.isWithinQuietHours())
    }

    @Test
    fun `schedule provider throwing fails safe to not quiet`() {
        val provider = FakeScheduleProvider(QuietHoursSchedule.DISABLED).apply { throwOnRead = true }
        val eval = QuietHoursEvaluator(provider, FakeDnd()) { LocalTime.of(12, 0) }
        assertFalse(eval.isWithinQuietHours())
    }
}
