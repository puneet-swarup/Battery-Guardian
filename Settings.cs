using System;

namespace BatteryGuardian
{
    /// <summary>
    /// Persisted user configuration for the Windows application.
    /// Property defaults are chosen so a missing JSON field yields sensible
    /// behaviour (System.Text.Json leaves absent properties at their defaults).
    /// </summary>
    public class Settings
    {
        public int HighBatteryThreshold { get; set; } = 95;
        public int LowBatteryThreshold { get; set; } = 15;
        public int AlertRepeatIntervalSeconds { get; set; } = 300;
        public bool CheckForUpdatesAutomatically { get; set; } = true;
        public DateTime? LastUpdateCheckUtc { get; set; } = null;
        public bool DiagnosticLoggingEnabled { get; set; } = false;
        public DateTime? SnoozedUntilUtc { get; set; } = null;

        // ---- Battery history (feature 1) ----
        /// <summary>When true, periodic readings are recorded for trend analysis.</summary>
        public bool HistoryEnabled { get; set; } = true;

        /// <summary>Lower edge of the "ideal" charge window used by the analyzer.</summary>
        public int IdealZoneLowPercent { get; set; } = 20;

        /// <summary>Upper edge of the "ideal" charge window used by the analyzer.</summary>
        public int IdealZoneHighPercent { get; set; } = 80;

        // ---- Quiet hours / DND (feature 5) ----
        /// <summary>When true, the user-defined quiet-hours window is honoured.</summary>
        public bool QuietHoursEnabled { get; set; } = false;

        /// <summary>Quiet-hours start, minutes since local midnight.</summary>
        public int QuietHoursStartMinutes { get; set; } = 22 * 60; // 22:00

        /// <summary>Quiet-hours end, minutes since local midnight.</summary>
        public int QuietHoursEndMinutes { get; set; } = 7 * 60;    // 07:00

        /// <summary>Suppress audible alerts while Windows Focus Assist is on.</summary>
        public bool RespectFocusAssist { get; set; } = true;

        // ---- Charge limit (feature 14) ----
        /// <summary>Last charge limit the user applied, for display (0 = never set).</summary>
        public int LastAppliedChargeLimitPercent { get; set; } = 0;
    }
}
