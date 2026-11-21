using System;
using BatteryGuardian.QuietHours;
using Xunit;

namespace BatteryGuardian.Tests
{
    /// <summary>Tests for the pure quiet-hours window logic.</summary>
    public class QuietHoursScheduleTests
    {
        private static TimeSpan T(int h, int m = 0) => new(h, m, 0);

        [Fact]
        public void Disabled_Schedule_IsNeverQuiet()
        {
            var schedule = QuietHoursSchedule.Disabled;

            Assert.False(schedule.Enabled);
            Assert.False(schedule.IsQuietAt(T(23)));
            Assert.False(schedule.IsQuietAt(T(3)));
            Assert.False(schedule.IsQuietAt(T(12)));
        }

        [Fact]
        public void SpansMidnight_TrueWhenStartAfterEnd()
        {
            var schedule = new QuietHoursSchedule(true, T(22), T(7));
            Assert.True(schedule.SpansMidnight);
        }

        [Fact]
        public void SpansMidnight_FalseWhenStartBeforeEnd()
        {
            var schedule = new QuietHoursSchedule(true, T(13), T(14));
            Assert.False(schedule.SpansMidnight);
        }

        [Theory]
        [InlineData(22, 0, true)]
        [InlineData(23, 30, true)]
        [InlineData(0, 0, true)]
        [InlineData(3, 0, true)]
        [InlineData(6, 59, true)]
        [InlineData(7, 0, false)]   // end is exclusive
        [InlineData(12, 0, false)]
        [InlineData(21, 59, false)] // just before start
        public void MidnightSpanningWindow_ClassifiesCorrectly(int hour, int minute, bool expected)
        {
            var schedule = new QuietHoursSchedule(true, T(22), T(7));
            Assert.Equal(expected, schedule.IsQuietAt(T(hour, minute)));
        }

        [Theory]
        [InlineData(13, 0, true)]   // start inclusive
        [InlineData(13, 30, true)]
        [InlineData(14, 29, true)]
        [InlineData(14, 30, false)] // end exclusive
        [InlineData(12, 59, false)]
        [InlineData(15, 0, false)]
        public void SameDayWindow_ClassifiesCorrectly(int hour, int minute, bool expected)
        {
            var schedule = new QuietHoursSchedule(true, T(13), T(14, 30));
            Assert.Equal(expected, schedule.IsQuietAt(T(hour, minute)));
        }

        [Fact]
        public void StartEqualsEnd_DisabledByDefault_IsNeverQuiet()
        {
            var schedule = new QuietHoursSchedule(false, T(10), T(10));
            Assert.False(schedule.IsQuietAt(T(10)));
        }

        [Fact]
        public void Normalization_WrapsValuesBeyondADay()
        {
            // 25:00 should normalise to 01:00.
            var schedule = new QuietHoursSchedule(true, TimeSpan.FromHours(25), TimeSpan.FromHours(26));

            Assert.Equal(TimeSpan.FromHours(1), schedule.Start);
            Assert.Equal(TimeSpan.FromHours(2), schedule.End);
        }
    }
}
