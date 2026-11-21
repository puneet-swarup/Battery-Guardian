using System;
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
 public enum Action { None, TurnOn, TurnOff, Query }

 public sealed record Decision(Action Action, string Reason);

 public static Decision Evaluate(int batteryPercent, bool batteryKnown, RelayState relay, SmartPlugConfig config)
 {
 if (!config.Enabled) return new Decision(Action.None, "Smart-plug control disabled.");
 if (!batteryKnown || batteryPercent < 0 || batteryPercent > 100)
 return new Decision(Action.None, "Battery percentage unknown; no action.");
 if (relay == RelayState.Unknown)
 return new Decision(Action.Query, "Relay state unknown; query before acting.");

 if (batteryPercent >= config.HighThreshold && relay == RelayState.On)
 return new Decision(Action.TurnOff, "Battery >= high threshold; turn plug OFF.");
 if (batteryPercent <= config.LowThreshold && relay == RelayState.Off)
 return new Decision(Action.TurnOn, "Battery <= low threshold; turn plug ON.");
 return new Decision(Action.None, "Within thresholds; retain current relay state.");
 }
 }
}
