using System;
using System.Collections.Generic;

namespace Recruitment.Logic
{
    /// <summary>The coefficients of <see cref="ApplicantScore"/>, set in the mod settings.</summary>
    public readonly struct ScoreWeights
    {
        public static readonly ScoreWeights Default = new ScoreWeights(2f, 4f, 5f, 5f);

        public readonly float MinorPassion;

        public readonly float MajorPassion;

        public readonly float IncapablePenalty;

        public readonly float HealthPenalty;

        public ScoreWeights(float minorPassion, float majorPassion, float incapablePenalty, float healthPenalty)
        {
            MinorPassion = minorPassion;
            MajorPassion = majorPassion;
            IncapablePenalty = incapablePenalty;
            HealthPenalty = healthPenalty;
        }
    }

    /// <summary>What the score looks at, read from a generated pawn.</summary>
    public readonly struct ApplicantFacts
    {
        /// <summary>Skill levels (a skill the pawn cannot use counts as 0).</summary>
        public readonly IReadOnlyList<int> SkillLevels;

        /// <summary>Passion per skill: 0 none, 1 minor, 2 major; other values (from mods) are ignored.</summary>
        public readonly IReadOnlyList<int> Passions;

        public readonly int IncapableWorkTypes;

        public readonly int HealthProblems;

        public ApplicantFacts(IReadOnlyList<int> skillLevels, IReadOnlyList<int> passions, int incapableWorkTypes, int healthProblems)
        {
            SkillLevels = skillLevels;
            Passions = passions;
            IncapableWorkTypes = incapableWorkTypes;
            HealthProblems = healthProblems;
        }
    }

    /// <summary>
    /// Rates candidates when a silver premium was paid: the six best skill levels, plus passions, minus work the
    /// pawn cannot do and health problems. Traits are left out: the game has no data that says whether a trait
    /// is good or bad.
    /// </summary>
    public static class ApplicantScore
    {
        public const int TopSkills = 6;

        public const int MinorPassion = 1;

        public const int MajorPassion = 2;

        public static float Score(ApplicantFacts applicant, ScoreWeights weights)
        {
            var levels = new List<int>(applicant.SkillLevels ?? Array.Empty<int>());
            levels.Sort();
            float score = 0f;
            for (int i = levels.Count - 1; i >= 0 && i >= levels.Count - TopSkills; i--)
            {
                score += levels[i];
            }
            if (applicant.Passions != null)
            {
                foreach (int passion in applicant.Passions)
                {
                    if (passion == MinorPassion)
                    {
                        score += weights.MinorPassion;
                    }
                    else if (passion == MajorPassion)
                    {
                        score += weights.MajorPassion;
                    }
                }
            }
            score -= weights.IncapablePenalty * applicant.IncapableWorkTypes;
            score -= weights.HealthPenalty * applicant.HealthProblems;
            return score;
        }

        /// <summary>The index of the highest score (the first one on ties), or -1 if there are none.</summary>
        public static int Best(IReadOnlyList<float> scores)
        {
            int best = -1;
            for (int i = 0; i < scores.Count; i++)
            {
                if (best < 0 || scores[i] > scores[best])
                {
                    best = i;
                }
            }
            return best;
        }
    }
}
