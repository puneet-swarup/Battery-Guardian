using BatteryGuardian.Signals;
﻿using Xunit;

namespace BatteryGuardian.Tests
{
    /// <summary>
    /// Unit tests for the pure alert-evaluation logic. These tests run without any
    /// WPF UI, no timers, and no actual battery — just deterministic inputs and outputs.
    /// </summary>
    public class BatteryAlertEvaluatorTests
    {
        /// <summary>The band the monitor would produce for this reading.</summary>
        private static BatteryBand Band(int percent, int high = 95, int low = 15) =>
            ThresholdMonitor.Evaluate(percent, true, new ThresholdPair(high, low), BatteryBand.Unknown).Band;

        private readonly BatteryAlertEvaluator _evaluator = new();

        // ============================================================
        //  HIGH ALERT TESTS  (triggers when plugged in AND battery high)
        // ============================================================

        [Fact]
        public void Evaluate_OnAcPower_AtExactHighThreshold_TriggersHighAlert()
        {
            var state = _evaluator.Evaluate(95, Band(95, 95, 15), true);

            Assert.True(state.HighAlertShouldBeActive);
            Assert.False(state.LowAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_OnAcPower_AboveHighThreshold_TriggersHighAlert()
        {
            var state = _evaluator.Evaluate(100, Band(100, 95, 15), true);

            Assert.True(state.HighAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_OnAcPower_BelowHighThreshold_DoesNotTriggerHighAlert()
        {
            var state = _evaluator.Evaluate(80, Band(80, 95, 15), true);

            Assert.False(state.HighAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_NotOnAcPower_AboveHighThreshold_DoesNotTriggerHighAlert()
        {
            var state = _evaluator.Evaluate(99, Band(99, 95, 15), false);

            Assert.False(state.HighAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_HighAlertMessage_ContainsBatteryPercentage()
        {
            var state = _evaluator.Evaluate(97, Band(97, 95, 15), true);

            Assert.Contains("97", state.HighAlertMessage);
        }

        // ============================================================
        //  LOW ALERT TESTS  (triggers when unplugged AND battery low)
        // ============================================================

        [Fact]
        public void Evaluate_NotOnAcPower_AtExactLowThreshold_TriggersLowAlert()
        {
            var state = _evaluator.Evaluate(15, Band(15, 95, 15), false);

            Assert.True(state.LowAlertShouldBeActive);
            Assert.False(state.HighAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_NotOnAcPower_BelowLowThreshold_TriggersLowAlert()
        {
            var state = _evaluator.Evaluate(5, Band(5, 95, 15), false);

            Assert.True(state.LowAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_NotOnAcPower_AboveLowThreshold_DoesNotTriggerLowAlert()
        {
            var state = _evaluator.Evaluate(50, Band(50, 95, 15), false);

            Assert.False(state.LowAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_OnAcPower_BelowLowThreshold_DoesNotTriggerLowAlert()
        {
            var state = _evaluator.Evaluate(10, Band(10, 95, 15), true);

            Assert.False(state.LowAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_LowAlertMessage_ContainsBatteryPercentage()
        {
            var state = _evaluator.Evaluate(12, Band(12, 95, 15), false);

            Assert.Contains("12", state.LowAlertMessage);
        }

        // ============================================================
        //  EDGE CASES
        // ============================================================

        [Fact]
        public void Evaluate_HighAndLowAlertsNeverBothActive()
        {
            for (int percent = 0; percent <= 100; percent += 5)
            {
                var onAc = _evaluator.Evaluate(percent, true, 95, 15);
                var onBattery = _evaluator.Evaluate(percent, false, 95, 15);

                Assert.False(onAc.HighAlertShouldBeActive && onAc.LowAlertShouldBeActive,
                    $"Both alerts active at {percent}% on AC power");
                Assert.False(onBattery.HighAlertShouldBeActive && onBattery.LowAlertShouldBeActive,
                    $"Both alerts active at {percent}% on battery");
            }
        }

        [Fact]
        public void Evaluate_CustomThresholds_AreRespected()
        {
            var highState = _evaluator.Evaluate(85, Band(85, 80, 20), true);
            var lowState = _evaluator.Evaluate(15, Band(15, 80, 20), false);

            Assert.True(highState.HighAlertShouldBeActive);
            Assert.True(lowState.LowAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_BatteryZeroAndNotOnAcPower_TriggersLowAlert()
        {
            var state = _evaluator.Evaluate(0, Band(0, 95, 15), false);

            Assert.True(state.LowAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_BatteryHundredAndOnAcPower_TriggersHighAlert()
        {
            var state = _evaluator.Evaluate(100, Band(100, 95, 15), true);

            Assert.True(state.HighAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_MidRangeBatteryAndAnyPowerState_NoAlerts()
        {
            var onAc = _evaluator.Evaluate(50, Band(50, 95, 15), true);
            var onBattery = _evaluator.Evaluate(50, Band(50, 95, 15), false);

            Assert.False(onAc.HighAlertShouldBeActive);
            Assert.False(onAc.LowAlertShouldBeActive);
            Assert.False(onBattery.HighAlertShouldBeActive);
            Assert.False(onBattery.LowAlertShouldBeActive);
        }
    }
}