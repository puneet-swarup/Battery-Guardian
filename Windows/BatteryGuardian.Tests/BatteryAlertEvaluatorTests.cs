using Xunit;

namespace BatteryGuardian.Tests
{
    /// <summary>
    /// Unit tests for the pure alert-evaluation logic. These tests run without any
    /// WPF UI, no timers, and no actual battery — just deterministic inputs and outputs.
    /// </summary>
    public class BatteryAlertEvaluatorTests
    {
        private readonly BatteryAlertEvaluator _evaluator = new();

        // ============================================================
        //  HIGH ALERT TESTS  (triggers when plugged in AND battery high)
        // ============================================================

        [Fact]
        public void Evaluate_OnAcPower_AtExactHighThreshold_TriggersHighAlert()
        {
            var state = _evaluator.Evaluate(batteryPercent: 95, isOnAcPower: true, highThreshold: 95, lowThreshold: 15);

            Assert.True(state.HighAlertShouldBeActive);
            Assert.False(state.LowAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_OnAcPower_AboveHighThreshold_TriggersHighAlert()
        {
            var state = _evaluator.Evaluate(batteryPercent: 100, isOnAcPower: true, highThreshold: 95, lowThreshold: 15);

            Assert.True(state.HighAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_OnAcPower_BelowHighThreshold_DoesNotTriggerHighAlert()
        {
            var state = _evaluator.Evaluate(batteryPercent: 80, isOnAcPower: true, highThreshold: 95, lowThreshold: 15);

            Assert.False(state.HighAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_NotOnAcPower_AboveHighThreshold_DoesNotTriggerHighAlert()
        {
            var state = _evaluator.Evaluate(batteryPercent: 99, isOnAcPower: false, highThreshold: 95, lowThreshold: 15);

            Assert.False(state.HighAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_HighAlertMessage_ContainsBatteryPercentage()
        {
            var state = _evaluator.Evaluate(batteryPercent: 97, isOnAcPower: true, highThreshold: 95, lowThreshold: 15);

            Assert.Contains("97", state.HighAlertMessage);
        }

        // ============================================================
        //  LOW ALERT TESTS  (triggers when unplugged AND battery low)
        // ============================================================

        [Fact]
        public void Evaluate_NotOnAcPower_AtExactLowThreshold_TriggersLowAlert()
        {
            var state = _evaluator.Evaluate(batteryPercent: 15, isOnAcPower: false, highThreshold: 95, lowThreshold: 15);

            Assert.True(state.LowAlertShouldBeActive);
            Assert.False(state.HighAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_NotOnAcPower_BelowLowThreshold_TriggersLowAlert()
        {
            var state = _evaluator.Evaluate(batteryPercent: 5, isOnAcPower: false, highThreshold: 95, lowThreshold: 15);

            Assert.True(state.LowAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_NotOnAcPower_AboveLowThreshold_DoesNotTriggerLowAlert()
        {
            var state = _evaluator.Evaluate(batteryPercent: 50, isOnAcPower: false, highThreshold: 95, lowThreshold: 15);

            Assert.False(state.LowAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_OnAcPower_BelowLowThreshold_DoesNotTriggerLowAlert()
        {
            var state = _evaluator.Evaluate(batteryPercent: 10, isOnAcPower: true, highThreshold: 95, lowThreshold: 15);

            Assert.False(state.LowAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_LowAlertMessage_ContainsBatteryPercentage()
        {
            var state = _evaluator.Evaluate(batteryPercent: 12, isOnAcPower: false, highThreshold: 95, lowThreshold: 15);

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
            var highState = _evaluator.Evaluate(85, true, 80, 20);
            var lowState = _evaluator.Evaluate(15, false, 80, 20);

            Assert.True(highState.HighAlertShouldBeActive);
            Assert.True(lowState.LowAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_BatteryZeroAndNotOnAcPower_TriggersLowAlert()
        {
            var state = _evaluator.Evaluate(0, false, 95, 15);

            Assert.True(state.LowAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_BatteryHundredAndOnAcPower_TriggersHighAlert()
        {
            var state = _evaluator.Evaluate(100, true, 95, 15);

            Assert.True(state.HighAlertShouldBeActive);
        }

        [Fact]
        public void Evaluate_MidRangeBatteryAndAnyPowerState_NoAlerts()
        {
            var onAc = _evaluator.Evaluate(50, true, 95, 15);
            var onBattery = _evaluator.Evaluate(50, false, 95, 15);

            Assert.False(onAc.HighAlertShouldBeActive);
            Assert.False(onAc.LowAlertShouldBeActive);
            Assert.False(onBattery.HighAlertShouldBeActive);
            Assert.False(onBattery.LowAlertShouldBeActive);
        }
    }
}