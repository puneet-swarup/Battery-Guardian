using BatteryGuardian.Signals;
using Xunit;

namespace BatteryGuardian.Tests
{
    /// <summary>
    /// One place decides what a reading means, so this is the one place the
    /// boundaries are tested. Alerts and the smart plug both consume the result.
    /// </summary>
    public class ThresholdMonitorTests
    {
        private static readonly ThresholdPair Pair = new(95, 15);

        // ---- the bands, including both boundaries -------------------------

        [Theory]
        [InlineData(100, BatteryBand.High)]
        [InlineData(96, BatteryBand.High)]
        [InlineData(95, BatteryBand.High)]   // exactly high is high
        [InlineData(94, BatteryBand.InBand)]
        [InlineData(50, BatteryBand.InBand)]
        [InlineData(16, BatteryBand.InBand)]
        [InlineData(15, BatteryBand.Low)]    // exactly low is low
        [InlineData(14, BatteryBand.Low)]
        [InlineData(0, BatteryBand.Low)]
        public void ClassifiesEveryReading(int percent, BatteryBand expected)
        {
            var signal = ThresholdMonitor.Evaluate(percent, true, Pair, BatteryBand.Unknown);
            Assert.Equal(expected, signal.Band);
        }

        [Theory]
        [InlineData(-1)]     // the "unknown" sentinel the app uses
        [InlineData(-50)]
        [InlineData(101)]    // a reading that cannot be real
        [InlineData(999)]
        public void UnusablePercentagesAreUnknown(int percent)
        {
            var signal = ThresholdMonitor.Evaluate(percent, true, Pair, BatteryBand.InBand);
            Assert.Equal(BatteryBand.Unknown, signal.Band);
        }

        [Fact]
        public void AReadingTheCallerCannotUseIsUnknown()
        {
            var signal = ThresholdMonitor.Evaluate(80, readingUsable: false, Pair, BatteryBand.InBand);
            Assert.Equal(BatteryBand.Unknown, signal.Band);
        }

        // ---- invalid pairs cannot produce a band --------------------------

        [Theory]
        [InlineData(15, 95)]   // inverted
        [InlineData(50, 50)]   // identical
        [InlineData(95, 92)]   // too close together
        [InlineData(-1, 15)]   // out of range
        [InlineData(95, 101)]
        public void AnInvalidPairYieldsUnknown(int high, int low)
        {
            var signal = ThresholdMonitor.Evaluate(50, true, new ThresholdPair(high, low), BatteryBand.InBand);
            Assert.Equal(BatteryBand.Unknown, signal.Band);
        }

        [Fact]
        public void ANullPairYieldsUnknown()
        {
            var signal = ThresholdMonitor.Evaluate(50, true, null!, BatteryBand.InBand);
            Assert.Equal(BatteryBand.Unknown, signal.Band);
        }

        // ---- level versus edge --------------------------------------------

        [Fact]
        public void StayingInABandIsNotAChange()
        {
            var signal = ThresholdMonitor.Evaluate(97, true, Pair, BatteryBand.High);
            Assert.Equal(BatteryBand.High, signal.Band);
            Assert.False(signal.Changed);
            Assert.False(signal.CrossedHigh);
        }

        [Fact]
        public void CrossingUpIsAChange()
        {
            var signal = ThresholdMonitor.Evaluate(96, true, Pair, BatteryBand.InBand);
            Assert.True(signal.Changed);
            Assert.True(signal.CrossedHigh);
            Assert.False(signal.CrossedLow);
        }

        [Fact]
        public void CrossingDownIsAChange()
        {
            var signal = ThresholdMonitor.Evaluate(14, true, Pair, BatteryBand.InBand);
            Assert.True(signal.Changed);
            Assert.True(signal.CrossedLow);
            Assert.False(signal.CrossedHigh);
        }

        [Fact]
        public void ReturningToTheBandIsAChange()
        {
            var signal = ThresholdMonitor.Evaluate(80, true, Pair, BatteryBand.High);
            Assert.True(signal.Changed);
            Assert.True(signal.ReturnedToBand);
            Assert.False(signal.CrossedHigh);
        }

        [Fact]
        public void GoingFromUnknownToABandIsAChange()
        {
            var signal = ThresholdMonitor.Evaluate(96, true, Pair, BatteryBand.Unknown);
            Assert.True(signal.Changed);
            Assert.True(signal.CrossedHigh);
        }

