using System;
using BatteryGuardian.QuietHours;
using Xunit;

namespace BatteryGuardian.Tests
{
    /// <summary>
    /// Tests for the quiet-hours orchestration using injected fakes — no OS access,
    /// fully deterministic.
    /// </summary>
    public class QuietHoursEvaluatorTests
    {
        private sealed class FakeScheduleProvider : IQuietHoursProvider
        {
            public QuietHoursSchedule Schedule { get; set; } = QuietHoursSchedule.Disabled;
            public QuietHoursSchedule GetSchedule() => Schedule;
        }

        private sealed class FakeDndDetector : IDoNotDisturbDetector
        {
            public bool Active { get; set; }
            public bool ThrowOnRead { get; set; }
            public bool IsDoNotDisturbActive()
            {
                if (ThrowOnRead) throw new InvalidOperationException("boom");
                return Active;
            }
        }

        private sealed class FakeClock : IClock
        {
            public DateTime NowLocal { get; set; } = new(2026, 1, 1, 12, 0, 0);
        }

        private static (QuietHoursEvaluator eval, FakeScheduleProvider sched, FakeDndDetector dnd, FakeClock clock) Build()
        {
            var sched = new FakeScheduleProvider();
            var dnd = new FakeDndDetector();
            var clock = new FakeClock();
            return (new QuietHoursEvaluator(sched, dnd, clock), sched, dnd, clock);
        }

        [Fact]
        public void Constructor_NullDependencies_Throw()
        {
            var sched = new FakeScheduleProvider();
            var dnd = new FakeDndDetector();
            var clock = new FakeClock();

            Assert.Throws<ArgumentNullException>(() => new QuietHoursEvaluator(null!, dnd, clock));
            Assert.Throws<ArgumentNullException>(() => new QuietHoursEvaluator(sched, null!, clock));
            Assert.Throws<ArgumentNullException>(() => new QuietHoursEvaluator(sched, dnd, null!));
        }

        [Fact]
        public void IsAudibleAlertSuppressed_NoQuietNoDnd_IsFalse()
        {
            var (eval, _, _, _) = Build();
            Assert.False(eval.IsAudibleAlertSuppressed());
        }

        [Fact]
        public void IsAudibleAlertSuppressed_WithinQuietHours_IsTrue()
        {
            var (eval, sched, _, clock) = Build();
            sched.Schedule = new QuietHoursSchedule(true, TimeSpan.FromHours(22), TimeSpan.FromHours(7));
            clock.NowLocal = new DateTime(2026, 1, 1, 23, 0, 0);

            Assert.True(eval.IsAudibleAlertSuppressed());
        }

        [Fact]
        public void IsAudibleAlertSuppressed_DndActive_IsTrue()
        {
            var (eval, _, dnd, _) = Build();
            dnd.Active = true;

            Assert.True(eval.IsAudibleAlertSuppressed());
        }

        [Fact]
        public void IsAudibleAlertSuppressed_DndThrows_FailsSafeToNotSuppressed()
        {
            var (eval, _, dnd, _) = Build();
            dnd.ThrowOnRead = true;

            Assert.False(eval.IsAudibleAlertSuppressed());
        }

        [Fact]
        public void IsWithinQuietHours_IgnoresDnd()
        {
            var (eval, _, dnd, _) = Build();
            dnd.Active = true;

            Assert.False(eval.IsWithinQuietHours());
        }

        [Fact]
        public void IsWithinQuietHours_OutsideWindow_IsFalse()
        {
            var (eval, sched, _, clock) = Build();
            sched.Schedule = new QuietHoursSchedule(true, TimeSpan.FromHours(22), TimeSpan.FromHours(7));
            clock.NowLocal = new DateTime(2026, 1, 1, 12, 0, 0);

            Assert.False(eval.IsWithinQuietHours());
        }

        [Fact]
        public void NullScheduleFromProvider_IsTreatedAsDisabled()
        {
            var (eval, sched, _, _) = Build();
            sched.Schedule = null!;

            Assert.False(eval.IsWithinQuietHours());
        }
    }
}
