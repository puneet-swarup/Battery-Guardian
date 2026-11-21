using System;

namespace BatteryGuardian.QuietHours
{
    /// <summary>
    /// Decides whether audible alerting should currently be suppressed.
    ///
    /// Suppression is active when EITHER:
    ///   - the user's configured quiet-hours window contains the current local time, OR
    ///   - the operating system is in Do Not Disturb / Focus Assist mode.
    ///
    /// Pure orchestration over injected abstractions — no direct OS calls,
    /// fully unit testable, single responsibility.
    /// </summary>
    public sealed class QuietHoursEvaluator
    {
        private readonly IQuietHoursProvider _scheduleProvider;
        private readonly IDoNotDisturbDetector _dndDetector;
        private readonly IClock _clock;

        public QuietHoursEvaluator(
            IQuietHoursProvider scheduleProvider,
            IDoNotDisturbDetector dndDetector,
            IClock clock)
        {
            _scheduleProvider = scheduleProvider ?? throw new ArgumentNullException(nameof(scheduleProvider));
            _dndDetector = dndDetector ?? throw new ArgumentNullException(nameof(dndDetector));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>
        /// True when audible alerts (voice + beep) should be suppressed.
        /// Callers may still log the event and show a silent notification.
        /// </summary>
        public bool IsAudibleAlertSuppressed()
        {
            if (IsWithinQuietHours()) return true;
            return SafelyDetectDnd();
        }

        /// <summary>True only if the configured quiet-hours window is active now.</summary>
        public bool IsWithinQuietHours()
        {
            var schedule = _scheduleProvider.GetSchedule() ?? QuietHoursSchedule.Disabled;
            return schedule.IsQuietAt(_clock.NowLocal.TimeOfDay);
        }

        private bool SafelyDetectDnd()
        {
            try
            {
                return _dndDetector.IsDoNotDisturbActive();
            }
            catch
            {
                // A failure to read DND state must never suppress or crash alerts.
                return false;
            }
        }
    }
}
