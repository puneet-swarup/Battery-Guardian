using System;

namespace BatteryGuardian
{
 /// <summary>
 /// Persisted user configuration for the Windows application.
 /// </summary>
 public class Settings
 {
 public int HighBatteryThreshold { get; set; } = 95;
 public int LowBatteryThreshold { get; set; } = 15;
 public int AlertRepeatIntervalSeconds { get; set; } = 300;
 public bool CheckForUpdatesAutomatically { get; set; } = true;
 public DateTime? LastUpdateCheckUtc { get; set; } = null;
 public bool DiagnosticLoggingEnabled { get; set; } = false;
 public DateTime? SnoozedUntilUtc { get; set; } = null;

 public bool HistoryEnabled { get; set; } = true;
 public int IdealZoneLowPercent { get; set; } = 20;
 public int IdealZoneHighPercent { get; set; } = 80;

 public bool QuietHoursEnabled { get; set; } = false;
 public int QuietHoursStartMinutes { get; set; } = 22 * 60;
 public int QuietHoursEndMinutes { get; set; } = 7 * 60;
 public bool RespectFocusAssist { get; set; } = true;

 public int LastAppliedChargeLimitPercent { get; set; } = 0;

 // ---- Smart plug (Tuya) ----
 public bool SmartPlugEnabled { get; set; } = false;
 public int SmartPlugHighThreshold { get; set; } = 95;
 public int SmartPlugLowThreshold { get; set; } = 15;
 public string SmartPlugDeviceId { get; set; } = string.Empty;
 public string SmartPlugHost { get; set; } = string.Empty;
 public int SmartPlugPort { get; set; } = 6668;
 public int SmartPlugRelayDp { get; set; } = 1;
 public string SmartPlugProtocolVersion { get; set; } = "3.5";

 /// <summary>Switch the plug off when the machine sleeps or shuts down.</summary>
 public bool SmartPlugOffWhenSleeping { get; set; } = true;

 /// <summary>
 /// Relay state recorded just before sleeping, so it can be put back on wake.
 /// "On", "Off", or empty when nothing was recorded.
 /// </summary>
 public string SmartPlugStateBeforeSleep { get; set; } = string.Empty;

 /// <summary>
 /// Copy of every persisted field. Use this rather than copying property by
 /// property: the settings dialog used to do that by hand and quietly dropped
 /// the smart-plug fields, so every save reset them to their defaults.
 /// </summary>
 public Settings Clone() => (Settings)MemberwiseClone();
 }
}
