namespace BatteryGuardian.ChargeLimit
{
    /// <summary>
    /// Abstraction for a vendor-specific battery charge-limit control.
    ///
    /// Implementations are discovered at runtime (see
    /// <see cref="ChargeLimitControllerFactory"/>); the rest of the app depends
    /// only on this interface, honouring the Dependency Inversion Principle and
    /// the Open/Closed Principle (adding a new vendor means adding an
    /// implementation, not editing existing code).
    /// </summary>
    public interface IChargeLimitController
    {
        /// <summary>Vendor/interface name for display, e.g. "Lenovo Vantage".</summary>
        string VendorName { get; }

        /// <summary>
        /// Probes whether this controller is applicable on the current machine
        /// (correct vendor, required WMI class present, etc.).
        /// Must not throw.
        /// </summary>
        bool IsAvailable();

        /// <summary>
        /// Reads the currently configured charge limit, or null if unknown.
        /// Must not throw.
        /// </summary>
        int? GetCurrentLimitPercent();

        /// <summary>
        /// Attempts to set the charge limit. Implementations must validate the
        /// range and never throw — failures are reported via
        /// <see cref="ChargeLimitResult"/>.
        /// </summary>
        ChargeLimitResult SetLimitPercent(int percent);
    }
}
