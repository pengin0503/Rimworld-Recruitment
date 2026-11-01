using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class PremiumTierTests
    {
        [Theory]
        [InlineData(0, 0, 1)]
        [InlineData(1, 50, 2)]
        [InlineData(2, 100, 3)]
        [InlineData(3, 200, 4)]
        public void TiersMatchTheTable(int tier, int percent, int candidates)
        {
            Assert.Equal(percent, PremiumTier.ExtraPercent(tier));
            Assert.Equal(candidates, PremiumTier.Candidates(tier));
        }

        [Theory]
        [InlineData(250, 0, 0)]
        [InlineData(250, 1, 125)]
        [InlineData(250, 2, 250)]
        [InlineData(250, 3, 500)]
        [InlineData(55, 1, 28)] // 27.5 rounds up
        [InlineData(0, 3, 0)]
        public void ExtraCostIsAShareOfTheBaseCost(int baseCost, int tier, int extra)
        {
            Assert.Equal(extra, PremiumTier.ExtraCost(baseCost, tier));
        }

        [Fact]
        public void HugeCostsDoNotOverflow()
        {
            Assert.Equal(int.MaxValue, PremiumTier.ExtraCost(int.MaxValue, 3));
            Assert.Equal(int.MaxValue, PremiumTier.TotalCost(int.MaxValue, 1));
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(4, 3)]
        [InlineData(99, 3)]
        public void OutOfRangeTiersAreClamped(int tier, int clamped)
        {
            Assert.Equal(clamped, PremiumTier.Clamp(tier));
            Assert.Equal(PremiumTier.Candidates(clamped), PremiumTier.Candidates(tier));
        }

        [Fact]
        public void NegativeBaseCostCostsNothingExtra()
        {
            Assert.Equal(0, PremiumTier.ExtraCost(-10, 2));
            Assert.Equal(0, PremiumTier.TotalCost(-10, 2));
        }
    }
}
