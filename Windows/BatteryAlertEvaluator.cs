using System;
using BatteryGuardian.Signals;

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
        /// Decides what alerts should be active for a reading.
        /// </summary>
        /// <remarks>
        /// The comparison with the thresholds is not done here: it happens once,
        /// in <see cref="ThresholdMonitor"/>, and arrives as a band. This class
        /// only knows what an alert means - a high alert needs the charger
        /// plugged in, a low alert needs it out.
        /// </remarks>
        /// <param name="batteryPercent">Current battery percentage, for the message.</param>
        /// <param name="band">Where that percentage sits against the alert levels.</param>
        /// <param name="isOnAcPower">True if the charger is physically plugged in.</param>
        public AlertState Evaluate(int batteryPercent, BatteryBand band, bool isOnAcPower)
        {
            bool highCondition = isOnAcPower && band == BatteryBand.High;
            bool lowCondition = !isOnAcPower && band == BatteryBand.Low;

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