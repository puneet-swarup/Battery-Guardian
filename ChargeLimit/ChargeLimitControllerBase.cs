using System;

namespace BatteryGuardian.ChargeLimit
{
    /// <summary>
    /// Base class for charge-limit controllers. Centralises input validation so
    /// every concrete controller enforces the same legal range and returns the
    /// same failure messages — a single place to change the rules.
    /// </summary>
    public abstract class ChargeLimitControllerBase : IChargeLimitController
    {
        public const int MinLimitPercent = 40;
        public const int MaxLimitPercent = 100;

        public abstract string VendorName { get; }

        public abstract bool IsAvailable();

        public abstract int? GetCurrentLimitPercent();

        protected abstract ChargeLimitResult ApplyLimit(int percent);

        /// <summary>
        /// Validates the requested percentage and delegates to the vendor-specific
        /// <see cref="ApplyLimit"/>. Concrete controllers cannot bypass validation.
        /// </summary>
        public ChargeLimitResult SetLimitPercent(int percent)
        {
            if (percent < MinLimitPercent || percent > MaxLimitPercent)
            {
                return ChargeLimitResult.Fail(
                    $"Charge limit must be between {MinLimitPercent}% and {MaxLimitPercent}%.");
            }

            try
            {
                if (!IsAvailable())
                    return ChargeLimitResult.Fail(
                        $"The {VendorName} charge-limit interface is not available on this machine.");

                return ApplyLimit(percent);
            }
            catch (Exception ex)
            {
                return ChargeLimitResult.Fail($"Failed to apply charge limit: {ex.Message}");
            }
        }
    }
}
