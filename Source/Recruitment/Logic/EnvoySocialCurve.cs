using System;

namespace Recruitment.Logic
{
    /// <summary>How the negotiator's Social skill lowers the chance that an envoy's request finds nobody.</summary>
    public readonly struct EnvoySocialCurve
    {
        public const int MaxSkill = 20;

        public static readonly EnvoySocialCurve Default = new EnvoySocialCurve(0.35f, 0.015f);

        public readonly float ZeroChanceAtZero;

        public readonly float ZeroChancePerLevel;

        public EnvoySocialCurve(float zeroChanceAtZero, float zeroChancePerLevel)
        {
            ZeroChanceAtZero = zeroChanceAtZero;
            ZeroChancePerLevel = zeroChancePerLevel;
        }

        public float ZeroChance(int social)
        {
            return Math.Clamp(ZeroChanceAtZero - ZeroChancePerLevel * Math.Clamp(social, 0, MaxSkill), 0f, 1f);
        }
    }
}
