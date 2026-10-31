using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class CooldownRuleTests
    {
        [Fact]
        public void NeverRequestedIsReady()
        {
            Assert.Equal(0, CooldownRule.TicksRemaining(null, 1000, 60000));
        }

        [Fact]
        public void CountsDownFromTheLastRequest()
        {
            Assert.Equal(50000, CooldownRule.TicksRemaining(100, 10100, 60000));
            Assert.Equal(0, CooldownRule.TicksRemaining(100, 60100, 60000));
            Assert.Equal(0, CooldownRule.TicksRemaining(100, 999999, 60000));
        }

        [Fact]
        public void ZeroOrNegativeCooldownIsAlwaysReady()
        {
            Assert.Equal(0, CooldownRule.TicksRemaining(100, 100, 0));
            Assert.Equal(0, CooldownRule.TicksRemaining(100, 100, -5));
        }

        [Fact]
        public void ClockBeforeTheRequestDoesNotOverflow()
        {
            // A save edited or loaded oddly could have the request in the future; never wait longer than the cooldown.
            Assert.Equal(60000, CooldownRule.TicksRemaining(5000, 0, 60000));
        }
    }
}
