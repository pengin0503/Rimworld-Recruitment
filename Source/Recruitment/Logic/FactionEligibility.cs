namespace Recruitment.Logic
{
    /// <summary>The facts about a faction that decide whether applicants may come from it.</summary>
    public readonly record struct FactionTraits
    {
        public bool IsPlayer { get; init; }

        public bool Hidden { get; init; }

        public bool Defeated { get; init; }

        public bool Deactivated { get; init; }

        public bool Temporary { get; init; }

        public bool Humanlike { get; init; }

        public bool PermanentEnemy { get; init; }

        /// <summary>The faction's caravan traders need a royal permit (the Empire needs Knight).</summary>
        public bool RequiresPermit { get; init; }

        public bool PlayerHasPermit { get; init; }
    }

    public static class FactionEligibility
    {
        public static bool IsEligible(FactionTraits faction, bool includePermanentEnemies)
        {
            if (faction.IsPlayer || faction.Hidden || faction.Defeated || faction.Deactivated || faction.Temporary || !faction.Humanlike)
            {
                return false;
            }
            if (faction.PermanentEnemy && !includePermanentEnemies)
            {
                return false;
            }
            return !faction.RequiresPermit || faction.PlayerHasPermit;
        }
    }
}
