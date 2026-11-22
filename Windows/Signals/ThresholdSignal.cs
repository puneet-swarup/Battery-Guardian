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
    public sealed record ThresholdPair(int High, int Low, int Margin = ThresholdPair.DefaultMargin)
    {
        public const int MinimumGap = 5;

        /// <summary>
        /// How far the battery must move back past a level before the band
        /// changes again. Without it a reading that wobbles across the level -
        /// 95, 94, 96 - re-fires the alert each time, and makes the relay click.
        /// </summary>
        public const int DefaultMargin = 3;

        public static ThresholdPair Default => new(95, 15);

        /// <summary>Null when the pair can be used, otherwise what is wrong with it.</summary>
        public string? ValidationError()
        {
            if (Low < 0 || High > 100) return "Thresholds must be within 0-100.";
            if (Low >= High) return "Low threshold must be below high threshold.";
            if (High - Low < MinimumGap) return $"Thresholds must be at least {MinimumGap} points apart.";
            if (Margin < 0) return "The margin cannot be negative.";
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

            // An unusable reading says nothing about the battery, so it must not
            // clear a band. Reporting Unknown here used to drop an active alert,
            // which then re-fired on the next good reading - the double
            // notification users saw.
            if (!readingUsable || batteryPercent < 0 || batteryPercent > 100)
                return new ThresholdSignal(previous, previous, false);

            // Hysteresis: entering a band needs the level, leaving it needs the
            // level plus the margin. The margin is clamped so a tight pair -
            // 95/90 - still works; it simply gets less room to wobble.
            int margin = Math.Min(pair.Margin, (pair.High - pair.Low - 1) / 2);
            BatteryBand band;
            switch (previous)
            {
                case BatteryBand.High:
                    // Leaving High can land anywhere: a jump from 96 straight to
                    // 5 is a Low reading, not a vague one in between.
                    band = batteryPercent >= pair.High - margin ? BatteryBand.High :
                           batteryPercent <= pair.Low ? BatteryBand.Low :
                           BatteryBand.InBand;
                    break;
                case BatteryBand.Low:
                    band = batteryPercent <= pair.Low + margin ? BatteryBand.Low :
                           batteryPercent >= pair.High ? BatteryBand.High :
                           BatteryBand.InBand;
                    break;
                default:
                    band = batteryPercent >= pair.High ? BatteryBand.High :
                           batteryPercent <= pair.Low ? BatteryBand.Low :
                           BatteryBand.InBand;
                    break;
            }

            return new ThresholdSignal(band, previous, band != previous);
        }
    }
}
