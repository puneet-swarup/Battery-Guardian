using System;
using System.Collections.Generic;
using System.Linq;

namespace BatteryGuardian.History
{
    /// <summary>
    /// Aggregated statistics derived from a set of battery history entries.
    /// Pure data object produced by <see cref="BatteryHistoryAnalyzer"/>.
    /// </summary>
    public sealed class BatteryHistoryStats
    {
        public int TotalEntries { get; init; }
        public int DaysCovered { get; init; }
        public int AveragePercent { get; init; }
        public int MinPercent { get; init; }
        public int MaxPercent { get; init; }

        /// <summary>Total time spent above the "high" zone threshold.</summary>
        public TimeSpan TimeAboveHighZone { get; init; }

        /// <summary>Total time spent below the "low" zone threshold.</summary>
        public TimeSpan TimeBelowLowZone { get; init; }

        /// <summary>Time spent within the ideal zone (low..high inclusive).</summary>
        public TimeSpan TimeInIdealZone { get; init; }

        /// <summary>
        /// Rough estimate of annual capacity loss caused by time spent outside the
        /// ideal zone. Heuristic, not a physical model — designed to be directionally
        /// useful, not precise.
        /// </summary>
        public double EstimatedAnnualWearPercent { get; init; }
    }

    /// <summary>
    /// Pure analytics over battery history. No I/O, no WPF, no timers — fully
    /// unit testable. Single responsibility: turn a sequence of readings into
    /// summary statistics.
    /// </summary>
    public sealed class BatteryHistoryAnalyzer
    {
        // Heuristic wear coefficients (% capacity lost per hour spent in each zone).
        // These are deliberately conservative ballpark figures based on published
        // lithium-ion degradation curves, not a physics model.
        private const double WearPerHourAboveHigh = 0.0040;
        private const double WearPerHourBelowLow = 0.0030;

        /// <summary>
        /// Analyse the given readings. Entries are sorted by timestamp internally.
        /// The zone thresholds default to the ideal-window edges (e.g. 80/20).
        /// </summary>
        public BatteryHistoryStats Analyze(
            IEnumerable<BatteryHistoryEntry> entries,
            int highZoneThreshold = 80,
            int lowZoneThreshold = 20)
        {
            var ordered = (entries ?? Enumerable.Empty<BatteryHistoryEntry>())
                .Where(e => e != null)
                .OrderBy(e => e.Utc)
                .ToList();

            if (ordered.Count == 0)
            {
                return new BatteryHistoryStats();
            }

            int min = ordered.Min(e => e.Percent);
            int max = ordered.Max(e => e.Percent);
            int avg = (int)Math.Round(ordered.Average(e => e.Percent));

            TimeSpan above = TimeSpan.Zero;
            TimeSpan below = TimeSpan.Zero;
            TimeSpan ideal = TimeSpan.Zero;

            // Each entry (except the last) contributes the duration until the next
            // reading, classified by the percentage observed at that entry.
            for (int i = 0; i < ordered.Count - 1; i++)
            {
                var current = ordered[i];
                var next = ordered[i + 1];
                var span = next.Utc - current.Utc;
                if (span < TimeSpan.Zero) span = TimeSpan.Zero;

                if (current.Percent > highZoneThreshold)
                    above += span;
                else if (current.Percent < lowZoneThreshold)
                    below += span;
                else
                    ideal += span;
            }

            var days = (int)Math.Max(1, Math.Ceiling((ordered.Last().Utc - ordered.First().Utc).TotalDays));

            double wear = above.TotalHours * WearPerHourAboveHigh
                        + below.TotalHours * WearPerHourBelowLow;
            double annualWear = days > 0 ? wear * (365.0 / days) : 0.0;

            return new BatteryHistoryStats
            {
                TotalEntries = ordered.Count,
                DaysCovered = days,
                AveragePercent = avg,
                MinPercent = min,
                MaxPercent = max,
                TimeAboveHighZone = above,
                TimeBelowLowZone = below,
                TimeInIdealZone = ideal,
                EstimatedAnnualWearPercent = Math.Round(annualWear, 2)
            };
        }
    }
}
