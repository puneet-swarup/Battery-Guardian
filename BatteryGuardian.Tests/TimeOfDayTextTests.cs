using BatteryGuardian.QuietHours;
using Xunit;

namespace BatteryGuardian.Tests
{
    /// <summary>Tests for parsing/formatting the quiet-hours time text.</summary>
    public class TimeOfDayTextTests
    {
        [Theory]
        [InlineData(0, "00:00")]
        [InlineData(7 * 60, "07:00")]
        [InlineData(22 * 60, "22:00")]
        [InlineData(13 * 60 + 45, "13:45")]
        [InlineData(23 * 60 + 59, "23:59")]
        public void Format_ProducesZeroPaddedHHmm(int minutes, string expected)
        {
            Assert.Equal(expected, TimeOfDayText.Format(minutes));
        }

        [Fact]
        public void Format_WrapsValuesBeyondADay()
        {
            Assert.Equal("01:00", TimeOfDayText.Format(25 * 60));
        }

        [Fact]
        public void Format_NegativeWrapsToPreviousDay()
        {
            Assert.Equal("23:00", TimeOfDayText.Format(-60));
        }

        [Theory]
        [InlineData("07:00", 420)]
        [InlineData("7:00", 420)]
        [InlineData("7", 420)]
        [InlineData("00:00", 0)]
        [InlineData("23:59", 1439)]
        public void TryParse_ValidInputs_Succeed(string input, int expectedMinutes)
        {
            Assert.True(TimeOfDayText.TryParse(input, out int minutes));
            Assert.Equal(expectedMinutes, minutes);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("24:00")]
        [InlineData("12:60")]
        [InlineData("-1")]
        [InlineData("abc")]
        [InlineData("12:xx")]
        public void TryParse_InvalidInputs_Fail(string? input)
        {
            Assert.False(TimeOfDayText.TryParse(input, out _));
        }

        [Fact]
        public void RoundTrip_FormatThenParse_IsStable()
        {
            int original = 21 * 60 + 15;
            var text = TimeOfDayText.Format(original);
            Assert.True(TimeOfDayText.TryParse(text, out int parsed));
            Assert.Equal(original, parsed);
        }
    }
}
