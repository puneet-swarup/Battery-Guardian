using System;

namespace BatteryGuardian
{
    /// <summary>
    /// Represents the desired state of alerts based on the current battery readings.
    /// This is a pure data object - no side effects, no WPF, no I/O.
    /// </summary>
    public class AlertState
    {
        public bool HighAlertShouldBeActive { get; set; }
        public bool LowAlertShouldBeActive { get; set; }
        public string HighAlertMessage { get; set; } = "";
        public string LowAlertMessage { get; set; } = "";
    }

    /// <summary>
    /// Pure logic for deciding whether a High or Low battery alert should currently be active.
    /// Deliberately has no dependency on WPF, timers, or Windows APIs so it can be unit tested.
    /// </summary>
    public class BatteryAlertEvaluator
    {
        /// <summary>
        /// Evaluates the current battery state and returns what alerts should be active.
        /// </summary>
        /// <param name="batteryPercent">Current battery percentage (0-100).</param>
        /// <param name="isOnAcPower">True if the charger is physically plugged in.</param>
        /// <param name="highThreshold">Configured high threshold (e.g., 95).</param>
        /// <param name="lowThreshold">Configured low threshold (e.g., 15).</param>
        public AlertState Evaluate(
            int batteryPercent,
            bool isOnAcPower,
            int highThreshold,
            int lowThreshold)
        {
            bool highCondition = isOnAcPower && batteryPercent >= highThreshold;
            bool lowCondition = !isOnAcPower && batteryPercent <= lowThreshold;

            var state = new AlertState
            {
                HighAlertShouldBeActive = highCondition,
                LowAlertShouldBeActive = lowCondition
            };

            if (highCondition)
            {
                state.HighAlertMessage =
                    $"Battery is at {batteryPercent}%. Consider unplugging the charger.";
            }

            if (lowCondition)
            {
                state.LowAlertMessage =
                    $"Battery is low at {batteryPercent}%. Please connect the charger.";
            }

            return state;
        }
    }
}