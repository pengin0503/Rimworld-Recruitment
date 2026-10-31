using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class DurationTests
    {
        [Theory]
        [InlineData(0.0, 4 * 60000)]
        [InlineData(1.0, 7 * 60000)]
        [InlineData(0.5, 330000)]
        public void RollIsUniformBetweenMinAndMax(double roll, int expected)
        {
            Assert.Equal(expected, RecruitmentDuration.RollTicks(4f, 7f, 1f, roll));
        }

        [Fact]
        public void FactorScalesDuration()
        {
            Assert.Equal(48000, RecruitmentDuration.RollTicks(1f, 1f, 0.8f, 0.3));
        }

        [Fact]
        public void SwappedBoundsAreTolerated()
        {
            Assert.Equal(RecruitmentDuration.RollTicks(2f, 5f, 1f, 0.25), RecruitmentDuration.RollTicks(5f, 2f, 1f, 0.25));
        }

        [Fact]
        public void DurationIsAtLeastOneHour()
        {
            Assert.Equal(RecruitmentDuration.MinTicks, RecruitmentDuration.RollTicks(0f, 0f, 1f, 0.5));
            Assert.Equal(RecruitmentDuration.MinTicks, RecruitmentDuration.RollTicks(-3f, -1f, 1f, 0.5));
        }
    }
}
