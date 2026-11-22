package com.puneet.batteryguardian.domain.signals

/** Which side of a pair of levels a battery reading falls on. */
enum class BatteryBand { UNKNOWN, LOW, IN_BAND, HIGH }

/**
 * A pair of levels, with the rule that they must be usable together. Alerts and
 * the smart plug each hold their own pair.
 */
data class ThresholdPair(val high: Int, val low: Int) {

    /** Null when the pair can be used, otherwise what is wrong with it. */
    fun validationError(): String? {
        if (low < 0 || high > 100) return "Thresholds must be within 0-100."
        if (low >= high) return "Low threshold must be below high threshold."
        if (high - low < MINIMUM_GAP) return "Thresholds must be at least $MINIMUM_GAP points apart."
        return null
    }

    companion object {
        const val MINIMUM_GAP = 5
        val DEFAULT = ThresholdPair(95, 15)
    }
}

/**
 * What one reading means, and whether that meaning is new.
 *
 * Both halves are needed. [band] is a level - "at or above the high level right
 * now" - which is what a switch wants, because a switch must be correct even if
 * it missed the moment of crossing. [changed] is an edge - "this is the reading
 * that crossed" - which is what an alert wants, because an alert fires once and
 * not every thirty seconds.
 */
data class ThresholdSignal(
    val band: BatteryBand,
    val previous: BatteryBand,
    val changed: Boolean
) {
    val crossedHigh: Boolean get() = band == BatteryBand.HIGH && changed
    val crossedLow: Boolean get() = band == BatteryBand.LOW && changed
    val returnedToBand: Boolean get() = band == BatteryBand.IN_BAND && changed

    companion object {
        fun unknown(previous: BatteryBand) =
            ThresholdSignal(BatteryBand.UNKNOWN, previous, previous != BatteryBand.UNKNOWN)
    }
}

/**
 * Turns a battery reading into a [ThresholdSignal] for one pair of levels. Pure,
 * stateless, and the only place in the application that compares a battery
 * percentage with a threshold.
 */
object ThresholdMonitor {

    /**
     * Classifies one reading. [previous] is the band the caller saw last time,
     * which is what makes the change flag meaningful.
     */
    fun evaluate(
        batteryPercent: Int,
        readingUsable: Boolean,
        pair: ThresholdPair?,
        previous: BatteryBand
    ): ThresholdSignal {
        if (pair == null) return ThresholdSignal.unknown(previous)
        if (pair.validationError() != null) return ThresholdSignal.unknown(previous)
        if (!readingUsable || batteryPercent < 0 || batteryPercent > 100) {
            return ThresholdSignal.unknown(previous)
        }

        val band = when {
            batteryPercent >= pair.high -> BatteryBand.HIGH
            batteryPercent <= pair.low -> BatteryBand.LOW
            else -> BatteryBand.IN_BAND
        }

        return ThresholdSignal(band, previous, band != previous)
    }
}
