using System;
using System.Collections.Generic;
using System.Linq;
using BatteryGuardian.History;
using Xunit;

namespace BatteryGuardian.Tests
{
    /// <summary>
    /// Tests for the pure battery-history analytics. No I/O, no WPF — deterministic
    /// inputs produce deterministic statistics.
    /// </summary>
    public class BatteryHistoryAnalyzerTests
    {
        private readonly BatteryHistoryAnalyzer _analyzer = new();
        private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private static BatteryHistoryEntry E(int minutesOffset, int percent, bool ac = false) =>
            new(T0.AddMinutes(minutesOffset), percent, ac);

        [Fact]
        public void Analyze_EmptyInput_ReturnsZeroedStats()
        {
            var stats = _analyzer.Analyze(Array.Empty<BatteryHistoryEntry>());

            Assert.Equal(0, stats.TotalEntries);
            Assert.Equal(0, stats.AveragePercent);
            Assert.Equal(TimeSpan.Zero, stats.TimeAboveHighZone);
        }

        [Fact]
        public void Analyze_NullInput_DoesNotThrow()
        {
            var stats = _analyzer.Analyze(null!);
            Assert.Equal(0, stats.TotalEntries);
        }

        [Fact]
        public void Analyze_NullEntriesAreIgnored()
        {
            var entries = new List<BatteryHistoryEntry?> { E(0, 50), null, E(10, 60) }!;

            var stats = _analyzer.Analyze(entries!);

            Assert.Equal(2, stats.TotalEntries);
        }

        [Fact]
        public void Analyze_ComputesMinMaxAndAverage()
        {
            var entries = new[] { E(0, 40), E(10, 60), E(20, 80) };

            var stats = _analyzer.Analyze(entries);

            Assert.Equal(40, stats.MinPercent);
            Assert.Equal(80, stats.MaxPercent);
            Assert.Equal(60, stats.AveragePercent);
        }

        [Fact]
        public void Analyze_EntriesOutOfOrder_AreSortedByTimestamp()
        {
            var entries = new[] { E(20, 30), E(0, 90), E(10, 60) };

            var stats = _analyzer.Analyze(entries);

            Assert.Equal(90, stats.MaxPercent);
            Assert.Equal(30, stats.MinPercent);
        }

        [Fact]
        public void Analyze_TimeAboveHighZone_IsAccumulated()
        {
            // Two consecutive readings above 80 -> 30 minutes classified as "above".
            var entries = new[] { E(0, 90), E(30, 95) };

            var stats = _analyzer.Analyze(entries, highZoneThreshold: 80, lowZoneThreshold: 20);

            Assert.Equal(TimeSpan.FromMinutes(30), stats.TimeAboveHighZone);
            Assert.Equal(TimeSpan.Zero, stats.TimeBelowLowZone);
        }

        [Fact]
        public void Analyze_TimeBelowLowZone_IsAccumulated()
        {
            var entries = new[] { E(0, 10), E(20, 5) };

            var stats = _analyzer.Analyze(entries, highZoneThreshold: 80, lowZoneThreshold: 20);

            Assert.Equal(TimeSpan.FromMinutes(20), stats.TimeBelowLowZone);
            Assert.Equal(TimeSpan.Zero, stats.TimeAboveHighZone);
        }

        [Fact]
        public void Analyze_TimeInIdealZone_IsAccumulated()
        {
            var entries = new[] { E(0, 50), E(45, 55) };

            var stats = _analyzer.Analyze(entries, highZoneThreshold: 80, lowZoneThreshold: 20);

            Assert.Equal(TimeSpan.FromMinutes(45), stats.TimeInIdealZone);
            Assert.Equal(TimeSpan.Zero, stats.TimeAboveHighZone);
            Assert.Equal(TimeSpan.Zero, stats.TimeBelowLowZone);
        }

        [Fact]
        public void Analyze_ZoneBoundaries_AreInclusiveOfIdeal()
        {
            // 80 and 20 sit exactly on the edges -> counted as ideal.
            var entries = new[] { E(0, 80), E(10, 20), E(20, 50) };

            var stats = _analyzer.Analyze(entries, highZoneThreshold: 80, lowZoneThreshold: 20);

            Assert.Equal(TimeSpan.Zero, stats.TimeAboveHighZone);
            Assert.Equal(TimeSpan.Zero, stats.TimeBelowLowZone);
            Assert.Equal(TimeSpan.FromMinutes(20), stats.TimeInIdealZone);
        }

        [Fact]
        public void Analyze_NonIncreasingTimestamp_ContributesZeroSpan()
        {
            // Same timestamp twice must not produce negative durations.
            var entries = new[] { E(0, 90), E(0, 90) };

            var stats = _analyzer.Analyze(entries);

            Assert.Equal(TimeSpan.Zero, stats.TimeAboveHighZone);
        }

        [Fact]
        public void Analyze_DaysCovered_IsAtLeastOne()
        {
            var stats = _analyzer.Analyze(new[] { E(0, 50) });
            Assert.True(stats.DaysCovered >= 1);
        }

        [Fact]
        public void Analyze_AnnualWear_IsNonNegative()
        {
            var entries = new[] { E(0, 95), E(60, 95), E(120, 95) };

            var stats = _analyzer.Analyze(entries);

            Assert.True(stats.EstimatedAnnualWearPercent >= 0);
        }

        [Fact]
        public void Analyze_MoreTimeAboveHighZone_YieldsHigherWearEstimate()
        {
            var shortAbove = _analyzer.Analyze(new[] { E(0, 95), E(30, 95) });
            var longAbove = _analyzer.Analyze(new[] { E(0, 95), E(600, 95) });

            Assert.True(longAbove.EstimatedAnnualWearPercent > shortAbove.EstimatedAnnualWearPercent);
        }

        [Fact]
        public void Analyze_CustomZoneThresholds_AreRespected()
        {
            var entries = new[] { E(0, 70), E(10, 70) };

            var strict = _analyzer.Analyze(entries, highZoneThreshold: 60, lowZoneThreshold: 20);
            var relaxed = _analyzer.Analyze(entries, highZoneThreshold: 90, lowZoneThreshold: 20);

            Assert.Equal(TimeSpan.FromMinutes(10), strict.TimeAboveHighZone);
            Assert.Equal(TimeSpan.FromMinutes(10), relaxed.TimeInIdealZone);
        }
    }
}