        [Fact]
        public void GoingFromABandToUnknownIsAChange()
        {
            var signal = ThresholdMonitor.Evaluate(-1, true, Pair, BatteryBand.High);
            Assert.Equal(BatteryBand.Unknown, signal.Band);
            Assert.True(signal.Changed);
            Assert.False(signal.CrossedHigh);
        }

        [Fact]
        public void RemainingUnknownIsNotAChange()
        {
            var signal = ThresholdMonitor.Evaluate(-1, true, Pair, BatteryBand.Unknown);
            Assert.Equal(BatteryBand.Unknown, signal.Band);
            Assert.False(signal.Changed);
        }

        // ---- hysteresis: leaving a band needs the margin -------------------

        [Theory]
        [InlineData(95, BatteryBand.High)]   // at the level
        [InlineData(94, BatteryBand.High)]   // one below, still high
        [InlineData(92, BatteryBand.High)]   // margin edge, still high
        [InlineData(91, BatteryBand.InBand)] // past the margin, back in band
        public void AHighBandHoldsUntilTheBatteryFallsPastTheMargin(int percent, BatteryBand expected)
        {
            var signal = ThresholdMonitor.Evaluate(percent, true, Pair, BatteryBand.High);
            Assert.Equal(expected, signal.Band);
        }

        [Theory]
        [InlineData(15, BatteryBand.Low)]
        [InlineData(16, BatteryBand.Low)]
        [InlineData(18, BatteryBand.Low)]
        [InlineData(19, BatteryBand.InBand)]
        public void ALowBandHoldsUntilTheBatteryRisesPastTheMargin(int percent, BatteryBand expected)
        {
            var signal = ThresholdMonitor.Evaluate(percent, true, Pair, BatteryBand.Low);
            Assert.Equal(expected, signal.Band);
        }

        [Fact]
        public void WobblingAcrossTheLevelDoesNotChangeTheBand()
        {
            // The case users reported: 95 fires, 94 must not re-arm, 96 must not
            // fire again. With the margin, the band never leaves High.
            BatteryBand band = ThresholdMonitor.Evaluate(95, true, Pair, BatteryBand.Unknown).Band;
            Assert.Equal(BatteryBand.High, band);

            foreach (int percent in new[] { 94, 96, 95, 93 })
            {
                var signal = ThresholdMonitor.Evaluate(percent, true, Pair, band);
                Assert.Equal(BatteryBand.High, signal.Band);
                Assert.False(signal.Changed, $"a reading of {percent} should not be a change");
                band = signal.Band;
            }
        }

        [Fact]
        public void AnUnusableReadingDoesNotClearTheBand()
        {
            // Reporting Unknown here dropped an active alert, which then fired
            // again on the next good reading - the double notification.
            var signal = ThresholdMonitor.Evaluate(-1, true, Pair, BatteryBand.High);
            Assert.Equal(BatteryBand.High, signal.Band);
            Assert.False(signal.Changed);
        }

        [Fact]
        public void AnInvalidPairStillClearsTheBand()
        {
            // A broken configuration is different from a missing reading: there is
            // nothing to hold on to, so the band becomes Unknown.
            var signal = ThresholdMonitor.Evaluate(50, true, new ThresholdPair(50, 50), BatteryBand.High);
            Assert.Equal(BatteryBand.Unknown, signal.Band);
        }

        // ---- the pair's own rules -----------------------------------------

        [Theory]
        [InlineData(95, 15, true)]
        [InlineData(95, 15, false, 0)]      // no margin is allowed
        [InlineData(95, 15, false, -1)]     // a negative margin is not
        [InlineData(95, 90, false, 3)]      // margin does not fit the gap
        [InlineData(100, 0, true)]
        [InlineData(95, 90, true)]
        [InlineData(95, 91, false)]   // four points apart
        [InlineData(50, 50, false)]
        [InlineData(15, 95, false)]
        [InlineData(101, 15, false)]
        [InlineData(95, -1, false)]
        public void ThePairValidatesItself(int high, int low, bool valid)
        {
            var pair = new ThresholdPair(high, low);
            Assert.Equal(valid, pair.ValidationError() == null);
        }

        [Theory]
        [InlineData(3, true)]
        [InlineData(0, true)]
        [InlineData(-1, false)]
        [InlineData(40, false)]   // too large for a 95/15 pair
        public void TheMarginHasItsOwnRules(int margin, bool valid)
        {
            var pair = new ThresholdPair(95, 15, margin);
            Assert.Equal(valid, pair.ValidationError() == null);
        }
    }
}
