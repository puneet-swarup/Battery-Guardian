using System.Collections.Generic;
using BatteryGuardian.ChargeLimit;
using Xunit;

namespace BatteryGuardian.Tests
{
    /// <summary>
    /// Tests for charge-limit validation, factory discovery and preset mapping.
    /// Uses a fake controller so no real WMI/vendor hardware is touched.
    /// </summary>
    public class ChargeLimitControllerTests
    {
        private sealed class FakeController : ChargeLimitControllerBase
        {
            private readonly bool _available;
            public int? CurrentLimit { get; set; }
            public int? LastApplied { get; private set; }
            public bool ApplyThrows { get; set; }

            public FakeController(string vendor, bool available)
            {
                VendorName = vendor;
                _available = available;
            }

            public override string VendorName { get; }

            public override bool IsAvailable() => _available;

            public override int? GetCurrentLimitPercent() => CurrentLimit;

            protected override ChargeLimitResult ApplyLimit(int percent)
            {
                if (ApplyThrows) throw new System.InvalidOperationException("vendor blew up");
                LastApplied = percent;
                return ChargeLimitResult.Ok($"applied {percent}");
            }
        }

        // ---- validation (base class) ----

        [Theory]
        [InlineData(39)]
        [InlineData(0)]
        [InlineData(-10)]
        [InlineData(101)]
        [InlineData(200)]
        public void SetLimitPercent_OutOfRange_Fails(int percent)
        {
            var controller = new FakeController("Fake", true);
            var result = controller.SetLimitPercent(percent);

            Assert.False(result.Success);
            Assert.Null(controller.LastApplied);
        }

        [Theory]
        [InlineData(40)]
        [InlineData(60)]
        [InlineData(80)]
        [InlineData(100)]
        public void SetLimitPercent_InRange_Succeeds(int percent)
        {
            var controller = new FakeController("Fake", true);
            var result = controller.SetLimitPercent(percent);

            Assert.True(result.Success);
            Assert.Equal(percent, controller.LastApplied);
        }

        [Fact]
        public void SetLimitPercent_WhenUnavailable_Fails()
        {
            var controller = new FakeController("Fake", false);
            var result = controller.SetLimitPercent(80);

            Assert.False(result.Success);
            Assert.Null(controller.LastApplied);
        }

        [Fact]
        public void SetLimitPercent_WhenApplyThrows_ReturnsFailureNotException()
        {
            var controller = new FakeController("Fake", true) { ApplyThrows = true };
            var result = controller.SetLimitPercent(80);

            Assert.False(result.Success);
            Assert.Contains("Failed", result.Message);
        }

        // ---- factory ----

        [Fact]
        public void Factory_NullControllers_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new ChargeLimitControllerFactory(null!));
        }

        [Fact]
        public void Factory_NoAvailableControllers_ReturnsNull()
        {
            var factory = new ChargeLimitControllerFactory(new List<IChargeLimitController>
            {
                new FakeController("A", false),
                new FakeController("B", false)
            });

            Assert.Null(factory.GetAvailableController());
        }

        [Fact]
        public void Factory_ReturnsFirstAvailableController()
        {
            var factory = new ChargeLimitControllerFactory(new List<IChargeLimitController>
            {
                new FakeController("A", false),
                new FakeController("B", true),
                new FakeController("C", true)
            });

            Assert.Equal("B", factory.GetAvailableController()!.VendorName);
        }

        [Fact]
        public void Factory_Describe_Unsupported_WhenNoneAvailable()
        {
            var factory = new ChargeLimitControllerFactory(new List<IChargeLimitController>
            {
                new FakeController("A", false)
            });

            var capability = factory.Describe();

            Assert.False(capability.IsSupported);
            Assert.False(string.IsNullOrWhiteSpace(capability.UnsupportedReason));
        }

        [Fact]
        public void Factory_Describe_Supported_ReportsVendorAndCurrentLimit()
        {
            var controller = new FakeController("Lenovo Vantage", true) { CurrentLimit = 80 };
            var factory = new ChargeLimitControllerFactory(new List<IChargeLimitController> { controller });

            var capability = factory.Describe();

            Assert.True(capability.IsSupported);
            Assert.Equal("Lenovo Vantage", capability.VendorName);
            Assert.Equal(80, capability.CurrentLimitPercent);
        }

        // ---- Dell preset mapping ----

        [Theory]
        [InlineData(100, "Standard")]
        [InlineData(95, "Adaptive")]
        [InlineData(80, "Primarily AC Use")]
        [InlineData(60, "Custom")]
        public void Dell_MapPercentToPreset(int percent, string expected)
        {
            Assert.Equal(expected, DellChargeLimitController.MapPercentToPreset(percent));
        }

        [Theory]
        [InlineData("Standard", 100)]
        [InlineData("standard", 100)]
        [InlineData("Adaptive", 90)]
        [InlineData("Primarily AC Use", 80)]
        public void Dell_MapPresetToPercent(string preset, int expected)
        {
            Assert.Equal(expected, DellChargeLimitController.MapPresetToPercent(preset));
        }

        [Theory]
        [InlineData("Custom")]
        [InlineData("")]
        [InlineData(null)]
        public void Dell_MapPresetToPercent_Unknown_ReturnsNull(string? preset)
        {
            Assert.Null(DellChargeLimitController.MapPresetToPercent(preset!));
        }
    }
}
