using System;

namespace BatteryGuardian
{
    /// <summary>
    /// Represents the health of a laptop battery, derived from its design capacity
    /// versus its current full-charge capacity.
    /// </summary>
    public class BatteryHealthInfo
    {
        /// <summary>Original design capacity in milliwatt-hours (mWh).</summary>
        public uint DesignCapacityMwh { get; set; }

        /// <summary>Current full-charge capacity in milliwatt-hours (mWh).</summary>
        public uint FullChargeCapacityMwh { get; set; }

        /// <summary>
        /// Health percentage (0-100). Returns -1 if it cannot be calculated.
        /// </summary>
        public int HealthPercent
        {
            get
            {
                if (DesignCapacityMwh == 0 || FullChargeCapacityMwh == 0) return -1;
                int pct = (int)Math.Round((double)FullChargeCapacityMwh / DesignCapacityMwh * 100.0);
                // Clamp to a sane range.
                if (pct < 0) return 0;
                if (pct > 100) return 100;
                return pct;
            }
        }

        /// <summary>A human-readable description of the health status.</summary>
        public string HealthLabel
        {
            get
            {
                int pct = HealthPercent;
                if (pct < 0) return "Unavailable";
                if (pct >= 80) return "Good";
                if (pct >= 60) return "Fair";
                return "Poor";
            }
        }
    }
}