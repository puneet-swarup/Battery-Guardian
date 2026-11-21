using Xunit;

namespace BatteryGuardian.Tests
{
    public class BatteryHealthInfoTests
    {
        [Fact]
        public void HealthPercent_NewBattery_Returns100()
        {
            var info = new BatteryHealthInfo
            {
                DesignCapacityMwh = 50_000,
                FullChargeCapacityMwh = 50_000
            };

            Assert.Equal(100, info.HealthPercent);
            Assert.Equal("Good", info.HealthLabel);
        }

        [Fact]
        public void HealthPercent_WornBattery_ReturnsCorrectPercentage()
        {
            // 80% of original capacity
            var info = new BatteryHealthInfo
            {
                DesignCapacityMwh = 50_000,
                FullChargeCapacityMwh = 40_000
            };

            Assert.Equal(80, info.HealthPercent);
            Assert.Equal("Good", info.HealthLabel);
        }

        [Fact]
        public void HealthPercent_FairCondition_ReturnsFairLabel()
        {
            var info = new BatteryHealthInfo
            {
                DesignCapacityMwh = 50_000,
                FullChargeCapacityMwh = 35_000 // 70%
            };

            Assert.Equal(70, info.HealthPercent);
            Assert.Equal("Fair", info.HealthLabel);
        }

        [Fact]
        public void HealthPercent_PoorCondition_ReturnsPoorLabel()
        {
            var info = new BatteryHealthInfo
            {
                DesignCapacityMwh = 50_000,
                FullChargeCapacityMwh = 25_000 // 50%
            };

            Assert.Equal(50, info.HealthPercent);
            Assert.Equal("Poor", info.HealthLabel);
        }

        [Fact]
        public void HealthPercent_UnknownDesignCapacity_ReturnsMinusOne()
        {
            var info = new BatteryHealthInfo
            {
                DesignCapacityMwh = 0,
                FullChargeCapacityMwh = 40_000
            };

            Assert.Equal(-1, info.HealthPercent);
            Assert.Equal("Unavailable", info.HealthLabel);
        }

        [Fact]
        public void HealthPercent_UnknownFullChargeCapacity_ReturnsMinusOne()
        {
            var info = new BatteryHealthInfo
            {
                DesignCapacityMwh = 50_000,
                FullChargeCapacityMwh = 0
            };

            Assert.Equal(-1, info.HealthPercent);
            Assert.Equal("Unavailable", info.HealthLabel);
        }

        [Fact]
        public void HealthPercent_OverReportedCapacity_IsClampedTo100()
        {
            // Some laptops report FullCharge > Design (calibration error). Should not exceed 100%.
            var info = new BatteryHealthInfo
            {
                DesignCapacityMwh = 50_000,
                FullChargeCapacityMwh = 55_000
            };

            Assert.Equal(100, info.HealthPercent);
        }

        [Fact]
        public void HealthLabel_Exactly80Percent_ReturnsGood()
        {
            var info = new BatteryHealthInfo
            {
                DesignCapacityMwh = 50_000,
                FullChargeCapacityMwh = 40_000
            };

            Assert.Equal("Good", info.HealthLabel);
        }

        [Fact]
        public void HealthLabel_Exactly60Percent_ReturnsFair()
        {
            var info = new BatteryHealthInfo
            {
                DesignCapacityMwh = 50_000,
                FullChargeCapacityMwh = 30_000
            };

            Assert.Equal("Fair", info.HealthLabel);
        }

        [Fact]
        public void HealthLabel_JustBelow60Percent_ReturnsPoor()
        {
            var info = new BatteryHealthInfo
            {
                DesignCapacityMwh = 100_000,
                FullChargeCapacityMwh = 59_000 // 59%
            };

            Assert.Equal("Poor", info.HealthLabel);
        }
    }
}