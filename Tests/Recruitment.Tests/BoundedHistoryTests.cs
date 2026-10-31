using System.Collections.Generic;
using System.Linq;
using Recruitment.Logic;
using Xunit;
namespace Recruitment.Tests
{
    public class BoundedHistoryTests
    {
        [Fact]
        public void KeepsOnlyLatestHundredResults()
        {
            var entries = new List<int>();
            for (int i=0;i<105;i++) BoundedHistory.Store(entries, i, v=>v, 100);
            Assert.Equal(100, entries.Count);
            Assert.Equal(Enumerable.Range(5,100), entries);
        }
        [Fact]
        public void RecoveryUpdatesSameRecordInsteadOfDuplicatingIt()
        {
            var entries = new List<(int id, string result)>();
            BoundedHistory.Store(entries, (1, "pending"), e=>e.Item1, 100);
            BoundedHistory.Store(entries, (2, "joined"), e=>e.Item1, 100);
            BoundedHistory.Store(entries, (1, "recovered"), e=>e.Item1, 100);
            Assert.Equal(2,entries.Count);
            Assert.Equal((1,"recovered"),entries[1]);
        }
    }
}
