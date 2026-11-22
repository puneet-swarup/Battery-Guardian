using System;
using BatteryGuardian.Signals;
using System.Threading;
using System.Threading.Tasks;

namespace BatteryGuardian.SmartPlug
{
 /// <summary>Immutable configuration for the controller.</summary>
 public sealed record SmartPlugConfig(
 bool Enabled,
 int HighThreshold,
 int LowThreshold,
 string DeviceId,
 string Host,
 int Port,
 int RelayDp,
 int ProtocolVersion)
 {
 /// <summary>
 /// How far apart the two levels must be. A relay that switches on a one-point
 /// change chatters as the battery moves, which wears the contacts and cycles
 /// the charger - the opposite of what the feature is for.
 /// </summary>
 public const int MinimumGapPercent = 5;

 public static SmartPlugConfig Disabled() => new(false, 95, 15, "", "", 6668, 1, 3);

 public string? ValidationError()
 {
 if (string.IsNullOrWhiteSpace(DeviceId)) return "Device ID is required.";
 if (string.IsNullOrWhiteSpace(Host)) return "Device IP address is required.";
 if (Port <= 0 || Port > 65535) return "Port must be between 1 and 65535.";
 if (LowThreshold < 0 || HighThreshold > 100) return "Thresholds must be within 0-100.";
 if (LowThreshold >= HighThreshold) return "Low threshold must be below high threshold.";
 if (HighThreshold - LowThreshold < MinimumGapPercent)
  return "Thresholds must be at least " + MinimumGapPercent + " points apart.";
 return null;
 }
 }

 /// <summary>
 /// Pure decision logic. Given the current battery state and the actual
 /// relay state, returns the action the caller should take. Contains no
 /// network or platform code, so it is fully unit-testable.
 /// </summary>
 public static class SmartPlugDecision
 {
 public enum Action { None, TurnOn, TurnOff }

 public sealed record Decision(Action Action, string Reason);

 /// <summary>
 /// Decides what to do with the relay, given where the reading sits.
 /// </summary>
 /// <remarks>
 /// Level-based, not edge-based, on purpose: the plug must end up correct even
 /// if the app missed the moment a level was crossed - while it was starting,
 /// or asleep. An alert wants the edge; a switch wants the level.
 /// </remarks>
 public static Decision Evaluate(BatteryBand band, RelayState relay, SmartPlugConfig config)
 {
  if (!config.Enabled) return new Decision(Action.None, "Smart-plug control disabled.");
  if (band == BatteryBand.Unknown)
   return new Decision(Action.None, "Battery percentage unknown; no action.");

  // The relay state decides whether a command is needed, never whether one is
  // allowed: when the plug cannot be read the state is unknown, and both
  // commands are idempotent, so acting is safe.
  if (band == BatteryBand.High)
  {
   return relay == RelayState.Off
    ? new Decision(Action.None, "Battery at or above the high level, plug already OFF.")
    : new Decision(Action.TurnOff, "Battery at or above the high level; turn plug OFF.");
  }

  if (band == BatteryBand.Low)
  {
   return relay == RelayState.On
    ? new Decision(Action.None, "Battery at or below the low level, plug already ON.")
    : new Decision(Action.TurnOn, "Battery at or below the low level; turn plug ON.");
  }

  return new Decision(Action.None, "Within the levels; retain the current relay state.");
 }
 }
}
