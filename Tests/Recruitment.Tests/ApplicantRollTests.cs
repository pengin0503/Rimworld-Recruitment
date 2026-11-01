using System;
using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class ApplicantRollTests
    {
        private static readonly float[] Even = { 1f, 1f, 1f, 1f };

        [Theory]
        [InlineData(0.00, 0)]
        [InlineData(0.24, 0)]
        [InlineData(0.25, 1)]
        [InlineData(0.49, 1)]
        [InlineData(0.50, 2)]
        [InlineData(0.74, 2)]
        [InlineData(0.75, 3)]
        [InlineData(0.999999, 3)]
        public void EvenWeightsSplitIntoQuarters(double roll, int expected)
        {
            Assert.Equal(expected, ApplicantRoll.Roll(Even, roll));
        }

        [Fact]
        public void ZeroWeightOutcomeIsNeverRolled()
        {
            float[] weights = { 0f, 1f, 0f, 1f };
            for (double roll = 0; roll < 1; roll += 0.01)
            {
                int count = ApplicantRoll.Roll(weights, roll);
                Assert.True(count == 1 || count == 3, $"roll {roll} gave {count}");
            }
        }

        [Fact]
        public void AllZeroWeightsMeanNoApplicants()
        {
            Assert.Equal(0, ApplicantRoll.Roll(new float[4], 0.7));
        }

        [Fact]
        public void NegativeWeightsAreTreatedAsZero()
        {
            float[] weights = { -5f, 0f, 0f, 1f };
            Assert.Equal(3, ApplicantRoll.Roll(weights, 0.0));
        }

        [Fact]
        public void RollOfOneIsClampedToLastOutcome()
        {
            Assert.Equal(3, ApplicantRoll.Roll(Even, 1.0));
        }

        [Fact]
        public void WrongWeightCountThrows()
        {
            Assert.Throws<ArgumentException>(() => ApplicantRoll.Roll(new float[] { 1f, 1f }, 0.5));
        }

        [Fact]
        public void SocialChancesCanBeRolledDirectly()
        {
            // RollApplicantCount feeds SocialWeights.Chances (summing to 1) into Roll.
            float[] chances = SocialWeights.Chances(new[] { 25f, 25f, 25f, 25f }, new[] { 5f, 15f, 50f, 30f }, 20);
            Assert.Equal(0, ApplicantRoll.Roll(chances, 0.04));
            Assert.Equal(1, ApplicantRoll.Roll(chances, 0.06));
            Assert.Equal(2, ApplicantRoll.Roll(chances, 0.69));
            Assert.Equal(3, ApplicantRoll.Roll(chances, 0.71));
        }
    }
}
