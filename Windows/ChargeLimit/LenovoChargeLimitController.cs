using System;
using System.Management;

namespace BatteryGuardian.ChargeLimit
{
    /// <summary>
    /// Charge-limit controller for Lenovo laptops that expose the
    /// <c>Lenovo_Battery</c> / <c>Lenovo_ChargingControl</c> WMI interface
    /// (installed by Lenovo Vantage / the Lenovo Power Management driver).
    ///
    /// On machines without that interface, <see cref="IsAvailable"/> returns false
    /// and the feature is hidden in the UI. All WMI access is wrapped so a missing
    /// driver never throws into the caller.
    /// </summary>
    public sealed class LenovoChargeLimitController : ChargeLimitControllerBase
    {
        private const string Namespace = @"root\WMI";
        private const string ClassName = "Lenovo_ChargingControl";

        public override string VendorName => "Lenovo Vantage";

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
                // Class not present / access denied / WMI unavailable.
            }
            return false;
        }

        public override int? GetCurrentLimitPercent()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    Namespace, $"SELECT * FROM {ClassName}");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var value = obj["ChargeStopThreshold"];
                    if (value != null) return Convert.ToInt32(value);
                }
            }
            catch
            {
                // Best-effort read.
            }
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
                    using var instance = obj;
                    var inParams = instance.GetMethodParameters("SetChargeStopThreshold");
                    inParams["Threshold"] = percent;
                    instance.InvokeMethod("SetChargeStopThreshold", inParams, null);
                    return ChargeLimitResult.Ok(
                        $"Lenovo charge limit set to {percent}%. Note: applies while the laptop is on AC power.");
                }
            }
            catch (Exception ex)
            {
                return ChargeLimitResult.Fail($"Lenovo WMI call failed: {ex.Message}");
            }

            return ChargeLimitResult.Fail("Lenovo charging-control interface did not respond.");
        }
    }
}
