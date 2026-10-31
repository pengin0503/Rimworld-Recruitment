using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class EnvoySocialCurveTests
    {
        private static readonly EnvoySocialCurve Default = EnvoySocialCurve.Default;

        [Theory]
        [InlineData(0, 1.2f)]
        [InlineData(10, 1.0f)]
        [InlineData(20, 0.8f)]
        public void DurationFactorFollowsSpec(int social, float expected)
        {
            Assert.Equal(expected, Default.DurationFactor(social), 5);
        }

        [Theory]
        [InlineData(0, 0.35f)]
        [InlineData(10, 0.20f)]
        [InlineData(20, 0.05f)]
        public void ZeroChanceFollowsSpec(int social, float expected)
        {
            Assert.Equal(expected, Default.ZeroChance(social), 5);
        }

        [Fact]
        public void DurationFactorHasFloor()
        {
            var steep = new EnvoySocialCurve(1.2f, 0.5f, 0.35f, 0.015f);
            Assert.Equal(EnvoySocialCurve.MinDurationFactor, steep.DurationFactor(20), 5);
        }

        [Fact]
        public void ZeroChanceIsClampedToUnitRange()
        {
            var steep = new EnvoySocialCurve(1.2f, 0.02f, 0.35f, 0.1f);
            Assert.Equal(0f, steep.ZeroChance(20), 5);
            var negative = new EnvoySocialCurve(1.2f, 0.02f, 1.5f, -0.1f);
            Assert.Equal(1f, negative.ZeroChance(0), 5);
        }

        [Fact]
        public void SocialOutsideSkillRangeIsClamped()
        {
            Assert.Equal(Default.DurationFactor(20), Default.DurationFactor(99), 5);
            Assert.Equal(Default.ZeroChance(0), Default.ZeroChance(-3), 5);
        }
    }
}
