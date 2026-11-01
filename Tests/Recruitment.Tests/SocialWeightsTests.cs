using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class SocialWeightsTests
    {
        private static readonly float[] AtZero = { 25f, 25f, 25f, 25f };

        private static readonly float[] AtMax = { 5f, 15f, 50f, 30f };

        private static void AssertChances(float[] expected, float[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], 4);
            }
        }

        [Fact]
        public void SocialZeroUsesTheFirstSet()
        {
            AssertChances(new[] { 0.25f, 0.25f, 0.25f, 0.25f }, SocialWeights.Chances(AtZero, AtMax, 0));
        }

        [Fact]
        public void SocialTenIsHalfway()
        {
            AssertChances(new[] { 0.15f, 0.20f, 0.375f, 0.275f }, SocialWeights.Chances(AtZero, AtMax, 10));
        }

        [Fact]
        public void SocialTwentyUsesTheSecondSet()
        {
            AssertChances(new[] { 0.05f, 0.15f, 0.50f, 0.30f }, SocialWeights.Chances(AtZero, AtMax, 20));
        }

        [Theory]
        [InlineData(-5, 0)]
        [InlineData(25, 20)]
        public void SkillOutsideTheRangeIsClamped(int social, int clampedTo)
        {
            AssertChances(SocialWeights.Chances(AtZero, AtMax, clampedTo), SocialWeights.Chances(AtZero, AtMax, social));
        }

        [Fact]
        public void EachEndIsNormalisedBeforeBlending()
        {
            // The same proportions at a different scale give the same chances.
            float[] small = { 1f, 1f, 1f, 1f };
            float[] large = { 10f, 30f, 100f, 60f };
            AssertChances(SocialWeights.Chances(AtZero, AtMax, 10), SocialWeights.Chances(small, large, 10));
        }

        [Fact]
        public void AllZeroEndMeansNobodyComes()
        {
            float[] none = { 0f, 0f, 0f, 0f };
            AssertChances(new[] { 1f, 0f, 0f, 0f }, SocialWeights.Chances(none, none, 7));
            AssertChances(new[] { 0.5f, 0.125f, 0.125f, 0.25f }, SocialWeights.Chances(none, new[] { 0f, 1f, 1f, 2f }, 10));
        }

        [Fact]
        public void NegativeWeightsCountAsZero()
        {
            AssertChances(new[] { 0f, 0f, 0f, 1f }, SocialWeights.Chances(new[] { -3f, 0f, 0f, 1f }, new[] { -3f, 0f, 0f, 1f }, 0));
        }

        [Fact]
        public void ChancesAlwaysSumToOne()
        {
            for (int social = 0; social <= 20; social++)
            {
                float[] chances = SocialWeights.Chances(AtZero, AtMax, social);
                Assert.Equal(1f, chances[0] + chances[1] + chances[2] + chances[3], 4);
            }
        }

        [Fact]
        public void WrongLengthIsRejected()
        {
            Assert.Throws<System.ArgumentException>(() => SocialWeights.Chances(new[] { 1f, 1f }, AtMax, 0));
        }
    }
}
