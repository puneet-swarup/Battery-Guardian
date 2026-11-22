package com.puneet.batteryguardian.domain.signals

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * One place decides what a reading means, so this is the one place the
 * boundaries are tested. The alerts and the smart plug both consume the result.
 *
 * Deliberately the same table as the Windows ThresholdMonitorTests: two apps
 * that promise the same behaviour should be pinned by the same cases.
 */
class ThresholdMonitorTest {

    private val pair = ThresholdPair(95, 15)

    private fun band(percent: Int, high: Int = 95, low: Int = 15) =
        ThresholdMonitor.evaluate(percent, true, ThresholdPair(high, low), BatteryBand.UNKNOWN).band

    // ---- the bands, including both boundaries --------------------------

    @Test
    fun classifiesEveryReading() {
        val rows = listOf(
            100 to BatteryBand.HIGH,
            96 to BatteryBand.HIGH,
            95 to BatteryBand.HIGH,      // exactly high is high
            94 to BatteryBand.IN_BAND,
            50 to BatteryBand.IN_BAND,
            16 to BatteryBand.IN_BAND,
            15 to BatteryBand.LOW,       // exactly low is low
            14 to BatteryBand.LOW,
            0 to BatteryBand.LOW
        )
        for ((percent, expected) in rows) {
            assertEquals("at $percent percent", expected, band(percent))
        }
    }

    @Test
    fun unusablePercentagesAreUnknown() {
        for (percent in listOf(-1, -50, 101, 999)) {
            assertEquals("at $percent percent", BatteryBand.UNKNOWN, band(percent))
        }
    }

    @Test
    fun aReadingTheCallerCannotUseIsUnknown() {
        val signal = ThresholdMonitor.evaluate(80, readingUsable = false, pair, BatteryBand.IN_BAND)
        assertEquals(BatteryBand.UNKNOWN, signal.band)
    }

    // ---- invalid pairs cannot produce a band ---------------------------

    @Test
    fun anInvalidPairYieldsUnknown() {
        val invalid = listOf(
            ThresholdPair(15, 95),   // inverted
            ThresholdPair(50, 50),   // identical
            ThresholdPair(95, 92),   // too close together
            ThresholdPair(-1, 15),   // out of range
            ThresholdPair(95, 101)
        )
        for (p in invalid) {
            val signal = ThresholdMonitor.evaluate(50, true, p, BatteryBand.IN_BAND)
            assertEquals("for $p", BatteryBand.UNKNOWN, signal.band)
        }
    }

    @Test
    fun aNullPairYieldsUnknown() {
        val signal = ThresholdMonitor.evaluate(50, true, null, BatteryBand.IN_BAND)
        assertEquals(BatteryBand.UNKNOWN, signal.band)
    }

    // ---- level versus edge ---------------------------------------------

    @Test
    fun stayingInABandIsNotAChange() {
        val signal = ThresholdMonitor.evaluate(97, true, pair, BatteryBand.HIGH)
        assertEquals(BatteryBand.HIGH, signal.band)
        assertFalse(signal.changed)
        assertFalse(signal.crossedHigh)
    }

    @Test
    fun crossingUpIsAChange() {
        val signal = ThresholdMonitor.evaluate(96, true, pair, BatteryBand.IN_BAND)
        assertTrue(signal.changed)
        assertTrue(signal.crossedHigh)
        assertFalse(signal.crossedLow)
    }

    @Test
    fun crossingDownIsAChange() {
        val signal = ThresholdMonitor.evaluate(14, true, pair, BatteryBand.IN_BAND)
        assertTrue(signal.changed)
        assertTrue(signal.crossedLow)
        assertFalse(signal.crossedHigh)
    }

    @Test
    fun returningToTheBandIsAChange() {
        val signal = ThresholdMonitor.evaluate(80, true, pair, BatteryBand.HIGH)
        assertTrue(signal.changed)
        assertTrue(signal.returnedToBand)
        assertFalse(signal.crossedHigh)
    }

    @Test
    fun goingFromUnknownToABandIsAChange() {
        val signal = ThresholdMonitor.evaluate(96, true, pair, BatteryBand.UNKNOWN)
        assertTrue(signal.changed)
        assertTrue(signal.crossedHigh)
    }

