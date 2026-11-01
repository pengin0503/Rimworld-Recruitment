using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class IdeologySelectionTests
    {
        [Fact]
        public void MajorityBeatsPrimaryIdeology()
        {
            object primary = new object();
            object majority = new object();
            Assert.Same(majority, IdeologySelection.MostCommonOrDefault(new[] { primary, majority, majority }, primary));
        }

        [Fact]
        public void PrimaryIdeologyWinsAnEqualCount()
        {
            object primary = new object();
            object other = new object();
            Assert.Same(primary, IdeologySelection.MostCommonOrDefault(new[] { other, primary }, primary));
        }

        [Fact]
        public void EmptyColonyFallsBackToPrimaryIdeology()
        {
            object primary = new object();
            Assert.Same(primary, IdeologySelection.MostCommonOrDefault(new object[0], primary));
        }

        [Fact]
        public void MissingIdeologiesAreIgnored()
        {
            object primary = new object();
            object other = new object();
            Assert.Same(other, IdeologySelection.MostCommonOrDefault(new[] { null, other, other, null }, primary));
        }

        [Fact]
        public void NoEligibleIdeologyReturnsNull()
        {
            Assert.Null(IdeologySelection.MostCommonOrDefault(new object[] { null }, null));
        }
    }
}
