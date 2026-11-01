using System;
using System.Collections.Generic;

namespace Recruitment.Logic
{
    /// <summary>
    /// How the Social skill of the colonist who starts a drive shifts the chances of 0..3 applicants: the chances
    /// for Social 0 and for Social 20 are blended in a straight line.
    /// </summary>
    public static class SocialWeights
    {
        public const int MaxSkill = 20;

        /// <summary>
        /// The chance of each applicant count for <paramref name="social"/> (clamped to 0..20). Each end is
        /// normalised first, so the result blends the percentages the player sees; an end whose weights are all
        /// zero means nobody comes. Negative weights count as zero.
        /// </summary>
        public static float[] Chances(IReadOnlyList<float> atZero, IReadOnlyList<float> atMax, int social)
        {
            float[] low = Normalised(atZero);
            float[] high = Normalised(atMax);
            float t = Math.Clamp(social, 0, MaxSkill) / (float)MaxSkill;
            var result = new float[ApplicantRoll.OutcomeCount];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = low[i] + (high[i] - low[i]) * t;
            }
            return result;
        }

        private static float[] Normalised(IReadOnlyList<float> weights)
        {
            if (weights == null || weights.Count != ApplicantRoll.OutcomeCount)
            {
                throw new ArgumentException($"Expected {ApplicantRoll.OutcomeCount} weights.", nameof(weights));
            }
            var result = new float[ApplicantRoll.OutcomeCount];
            float total = 0f;
            for (int i = 0; i < result.Length; i++)
            {
                total += Math.Max(0f, weights[i]);
            }
            if (total <= 0f)
            {
                result[0] = 1f;
                return result;
            }
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Math.Max(0f, weights[i]) / total;
            }
            return result;
        }
    }
}
