using System;

namespace BatteryGuardian.QuietHours
{
    /// <summary>
    /// Adapts the app's <see cref="Settings"/> into a <see cref="QuietHoursSchedule"/>.
    /// Keeps the mapping in one place so the evaluator never has to know about the
    /// persisted settings shape.
    /// </summary>
    public sealed class SettingsQuietHoursProvider : IQuietHoursProvider
    {
        private readonly Func<Settings> _settingsAccessor;

        public SettingsQuietHoursProvider(Func<Settings> settingsAccessor)
        {
            _settingsAccessor = settingsAccessor ?? throw new ArgumentNullException(nameof(settingsAccessor));
        }

        public QuietHoursSchedule GetSchedule()
        {
            var settings = _settingsAccessor();
            if (settings == null) return QuietHoursSchedule.Disabled;

            return new QuietHoursSchedule(
                settings.QuietHoursEnabled,
                TimeSpan.FromMinutes(settings.QuietHoursStartMinutes),
                TimeSpan.FromMinutes(settings.QuietHoursEndMinutes));
        }
    }
}
