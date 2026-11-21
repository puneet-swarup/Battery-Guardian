using System;
using System.Management;

namespace BatteryGuardian
{
    /// <summary>
    /// Queries WMI for the laptop's battery design capacity and current full-charge capacity.
    /// Returns null if the information is unavailable (common on desktops and some laptops).
    /// </summary>
    public class BatteryHealthService
    {
        public BatteryHealthInfo? GetBatteryHealth()
        {
            try
            {
                uint designCapacity = QueryDesignCapacity();
                uint fullChargeCapacity = QueryFullChargeCapacity();

                if (designCapacity == 0 || fullChargeCapacity == 0)
                    return null;

                return new BatteryHealthInfo
                {
                    DesignCapacityMwh = designCapacity,
                    FullChargeCapacityMwh = fullChargeCapacity
                };
            }
            catch
            {
                // WMI is unreliable across hardware — fail gracefully.
                return null;
            }
        }

        private uint QueryDesignCapacity()
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT DesignedCapacity FROM BatteryStaticData");

            foreach (ManagementObject obj in searcher.Get())
            {
                var value = obj["DesignedCapacity"];
                if (value != null) return Convert.ToUInt32(value);
            }
            return 0;
        }

        private uint QueryFullChargeCapacity()
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity");

            foreach (ManagementObject obj in searcher.Get())
            {
                var value = obj["FullChargedCapacity"];
                if (value != null) return Convert.ToUInt32(value);
            }
            return 0;
        }
    }
}