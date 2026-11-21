using System;
using System.Globalization;

namespace BatteryGuardian.QuietHours
{
    /// <summary>
    /// Converts between "minutes since local midnight" (the persisted form) and
    /// a human "HH:mm" string (the settings-UI form). Pure and unit testable so
    /// the parsing rules are covered without driving the WPF window.
    /// </summary>
    public static class TimeOfDayText
    {
        /// <summary>
        /// Formats minutes-since-midnight as "HH:mm". Values outside a day are
        /// wrapped, so 25*60 becomes "01:00".
        /// </summary>
        public static string Format(int minutesSinceMidnight)
        {
            int wrapped = Wrap(minutesSinceMidnight);
            int hours = wrapped / 60;
            int minutes = wrapped % 60;
            return $"{hours:D2}:{minutes:D2}";
        }

        /// <summary>
        /// Parses "HH:mm" (also accepts "H:mm" and a bare number of hours) into
        /// minutes since midnight. Returns false for anything unrecognised.
        /// </summary>
        public static bool TryParse(string? text, out int minutesSinceMidnight)
        {
            minutesSinceMidnight = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var trimmed = text.Trim();

            // Accept "HH:mm" or "H:mm".
            var parts = trimmed.Split(':');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int m))
            {
                if (h < 0 || h > 23 || m < 0 || m > 59) return false;
                minutesSinceMidnight = h * 60 + m;
                return true;
            }

            // Accept a bare hour number, e.g. "7" -> 07:00.
            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bareHour))
            {
                if (bareHour < 0 || bareHour > 23) return false;
                minutesSinceMidnight = bareHour * 60;
                return true;
            }

            return false;
        }

        private static int Wrap(int minutes)
        {
            const int minutesPerDay = 24 * 60;
            int result = minutes % minutesPerDay;
            if (result < 0) result += minutesPerDay;
            return result;
        }
    }
}
