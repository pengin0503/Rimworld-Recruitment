using System;

namespace Recruitment.Logic
{
    public enum DirectRequestBlock
    {
        None,
        Hostile,
        Goodwill,
        Pending,
    }

    /// <summary>
    /// The faction-side conditions of asking a faction for settlers over the comms console: not hostile, goodwill
    /// that stays at 0 or above after paying, and no earlier request to that faction still waiting for its result.
    /// </summary>
    public static class DirectRequestRule
    {
        public const int GoodwillCost = 10;

        public const int MinGoodwillAfter = 0;

        public static DirectRequestBlock Check(bool hostile, int goodwill, int goodwillCost, bool pending)
        {
            if (hostile)
            {
                return DirectRequestBlock.Hostile;
            }
            if (goodwill - goodwillCost < MinGoodwillAfter)
            {
                return DirectRequestBlock.Goodwill;
            }
            return pending ? DirectRequestBlock.Pending : DirectRequestBlock.None;
        }

        public static int GoodwillAfter(int goodwill, int goodwillCost)
        {
            return Math.Clamp(goodwill - goodwillCost, -100, 100);
        }
    }
}
