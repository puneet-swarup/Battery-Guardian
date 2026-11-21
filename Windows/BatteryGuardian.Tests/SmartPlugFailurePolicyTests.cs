using BatteryGuardian.SmartPlug;
using Xunit;

namespace BatteryGuardian.Tests
{
 /// <summary>
 /// A snooze means "stop telling me, keep protecting me". That promise is only
 /// worth anything if a failed switch is still reported, once, without shouting.
 /// </summary>
 public class SmartPlugFailurePolicyTests
 {
  private static SmartPlugStatus Failed() =>
   new(true, false, RelayState.Unknown, null, SmartPlugError.Unreachable, "plug did not answer");

  private static SmartPlugStatus Succeeded() =>
   new(true, true, RelayState.Off, null, SmartPlugError.None, "Battery >= high threshold");

  [Fact]
  public void ReportsAFailureThatHappenedDuringASnooze()
  {
   Assert.True(SmartPlugFailurePolicy.ShouldNotify(Failed(), snoozed: true, alreadyNotified: false));
  }

  [Fact]
  public void ReportsEachOutageOnlyOnce()
  {
   Assert.False(SmartPlugFailurePolicy.ShouldNotify(Failed(), snoozed: true, alreadyNotified: true));
  }

  [Fact]
  public void StaysQuietOutsideASnooze()
  {
   // Outside a snooze the ordinary alerts already carry the message.
   Assert.False(SmartPlugFailurePolicy.ShouldNotify(Failed(), snoozed: false, alreadyNotified: false));
  }

  [Fact]
  public void StaysQuietWhenTheSwitchWorked()
  {
   Assert.False(SmartPlugFailurePolicy.ShouldNotify(Succeeded(), snoozed: true, alreadyNotified: false));
  }

  [Fact]
  public void IgnoresAMissingStatus()
  {
   Assert.False(SmartPlugFailurePolicy.ShouldNotify(null, snoozed: true, alreadyNotified: false));
  }
 }
}
