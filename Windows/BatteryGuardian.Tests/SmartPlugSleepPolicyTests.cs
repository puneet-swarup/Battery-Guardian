using BatteryGuardian.SmartPlug;
using Xunit;

namespace BatteryGuardian.Tests
{
 /// <summary>
 /// "Cut the plug when the machine goes away, put it back when it returns."
 /// The threshold rules cannot express this: a laptop that sleeps at 80% is
 /// between the thresholds, and those rules say leave the relay alone.
 /// </summary>
 public class SmartPlugSleepPolicyTests
 {
  [Fact]
  public void SwitchingOffNeedsBothTheFeatureAndThePlug()
  {
   Assert.True(SmartPlugSleepPolicy.ShouldSwitchOff(plugEnabled: true, switchOffWhenSleeping: true));
   Assert.False(SmartPlugSleepPolicy.ShouldSwitchOff(plugEnabled: false, switchOffWhenSleeping: true));
   Assert.False(SmartPlugSleepPolicy.ShouldSwitchOff(plugEnabled: true, switchOffWhenSleeping: false));
  }

  [Theory]
  [InlineData(RelayState.On, "On")]
  [InlineData(RelayState.Off, "Off")]
  [InlineData(RelayState.Unknown, "")]
  public void TheRememberedStateIsTheRelayState(RelayState relay, string expected)
  {
   Assert.Equal(expected, SmartPlugSleepPolicy.Remember(relay));
  }


  [Fact]
  public void AnUnknownStateRemembersNothing()
  {
   // Restoring a guess is worse than restoring nothing: with nothing recorded,
   // the threshold rules decide on the next reading instead.
   Assert.Equal(string.Empty, SmartPlugSleepPolicy.Remember(RelayState.Unknown));
  }

  [Theory]
  [InlineData("On", true)]
  [InlineData("Off", false)]
  public void ARecordedStateIsRestored(string recorded, bool expected)
  {
   Assert.Equal(expected, SmartPlugSleepPolicy.StateToRestore(recorded));
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("garbage")]
  [InlineData(null)]
  public void NothingIsRestoredWithoutARecordedState(string? recorded)
  {
   Assert.Null(SmartPlugSleepPolicy.StateToRestore(recorded));
  }
 }
}
