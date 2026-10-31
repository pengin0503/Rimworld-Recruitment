using System;
using System.Collections.Generic;

namespace Recruitment.Logic
{
    /// <summary>Decides how many applicants answer a recruitment drive.</summary>
    public static class ApplicantRoll
    {
        public const int MaxApplicants = 3;

        public const int OutcomeCount = MaxApplicants + 1;

        /// <summary>
        /// Picks an applicant count from 0..MaxApplicants. <paramref name="weights"/>[i] is the relative
        /// weight of getting i applicants; negative weights count as zero.
        /// </summary>
        public static int Roll(IReadOnlyList<float> weights, double roll01)
        {
            CheckLength(weights);
            float total = 0f;
            for (int i = 0; i < OutcomeCount; i++)
            {
                total += Math.Max(0f, weights[i]);
            }
            if (total <= 0f)
            {
                return 0;
            }
            double target = Math.Clamp(roll01, 0.0, 1.0) * total;
            double cumulative = 0.0;
            int last = 0;
            for (int i = 0; i < OutcomeCount; i++)
            {
                float w = Math.Max(0f, weights[i]);
                if (w <= 0f)
                {
                    continue;
                }
                last = i;
                cumulative += w;
                if (target < cumulative)
                {
                    return i;
                }
            }
            return last;
        }

        /// <summary>
        /// Builds probabilities where the chance of no applicants is fixed to <paramref name="zeroChance"/>
        /// and the rest is shared by 1..MaxApplicants in proportion to <paramref name="weights"/>.
        /// </summary>
        public static float[] WithZeroChance(IReadOnlyList<float> weights, float zeroChance)
        {
            CheckLength(weights);
            float p0 = Math.Clamp(zeroChance, 0f, 1f);
            float positiveTotal = 0f;
            for (int i = 1; i < OutcomeCount; i++)
            {
                positiveTotal += Math.Max(0f, weights[i]);
            }
            var result = new float[OutcomeCount];
            if (positiveTotal <= 0f)
            {
                result[0] = 1f;
                return result;
            }
            result[0] = p0;
            for (int i = 1; i < OutcomeCount; i++)
            {
                result[i] = (1f - p0) * Math.Max(0f, weights[i]) / positiveTotal;
            }
            return result;
        }

        private static void CheckLength(IReadOnlyList<float> weights)
        {
            if (weights == null || weights.Count != OutcomeCount)
            {
                throw new ArgumentException($"Expected {OutcomeCount} weights.", nameof(weights));
            }
        }
    }
}
