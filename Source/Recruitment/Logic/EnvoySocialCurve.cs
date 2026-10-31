using System;

namespace Recruitment.Logic
{
    /// <summary>How an envoy's Social skill changes the drive's duration and its chance of finding nobody.</summary>
    public readonly struct EnvoySocialCurve
    {
        public const int MaxSkill = 20;

        public const float MinDurationFactor = 0.1f;

        public static readonly EnvoySocialCurve Default = new EnvoySocialCurve(1.2f, 0.02f, 0.35f, 0.015f);

        public readonly float DurationFactorAtZero;

        public readonly float DurationFactorPerLevel;

        public readonly float ZeroChanceAtZero;

        public readonly float ZeroChancePerLevel;

        public EnvoySocialCurve(float durationFactorAtZero, float durationFactorPerLevel, float zeroChanceAtZero, float zeroChancePerLevel)
        {
            DurationFactorAtZero = durationFactorAtZero;
            DurationFactorPerLevel = durationFactorPerLevel;
            ZeroChanceAtZero = zeroChanceAtZero;
            ZeroChancePerLevel = zeroChancePerLevel;
        }

        public float DurationFactor(int social)
        {
            return Math.Max(MinDurationFactor, DurationFactorAtZero - DurationFactorPerLevel * Clamp(social));
        }

        public float ZeroChance(int social)
        {
            return Math.Clamp(ZeroChanceAtZero - ZeroChancePerLevel * Clamp(social), 0f, 1f);
        }

        private static int Clamp(int social)
        {
            return Math.Clamp(social, 0, MaxSkill);
        }
    }
}