    @Test
    fun goingFromABandToUnknownIsAChange() {
        val signal = ThresholdMonitor.evaluate(-1, true, pair, BatteryBand.HIGH)
        assertEquals(BatteryBand.UNKNOWN, signal.band)
        assertTrue(signal.changed)
        assertFalse(signal.crossedHigh)
    }

    @Test
    fun remainingUnknownIsNotAChange() {
        val signal = ThresholdMonitor.evaluate(-1, true, pair, BatteryBand.UNKNOWN)
        assertEquals(BatteryBand.UNKNOWN, signal.band)
        assertFalse(signal.changed)
    }

    // ---- hysteresis: leaving a band needs the margin -------------------

    @Test
    fun aHighBandHoldsUntilTheBatteryFallsPastTheMargin() {
        val rows = listOf(
            95 to BatteryBand.HIGH,      // at the level
            94 to BatteryBand.HIGH,      // one below, still high
            92 to BatteryBand.HIGH,      // margin edge, still high
            91 to BatteryBand.IN_BAND    // past the margin
        )
        for ((percent, expected) in rows) {
            val signal = ThresholdMonitor.evaluate(percent, true, pair, BatteryBand.HIGH)
            assertEquals("at $percent percent", expected, signal.band)
        }
    }

    @Test
    fun aLowBandHoldsUntilTheBatteryRisesPastTheMargin() {
        val rows = listOf(
            15 to BatteryBand.LOW,
            16 to BatteryBand.LOW,
            18 to BatteryBand.LOW,
            19 to BatteryBand.IN_BAND
        )
        for ((percent, expected) in rows) {
            val signal = ThresholdMonitor.evaluate(percent, true, pair, BatteryBand.LOW)
            assertEquals("at $percent percent", expected, signal.band)
        }
    }

    @Test
    fun wobblingAcrossTheLevelDoesNotChangeTheBand() {
        // The case users reported: 95 fires, 94 must not re-arm, 96 must not
        // fire again. With the margin the band never leaves HIGH.
        var band = ThresholdMonitor.evaluate(95, true, pair, BatteryBand.UNKNOWN).band
        assertEquals(BatteryBand.HIGH, band)

        for (percent in listOf(94, 96, 95, 93)) {
            val signal = ThresholdMonitor.evaluate(percent, true, pair, band)
            assertEquals("at $percent percent", BatteryBand.HIGH, signal.band)
            assertFalse("a reading of $percent should not be a change", signal.changed)
            band = signal.band
        }
    }

    @Test
    fun anUnusableReadingDoesNotClearTheBand() {
        val signal = ThresholdMonitor.evaluate(-1, true, pair, BatteryBand.HIGH)
        assertEquals(BatteryBand.HIGH, signal.band)
        assertFalse(signal.changed)
    }

    @Test
    fun anInvalidPairStillClearsTheBand() {
        val signal = ThresholdMonitor.evaluate(50, true, ThresholdPair(50, 50), BatteryBand.HIGH)
        assertEquals(BatteryBand.UNKNOWN, signal.band)
    }

    @Test
    fun theMarginHasItsOwnRules() {
        assertTrue(ThresholdPair(95, 15, 3).validationError() == null)
        assertTrue(ThresholdPair(95, 15, 0).validationError() == null)
        assertTrue(ThresholdPair(95, 15, -1).validationError() != null)
        assertTrue(ThresholdPair(95, 15, 40).validationError() != null)
    }

    // ---- the pair's own rules ------------------------------------------

    @Test
    fun thePairValidatesItself() {
        assertNull(ThresholdPair(95, 15).validationError())
        assertNull(ThresholdPair(100, 0).validationError())
        assertNull(ThresholdPair(95, 90).validationError())
        assertTrue(ThresholdPair(95, 91).validationError() != null)   // four apart
        assertTrue(ThresholdPair(50, 50).validationError() != null)
        assertTrue(ThresholdPair(15, 95).validationError() != null)
        assertTrue(ThresholdPair(101, 15).validationError() != null)
        assertTrue(ThresholdPair(95, -1).validationError() != null)
    }
}
