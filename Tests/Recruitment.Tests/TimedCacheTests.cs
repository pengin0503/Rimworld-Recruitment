using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class TimedCacheTests
    {
        private float now;

        private TimedCache<string, int> NewCache(float lifetime = 0.5f) => new TimedCache<string, int>(lifetime, () => now);

        [Fact]
        public void ComputesOnceWithinLifetime()
        {
            var cache = NewCache();
            int calls = 0;
            Assert.Equal(1, cache.Get("a", () => ++calls));
            now = 0.4f;
            Assert.Equal(1, cache.Get("a", () => ++calls));
            Assert.Equal(1, calls);
        }

        [Fact]
        public void RecomputesAfterLifetime()
        {
            var cache = NewCache();
            int calls = 0;
            cache.Get("a", () => ++calls);
            now = 0.5f;
            Assert.Equal(2, cache.Get("a", () => ++calls));
        }

        [Fact]
        public void KeysAreIndependent()
        {
            var cache = NewCache();
            Assert.Equal(1, cache.Get("a", () => 1));
            Assert.Equal(2, cache.Get("b", () => 2));
            Assert.Equal(1, cache.Get("a", () => 99));
        }

        [Fact]
        public void ClearForcesRecompute()
        {
            var cache = NewCache();
            cache.Get("a", () => 1);
            cache.Clear();
            Assert.Equal(2, cache.Get("a", () => 2));
        }

        [Fact]
        public void ClockGoingBackwardsRecomputes()
        {
            // A new game resets realtime-based clocks; stale entries must not live forever.
            now = 10f;
            var cache = NewCache();
            cache.Get("a", () => 1);
            now = 1f;
            Assert.Equal(2, cache.Get("a", () => 2));
        }
    }
}
