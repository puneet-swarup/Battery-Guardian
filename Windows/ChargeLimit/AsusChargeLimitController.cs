using System;
using System.Management;

namespace BatteryGuardian.ChargeLimit
{
    /// <summary>
    /// Charge-limit controller for ASUS laptops exposing the
    /// <c>AsusAtkWmi_WMNB</c> WMI interface (installed by ASUS ATK / Armoury Crate).
    /// The interface exposes a charge-threshold value in the 0-100 range.
    /// </summary>
    public sealed class AsusChargeLimitController : ChargeLimitControllerBase
    {
        private const string Namespace = @"root\WMI";
        private const string ClassName = "AsusAtkWmi_WMNB";

        public override string VendorName => "ASUS ATK";

        public override bool IsAvailable()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    Namespace, $"SELECT * FROM {ClassName}");
                foreach (var _ in searcher.Get())
                {
                    return true;
                }
            }
            catch
            {
                // Interface not present.
            }
            return false;
        }

        public override int? GetCurrentLimitPercent()
        {
            // ASUS does not expose a simple readback for the charge threshold
            // across all models, so we report unknown rather than guess.
            return null;
        }

        protected override ChargeLimitResult ApplyLimit(int percent)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    Namespace, $"SELECT * FROM {ClassName}");
                foreach (ManagementObject obj in searcher.Get())
                {
                    using (obj)
                    {
                        using var inParams = obj.GetMethodParameters("DEVS");
                        inParams["Device_ID"] = 0x00120057;
                        inParams["Control_status"] = (uint)percent;
                        obj.InvokeMethod("DEVS", inParams, null);
                        return ChargeLimitResult.Ok(
                            $"ASUS charge limit set to {percent}%.");
                    }
                }
            }
            catch (Exception ex)
            {
                return ChargeLimitResult.Fail($"ASUS WMI call failed: {ex.Message}");
            }

            return ChargeLimitResult.Fail("ASUS ATK interface did not respond.");
        }
    }
}
