namespace BatteryGuardian.ChargeLimit
{
    /// <summary>
    /// Describes whether and how the current machine supports setting a battery
    /// charge limit (a firmware-level cap on charging, e.g. 80%).
    /// </summary>
    public sealed class ChargeLimitCapability
    {
        /// <summary>True if a control interface was detected for this machine.</summary>
        public bool IsSupported { get; init; }

        /// <summary>Human-readable vendor/interface name, e.g. "Lenovo Vantage".</summary>
        public string VendorName { get; init; } = "";

        /// <summary>Current configured charge limit, if readable (0-100), else null.</summary>
        public int? CurrentLimitPercent { get; init; }

        /// <summary>Reason the feature is unavailable, for display in the UI.</summary>
        public string UnsupportedReason { get; init; } = "";

        public static ChargeLimitCapability Unsupported(string reason) =>
            new() { IsSupported = false, UnsupportedReason = reason };
    }

    /// <summary>The outcome of attempting to change the charge limit.</summary>
    public sealed class ChargeLimitResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";

        public static ChargeLimitResult Ok(string message = "Charge limit applied.") =>
            new() { Success = true, Message = message };

        public static ChargeLimitResult Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
