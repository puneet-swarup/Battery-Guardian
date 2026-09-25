namespace BatteryGuardian
{
    public class Settings
    {
        public int HighBatteryThreshold { get; set; } = 95;
        public int LowBatteryThreshold { get; set; } = 15;
        public int AlertRepeatIntervalSeconds { get; set; } = 300;
        public bool CheckForUpdatesAutomatically { get; set; } = true;
        public DateTime? LastUpdateCheckUtc { get; set; } = null;
        public bool DiagnosticLoggingEnabled { get; set; } = false;
    }
}