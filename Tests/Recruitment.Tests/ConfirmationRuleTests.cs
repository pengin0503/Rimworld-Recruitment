using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class ConfirmationRuleTests
    {
        [Theory]
        [InlineData(250, 200, true)]
        [InlineData(200, 200, true)]
        [InlineData(199, 200, false)]
        [InlineData(0, 0, true)]
        [InlineData(50, 0, true)]
        public void ConfirmsAtOrAboveThreshold(int cost, int threshold, bool expected)
        {
            Assert.Equal(expected, ConfirmationRule.ShouldConfirm(cost, threshold));
        }

        [Fact]
        public void NegativeThresholdDisablesConfirmation()
        {
            Assert.False(ConfirmationRule.ShouldConfirm(100000, -1));
        }
    }
}
