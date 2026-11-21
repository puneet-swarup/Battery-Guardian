using System;
using System.Collections.Generic;
using System.Linq;

namespace BatteryGuardian.ChargeLimit
{
    /// <summary>
    /// Discovers a usable <see cref="IChargeLimitController"/> for the current
    /// machine by probing each registered vendor controller in turn.
    ///
    /// Adding support for a new vendor requires only registering a new
    /// controller instance here — no existing class is modified (Open/Closed).
    /// </summary>
    public sealed class ChargeLimitControllerFactory
    {
        private readonly IReadOnlyList<IChargeLimitController> _controllers;

        public ChargeLimitControllerFactory()
            : this(DefaultControllers())
        {
        }

        /// <summary>Test/DI constructor allowing injection of fake controllers.</summary>
        public ChargeLimitControllerFactory(IEnumerable<IChargeLimitController> controllers)
        {
            _controllers = (controllers ?? throw new ArgumentNullException(nameof(controllers)))
                .ToList();
        }

        private static IEnumerable<IChargeLimitController> DefaultControllers()
        {
            // Order matters: the first available controller wins.
            yield return new LenovoChargeLimitController();
            yield return new DellChargeLimitController();
            yield return new AsusChargeLimitController();
        }

        /// <summary>
        /// Returns the first controller that reports itself available, or null if
        /// none is supported on this machine.
        /// </summary>
        public IChargeLimitController? GetAvailableController()
        {
            foreach (var controller in _controllers)
            {
                try
                {
                    if (controller.IsAvailable()) return controller;
                }
                catch
                {
                    // A misbehaving vendor probe must not block discovery.
                }
            }
            return null;
        }

        /// <summary>Describes support status for display in the UI.</summary>
        public ChargeLimitCapability Describe()
        {
            var controller = GetAvailableController();
            if (controller == null)
            {
                return ChargeLimitCapability.Unsupported(
                    "No supported battery charge-limit interface was detected on this machine.");
            }

            return new ChargeLimitCapability
            {
                IsSupported = true,
                VendorName = controller.VendorName,
                CurrentLimitPercent = SafeRead(controller)
            };
        }

        private static int? SafeRead(IChargeLimitController controller)
        {
            try
            {
                return controller.GetCurrentLimitPercent();
            }
            catch
            {
                return null;
            }
        }
    }
}
