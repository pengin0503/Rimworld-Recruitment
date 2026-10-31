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
        public void EnvoyProbabilitiesFixZeroChanceAndSplitRestByWeights()
        {
            float[] p = ApplicantRoll.WithZeroChance(new[] { 9f, 1f, 1f, 2f }, 0.2f);
            Assert.Equal(0.2f, p[0], 5);
            Assert.Equal(0.2f, p[1], 5);
            Assert.Equal(0.2f, p[2], 5);
            Assert.Equal(0.4f, p[3], 5);
        }

        [Fact]
        public void EnvoyProbabilitiesWithNoPositiveWeightsGiveEverythingToZero()
        {
            float[] p = ApplicantRoll.WithZeroChance(new[] { 1f, 0f, 0f, 0f }, 0.2f);
            Assert.Equal(new[] { 1f, 0f, 0f, 0f }, p);
        }

        [Fact]
        public void EnvoyZeroChanceIsClamped()
        {
            Assert.Equal(1f, ApplicantRoll.WithZeroChance(Even, 1.5f)[0], 5);
            Assert.Equal(0f, ApplicantRoll.WithZeroChance(Even, -0.5f)[0], 5);
        }
    }
}
