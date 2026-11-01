using System;

namespace Recruitment.Logic
{
    /// <summary>
    /// Extra silver paid on top of a method's cost. Each tier generates more candidates per applicant, of whom
    /// the best is the one who comes. Tier 0 is no premium.
    /// </summary>
    public static class PremiumTier
    {
        public const int Count = 4;

        public const int Max = Count - 1;

        private static readonly int[] Percents = { 0, 50, 100, 200 };

        private static readonly int[] CandidateCounts = { 1, 2, 3, 4 };

        public static int Clamp(int tier)
        {
            return Math.Clamp(tier, 0, Max);
        }

        /// <summary>The premium as a percentage of the method's cost.</summary>
        public static int ExtraPercent(int tier)
        {
            return Percents[Clamp(tier)];
        }

        /// <summary>Candidates generated for each applicant.</summary>
        public static int Candidates(int tier)
        {
            return CandidateCounts[Clamp(tier)];
        }

        /// <summary>The premium in silver, rounded half up; never negative.</summary>
        public static int ExtraCost(int baseCost, int tier)
        {
            if (baseCost <= 0)
            {
                return 0;
            }
            long extra = ((long)baseCost * ExtraPercent(tier) + 50) / 100;
            return (int)Math.Min(extra, int.MaxValue);
        }

        public static int TotalCost(int baseCost, int tier)
        {
            if (baseCost <= 0)
            {
                return 0;
            }
            return (int)Math.Min((long)baseCost + ExtraCost(baseCost, tier), int.MaxValue);
        }
    }
}
