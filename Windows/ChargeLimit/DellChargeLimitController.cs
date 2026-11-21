using System;
using System.Management;

namespace BatteryGuardian.ChargeLimit
{
    /// <summary>
    /// Charge-limit controller for Dell laptops exposing the
    /// <c>DCIM_BIOSEnumeration</c> WMI interface with the
    /// <c>PrimaryBattChargeCfg</c> attribute (Dell Command | Configure / BIOS).
    ///
    /// Dell exposes named presets rather than a raw percentage, so the requested
    /// percentage is mapped to the nearest supported preset value.
    /// </summary>
    public sealed class DellChargeLimitController : ChargeLimitControllerBase
    {
        private const string Namespace = @"root\dcim\sysman\biosattributes";
        private const string AttributeName = "PrimaryBattChargeCfg";

        public override string VendorName => "Dell BIOS";

        public override bool IsAvailable()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    Namespace, "SELECT * FROM DCIM_BIOSEnumeration");
                foreach (ManagementObject obj in searcher.Get())
                {
                    using (obj)
                    {
                        var name = obj["AttributeName"]?.ToString();
                        if (string.Equals(name, AttributeName, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch
            {
                // BIOS attribute namespace not present on this machine.
            }
            return false;
        }

        public override int? GetCurrentLimitPercent()
        {
            // Dell reports a preset name, not a number. We surface the mapped
            // percentage when the current value matches a known preset.
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    Namespace, "SELECT * FROM DCIM_BIOSEnumeration");
                foreach (ManagementObject obj in searcher.Get())
                {
                    using (obj)
                    {
                        var name = obj["AttributeName"]?.ToString();
                        if (!string.Equals(name, AttributeName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var current = obj["CurrentValue"]?.ToString() ?? "";
                        return MapPresetToPercent(current);
                    }
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
            var preset = MapPercentToPreset(percent);
            if (preset == null)
            {
                return ChargeLimitResult.Fail(
                    "Dell BIOS only supports preset charge limits (e.g. 80%). " +
                    $"Requested {percent}% has no matching preset.");
            }

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    Namespace, "SELECT * FROM DCIM_BIOSEnumeration");
                foreach (ManagementObject obj in searcher.Get())
                {
                    using (obj)
                    {
                        var name = obj["AttributeName"]?.ToString();
                        if (!string.Equals(name, AttributeName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        using var inParams = obj.GetMethodParameters("SetAttribute");
                        inParams["AttributeName"] = AttributeName;
                        inParams["AttributeValue"] = preset;
                        obj.InvokeMethod("SetAttribute", inParams, null);

                        return ChargeLimitResult.Ok(
                            $"Dell charge limit set to the \"{preset}\" preset ({percent}%).");
                    }
                }
            }
            catch (Exception ex)
            {
                return ChargeLimitResult.Fail($"Dell BIOS call failed: {ex.Message}");
            }

            return ChargeLimitResult.Fail("Dell BIOS charge attribute did not respond.");
        }

        /// <summary>Maps a requested percentage to the nearest Dell preset name.</summary>
        public static string? MapPercentToPreset(int percent)
        {
            if (percent >= 100) return "Standard";
            if (percent >= 90) return "Adaptive";
            if (percent >= 75) return "Primarily AC Use";
            return "Custom";
        }

        /// <summary>Maps a Dell preset name back to its nominal percentage.</summary>
        public static int? MapPresetToPercent(string preset)
        {
            if (string.IsNullOrWhiteSpace(preset)) return null;
            return preset.Trim().ToLowerInvariant() switch
            {
                "standard" => 100,
                "adaptive" => 90,
                "primarily ac use" => 80,
                "custom" => null,
                _ => null
            };
        }
    }
}
