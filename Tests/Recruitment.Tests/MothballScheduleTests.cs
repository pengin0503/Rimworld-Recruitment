using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class MothballScheduleTests
    {
        [Fact]
        public void NothingIsDueBeforeAFullInterval()
        {
            Assert.Equal(0, MothballSchedule.DueTicks(1000, 1000 + 14999, 15000));
        }

        [Fact]
        public void WholeIntervalsAreDue()
        {
            Assert.Equal(15000, MothballSchedule.DueTicks(0, 15000, 15000));
            Assert.Equal(30000, MothballSchedule.DueTicks(0, 44999, 15000));
        }

        [Fact]
        public void RemainderIsTheRest()
        {
            Assert.Equal(1234, MothballSchedule.RemainderTicks(100, 1334));
            Assert.Equal(0, MothballSchedule.RemainderTicks(500, 400));
        }

        [Fact]
        public void BadIntervalMeansNothingDue()
        {
            Assert.Equal(0, MothballSchedule.DueTicks(0, 100000, 0));
        }
    }
}
