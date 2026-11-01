using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class DirectRequestRuleTests
    {
        [Fact]
        public void FriendlyFactionWithEnoughGoodwillCanBeAsked()
        {
            Assert.Equal(DirectRequestBlock.None, DirectRequestRule.Check(hostile: false, goodwill: 50, goodwillCost: 10, pending: false));
        }

        [Fact]
        public void GoodwillMayDropExactlyToZero()
        {
            Assert.Equal(DirectRequestBlock.None, DirectRequestRule.Check(false, 10, 10, false));
            Assert.Equal(DirectRequestBlock.Goodwill, DirectRequestRule.Check(false, 9, 10, false));
        }

        [Fact]
        public void NegativeGoodwillIsNeverEnough()
        {
            Assert.Equal(DirectRequestBlock.Goodwill, DirectRequestRule.Check(false, -5, 10, false));
        }

        [Fact]
        public void HostilityComesBeforeGoodwill()
        {
            Assert.Equal(DirectRequestBlock.Hostile, DirectRequestRule.Check(true, -80, 10, true));
        }

        [Fact]
        public void APendingRequestBlocksAnother()
        {
            Assert.Equal(DirectRequestBlock.Pending, DirectRequestRule.Check(false, 100, 10, true));
        }

        [Fact]
        public void GoodwillAfterRequest()
        {
            Assert.Equal(40, DirectRequestRule.GoodwillAfter(50, 10));
            Assert.Equal(-100, DirectRequestRule.GoodwillAfter(-95, 10));
        }
    }
}
