using System;

namespace BatteryGuardian.Signals
{
    /// <summary>
    /// Which side of a pair of levels a battery reading falls on.
    /// </summary>
    public enum BatteryBand
    {
        /// <summary>No usable reading, or the levels themselves are invalid.</summary>
        Unknown = 0,
        Low = 1,
        InBand = 2,
        High = 3,
    }

    /// <summary>
    /// A pair of levels, with the rule that they must be usable together.
    /// Alerts and the smart plug each hold their own pair.
    /// </summary>
    public sealed record ThresholdPair(int High, int Low)
    {
        public const int MinimumGap = 5;

        public static ThresholdPair Default => new(95, 15);

        /// <summary>Null when the pair can be used, otherwise what is wrong with it.</summary>
        public string? ValidationError()
        {
            if (Low < 0 || High > 100) return "Thresholds must be within 0-100.";
            if (Low >= High) return "Low threshold must be below high threshold.";
            if (High - Low < MinimumGap) return $"Thresholds must be at least {MinimumGap} points apart.";
            return null;
        }
    }

    /// <summary>
    /// What one reading means, and whether that meaning is new.
    /// </summary>
    /// <remarks>
    /// Both halves are needed. The band is a level: "the battery is at or above
    /// the high level right now", which is what a switch wants, because a switch
    /// must be correct even if it missed the moment the level was crossed. The
    /// change flag is an edge: "this is the reading that crossed", which is what
    /// an alert wants, because an alert must fire once and not every 30 seconds.
    /// </remarks>
    public sealed record ThresholdSignal(BatteryBand Band, BatteryBand Previous, bool Changed)
    {
        /// <summary>This reading crossed up into the high band.</summary>
        public bool CrossedHigh => Band == BatteryBand.High && Changed;

        /// <summary>This reading crossed down into the low band.</summary>
        public bool CrossedLow => Band == BatteryBand.Low && Changed;

        /// <summary>This reading came back between the levels.</summary>
        public bool ReturnedToBand => Band == BatteryBand.InBand && Changed;

        public static ThresholdSignal Unknown(BatteryBand previous) =>
            new(BatteryBand.Unknown, previous, previous != BatteryBand.Unknown);
    }

    /// <summary>
    /// Turns a battery reading into a <see cref="ThresholdSignal"/> for one pair
    /// of levels. Pure, stateless, and the only place in the application that
    /// compares a battery percentage with a threshold.
    /// </summary>
    public static class ThresholdMonitor
    {
        /// <summary>
        /// Classifies one reading. <paramref name="previous"/> is the band the
        /// caller saw last time, which is what makes the change flag meaningful.
        /// </summary>
        public static ThresholdSignal Evaluate(int batteryPercent, bool readingUsable, ThresholdPair pair, BatteryBand previous)
        {
            if (pair == null) return ThresholdSignal.Unknown(previous);
            if (pair.ValidationError() != null) return ThresholdSignal.Unknown(previous);
            if (!readingUsable || batteryPercent < 0 || batteryPercent > 100) return ThresholdSignal.Unknown(previous);

            BatteryBand band =
                batteryPercent >= pair.High ? BatteryBand.High :
                batteryPercent <= pair.Low ? BatteryBand.Low :
                BatteryBand.InBand;

            return new ThresholdSignal(band, previous, band != previous);
        }
    }
}
