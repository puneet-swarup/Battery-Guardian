namespace BatteryGuardian
{
    public class Settings
    {
        public int HighBatteryThreshold { get; set; } = 95;
        public int LowBatteryThreshold { get; set; } = 15;
        public int AlertRepeatIntervalSeconds { get; set; } = 300;
    }
}