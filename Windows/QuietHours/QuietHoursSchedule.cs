using System;

namespace BatteryGuardian.QuietHours
{
    /// <summary>
    /// A daily quiet-hours window, expressed in local time. Supports windows that
    /// span midnight (e.g. 22:00 to 07:00). Immutable value object.
    /// </summary>
    public sealed class QuietHoursSchedule
    {
        public bool Enabled { get; }
        public TimeSpan Start { get; }
        public TimeSpan End { get; }

        public QuietHoursSchedule(bool enabled, TimeSpan start, TimeSpan end)
        {
            Enabled = enabled;
            Start = Normalize(start);
            End = Normalize(end);
        }

        public static QuietHoursSchedule Disabled =>
            new(false, TimeSpan.Zero, TimeSpan.Zero);

        /// <summary>True if the window spans midnight (start is later than end).</summary>
        public bool SpansMidnight => Start > End;

        /// <summary>
        /// Determines whether the supplied local time falls inside the quiet window.
        /// A disabled schedule is never quiet.
        /// </summary>
        public bool IsQuietAt(TimeSpan localTimeOfDay)
        {
            if (!Enabled) return false;

            var t = Normalize(localTimeOfDay);

            if (SpansMidnight)
            {
                // e.g. 22:00 -> 07:00 : quiet if t >= 22:00 OR t < 07:00
                return t >= Start || t < End;
            }

            // Same-day window, e.g. 13:00 -> 14:30 : quiet if start <= t < end
            return t >= Start && t < End;
        }

        private static TimeSpan Normalize(TimeSpan value)
        {
            // Clamp into [0, 24h). A value of exactly 24h wraps to zero.
            var ticks = value.Ticks % TimeSpan.TicksPerDay;
            if (ticks < 0) ticks += TimeSpan.TicksPerDay;
            return TimeSpan.FromTicks(ticks);
        }
    }
}
