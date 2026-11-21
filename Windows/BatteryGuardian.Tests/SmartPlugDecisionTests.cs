using BatteryGuardian.SmartPlug;
using Xunit;

namespace BatteryGuardian.Tests
{
 public class SmartPlugDecisionTests
 {
 private static readonly SmartPlugConfig Cfg = new(true, 95, 15, "dev", "1.2.3.4", 6668, 1, 3);

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
