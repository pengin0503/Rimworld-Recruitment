using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class FactionEligibilityTests
    {
        private static readonly FactionTraits Outlanders = new FactionTraits
        {
            Humanlike = true,
        };

        [Fact]
        public void OrdinaryHumanlikeFactionIsEligible()
        {
            Assert.True(FactionEligibility.IsEligible(Outlanders, includePermanentEnemies: false));
        }

        [Fact]
        public void PlayerHiddenDefeatedDeactivatedTemporaryAndNonHumanlikeAreExcluded()
        {
            Assert.False(FactionEligibility.IsEligible(Outlanders with { IsPlayer = true }, true));
            Assert.False(FactionEligibility.IsEligible(Outlanders with { Hidden = true }, true));
            Assert.False(FactionEligibility.IsEligible(Outlanders with { Defeated = true }, true));
            Assert.False(FactionEligibility.IsEligible(Outlanders with { Deactivated = true }, true));
            Assert.False(FactionEligibility.IsEligible(Outlanders with { Temporary = true }, true));
            Assert.False(FactionEligibility.IsEligible(Outlanders with { Humanlike = false }, true));
        }

        [Fact]
        public void PermanentEnemiesDependOnSetting()
        {
            var pirates = Outlanders with { PermanentEnemy = true };
            Assert.False(FactionEligibility.IsEligible(pirates, includePermanentEnemies: false));
            Assert.True(FactionEligibility.IsEligible(pirates, includePermanentEnemies: true));
        }

        [Fact]
        public void PermitFactionsNeedThePermit()
        {
            var empire = Outlanders with { RequiresPermit = true };
            Assert.False(FactionEligibility.IsEligible(empire, false));
            Assert.True(FactionEligibility.IsEligible(empire with { PlayerHasPermit = true }, false));
        }
    }
}
