using System;

namespace BatteryGuardian.SmartPlug
{
    /// <summary>
    /// When a failed plug command should interrupt the user.
    /// </summary>
    /// <remarks>
    /// A snooze means "stop telling me, keep protecting me" - so the reminders
    /// go quiet while the plug carries on switching the charger. That promise
    /// only holds if a failure is still reported: silently failing to switch is
    /// the one thing a snooze must not hide. Kept pure so the rule is testable
    /// without a window or a device.
    /// </remarks>
    public static class SmartPlugFailurePolicy
    {
        /// <summary>
        /// True when the last evaluation failed, the user is snoozed, and this
        /// failure has not been reported yet. One notice per outage, not one per
        /// thirty-second tick.
        /// </summary>
        public static bool ShouldNotify(SmartPlugStatus? status, bool snoozed, bool alreadyNotified)
        {
            if (status == null) return false;
            if (status.LastError == SmartPlugError.None) return false;
            return snoozed && !alreadyNotified;
        }
    }
}
