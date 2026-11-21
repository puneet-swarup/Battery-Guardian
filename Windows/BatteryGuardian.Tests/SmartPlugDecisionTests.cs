using BatteryGuardian.SmartPlug;
using Xunit;

namespace BatteryGuardian.Tests
{
 public class SmartPlugDecisionTests
 {
 private static readonly SmartPlugConfig Cfg = new(true, 95, 15, "dev", "1.2.3.4", 6668, 1, 3);

 /// <summary>
 /// Every combination of battery position and relay state, asserted as an
 /// outcome rather than as a mechanism.
 ///
 /// The bug this replaces was invisible for two releases because the only test
 /// with an unknown relay sat between the thresholds, where doing nothing is
 /// correct, and every turn-on test supplied a readable state. Written from the
 /// requirement - "at 10% the charger must come back on" - rather than from the
 /// code, so it does not inherit the code's blind spots.
 /// </summary>
 [Theory]
 [InlineData(96, RelayState.On, SmartPlugDecision.Action.TurnOff)]
 [InlineData(96, RelayState.Off, SmartPlugDecision.Action.None)]
 [InlineData(96, RelayState.Unknown, SmartPlugDecision.Action.TurnOff)]
 [InlineData(50, RelayState.On, SmartPlugDecision.Action.None)]
 [InlineData(50, RelayState.Off, SmartPlugDecision.Action.None)]
 [InlineData(50, RelayState.Unknown, SmartPlugDecision.Action.None)]
 [InlineData(10, RelayState.On, SmartPlugDecision.Action.None)]
 [InlineData(10, RelayState.Off, SmartPlugDecision.Action.TurnOn)]
 [InlineData(10, RelayState.Unknown, SmartPlugDecision.Action.TurnOn)]
 public void DecisionMatrix(int batteryPercent, RelayState relay, SmartPlugDecision.Action expected)
 {
  var decision = SmartPlugDecision.Evaluate(batteryPercent, true, relay, Cfg);
  Assert.Equal(expected, decision.Action);
 }

 [Fact]
 public void DisabledProducesNoAction()
 {
 var d = SmartPlugDecision.Evaluate(99, true, RelayState.On, Cfg with { Enabled = false });
 Assert.Equal(SmartPlugDecision.Action.None, d.Action);
 }

 [Fact]
 public void HighThresholdTurnsOffWhenOn()
 {
 var d = SmartPlugDecision.Evaluate(95, true, RelayState.On, Cfg);
 Assert.Equal(SmartPlugDecision.Action.TurnOff, d.Action);
 }

 [Fact]
 public void LowThresholdTurnsOnWhenOff()
 {
 var d = SmartPlugDecision.Evaluate(15, true, RelayState.Off, Cfg);
 Assert.Equal(SmartPlugDecision.Action.TurnOn, d.Action);
 }

 [Fact]
 public void BetweenThresholdsRetainsOn()
 {
 var d = SmartPlugDecision.Evaluate(50, true, RelayState.On, Cfg);
 Assert.Equal(SmartPlugDecision.Action.None, d.Action);
 }

 [Fact]
 public void BetweenThresholdsRetainsOff()
 {
 var d = SmartPlugDecision.Evaluate(50, true, RelayState.Off, Cfg);
 Assert.Equal(SmartPlugDecision.Action.None, d.Action);
 }

 [Fact]
 public void UnknownBatteryProducesNoAction()
 {
 var d = SmartPlugDecision.Evaluate(-1, false, RelayState.On, Cfg);
 Assert.Equal(SmartPlugDecision.Action.None, d.Action);
 }

 [Fact]
 public void HighButAlreadyOffDoesNothing()
 {
 var d = SmartPlugDecision.Evaluate(96, true, RelayState.Off, Cfg);
 Assert.Equal(SmartPlugDecision.Action.None, d.Action);
 }

 [Fact]
 public void LowButAlreadyOnDoesNothing()
 {
 var d = SmartPlugDecision.Evaluate(10, true, RelayState.On, Cfg);
 Assert.Equal(SmartPlugDecision.Action.None, d.Action);
 }

 [Fact]
 public void AnUnknownRelayStateStillTurnsThePlugOffAtTheHighThreshold()
 {
  var d = SmartPlugDecision.Evaluate(96, true, RelayState.Unknown, Cfg);
  Assert.Equal(SmartPlugDecision.Action.TurnOff, d.Action);
 }

 [Fact]
 public void AnUnknownRelayStateStillTurnsThePlugOnAtTheLowThreshold()
 {
  // This is the case that sent the user to the phone app: the plug had been
  // off for hours, the state read came back unknown, and the app did nothing.
  var d = SmartPlugDecision.Evaluate(10, true, RelayState.Unknown, Cfg);
  Assert.Equal(SmartPlugDecision.Action.TurnOn, d.Action);
 }

 [Fact]
 public void BetweenThresholdsAnUnknownRelayStateDoesNothing()
 {
  var d = SmartPlugDecision.Evaluate(50, true, RelayState.Unknown, Cfg);
  Assert.Equal(SmartPlugDecision.Action.None, d.Action);
 }

 [Fact]
 public void InvalidConfigReportsError()
 {
 var bad = new SmartPlugConfig(true, 10, 90, "id", "1.2.3.4", 6668, 1, 3);
 Assert.NotNull(bad.ValidationError());
 }
 }
}
