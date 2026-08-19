namespace BatteryGuardian
{
    public class Settings
    {
        public int HighBatteryThreshold { get; set; } = 95;
        public int LowBatteryThreshold { get; set; } = 15;
        public string HighAlertSoundPath { get; set; } = "";
        public string LowAlertSoundPath { get; set; } = "";
    }
}