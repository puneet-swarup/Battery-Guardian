using System;

namespace BatteryGuardian.History
{
    /// <summary>
    /// A single point-in-time battery reading, recorded by the history service.
    /// Immutable value object with no behaviour — pure data.
    /// </summary>
    public sealed record BatteryHistoryEntry
    {
        /// <summary>UTC timestamp of the reading.</summary>
        public DateTime Utc { get; init; }

        /// <summary>Battery percentage at the time of the reading (0-100).</summary>
        public int Percent { get; init; }

        /// <summary>True if the charger was physically plugged in.</summary>
        public bool IsOnAcPower { get; init; }

        public BatteryHistoryEntry(DateTime utc, int percent, bool isOnAcPower)
        {
            Utc = utc;
            Percent = percent;
            IsOnAcPower = isOnAcPower;
        }
    }
}
