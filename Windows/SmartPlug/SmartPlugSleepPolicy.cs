using System;

namespace BatteryGuardian.SmartPlug
{
    /// <summary>
    /// The rules behind "switch the plug off when the machine goes away, and put
    /// it back when it returns".
    /// </summary>
    /// <remarks>
    /// This exists because the threshold rules cannot express the intent. A
    /// laptop that sleeps at 80% is between the thresholds, and the threshold
    /// rules say "leave the relay alone" - which would leave it off until the
    /// battery fell to 15%. Restoring the remembered state is a separate
    /// instruction, so it is kept separate and testable.
    /// </remarks>
    public static class SmartPlugSleepPolicy
    {
        public const string RelayOn = "On";
        public const string RelayOff = "Off";

        /// <summary>Whether a sleep or shutdown should cut the plug.</summary>
        public static bool ShouldSwitchOff(bool plugEnabled, bool switchOffWhenSleeping)
            => plugEnabled && switchOffWhenSleeping;

        /// <summary>
        /// The value to persist for the current relay state. An unknown state
        /// records nothing, because restoring a guess is worse than restoring
        /// nothing - the threshold rules then decide.
        /// </summary>
        public static string Remember(RelayState relay) => relay switch
        {
            RelayState.On => RelayOn,
            RelayState.Off => RelayOff,
            _ => string.Empty,
        };

        /// <summary>
        /// The relay state to restore, or null when nothing usable was recorded.
        /// </summary>
        public static bool? StateToRestore(string? remembered) => remembered switch
        {
            RelayOn => true,
            RelayOff => false,
            _ => null,
        };
    }
}
