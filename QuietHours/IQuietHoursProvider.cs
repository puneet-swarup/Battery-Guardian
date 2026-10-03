using System;

namespace BatteryGuardian.QuietHours
{
    /// <summary>
    /// Supplies the current quiet-hours configuration. Abstracted so tests can
    /// inject a fixed schedule without touching the settings file.
    /// </summary>
    public interface IQuietHoursProvider
    {
        QuietHoursSchedule GetSchedule();
    }

    /// <summary>
    /// Detects whether the operating system is currently in "Do Not Disturb" /
    /// Focus Assist mode. Abstracted for testability; the Windows implementation
    /// is a separate class.
    /// </summary>
    public interface IDoNotDisturbDetector
    {
        bool IsDoNotDisturbActive();
    }

    /// <summary>
    /// Clock abstraction so quiet-hours evaluation can be tested deterministically.
    /// </summary>
    public interface IClock
    {
        DateTime NowLocal { get; }
    }

    /// <summary>Default clock backed by the system.</summary>
    public sealed class SystemClock : IClock
    {
        public DateTime NowLocal => DateTime.Now;
    }
}
