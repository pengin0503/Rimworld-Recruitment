using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class DestinationPickerTests
    {
        private static MapCandidate Home(int id, int colonists, bool space = false) => new MapCandidate(id, true, space, colonists);

        [Fact]
        public void StartSkipsFullHomeButArrivalCanStillUseIt()
        {
            var maps = new[] { new MapCandidate(1, true, false, 9, hasCapacity: false), Home(2, 1) };
            Assert.Equal(2, DestinationPicker.Pick(maps, -1, true, false, requireCapacity: true));
            Assert.Equal(1, DestinationPicker.Pick(maps, 1, true, false));
        }

        [Fact]
        public void AllFullHomesRejectNewRequests()
        {
            var maps = new[] { new MapCandidate(1, true, false, 9, hasCapacity: false) };
            Assert.Null(DestinationPicker.Pick(maps, -1, true, false, requireCapacity: true));
        }

        [Fact]
        public void OriginalMapIsKeptWhenStillSuitable()
        {
            var maps = new[] { Home(1, 2), Home(2, 9) };
            Assert.Equal(1, DestinationPicker.Pick(maps, 1, allowSurface: true, allowSpace: false));
        }

        [Fact]
        public void LostOriginalMapRedirectsToMostPopulatedHome()
        {
            var maps = new[] { Home(2, 3), Home(3, 7), Home(4, 5) };
            Assert.Equal(3, DestinationPicker.Pick(maps, 1, allowSurface: true, allowSpace: false));
        }

        [Fact]
        public void NonHomeOriginalMapIsNotUsed()
        {
            var maps = new[] { new MapCandidate(1, false, false, 10), Home(2, 1) };
            Assert.Equal(2, DestinationPicker.Pick(maps, 1, allowSurface: true, allowSpace: false));
        }

        [Fact]
        public void WalkInMethodsSkipSpaceMaps()
        {
            var maps = new[] { Home(1, 5, space: true), Home(2, 1) };
            Assert.Equal(2, DestinationPicker.Pick(maps, 1, allowSurface: true, allowSpace: false));
        }

        [Fact]
        public void OrbitalMethodAcceptsSpaceMaps()
        {
            var maps = new[] { Home(1, 5, space: true), Home(2, 1) };
            Assert.Equal(1, DestinationPicker.Pick(maps, 1, allowSurface: true, allowSpace: true));
        }

        [Fact]
        public void NoSuitableMapGivesNull()
        {
            var maps = new[] { Home(1, 5, space: true), new MapCandidate(2, false, false, 3) };
            Assert.Null(DestinationPicker.Pick(maps, 1, allowSurface: true, allowSpace: false));
        }

        [Fact]
        public void TiesBreakOnLowestId()
        {
            var maps = new[] { Home(5, 4), Home(3, 4), Home(8, 4) };
            Assert.Equal(3, DestinationPicker.Pick(maps, 1, allowSurface: true, allowSpace: false));
        }
    }
}
