using System.Collections.Generic;
using Recruitment.Logic;
using Verse;

namespace Recruitment
{
    public enum JoinMode
    {
        /// <summary>The player reviews the applicants and accepts or rejects each of them.</summary>
        Choose,

        /// <summary>Applicants join as soon as they arrive.</summary>
        AutoJoin,
    }

    public class RecruitmentSettings : ModSettings
    {
        public const float DefaultApplicantWeight = 25f;

        /// <summary>The chances at Social 20 by default: 5% / 15% / 50% / 30%.</summary>
        public static readonly float[] DefaultWeightsAtMaxSocial = { 5f, 15f, 50f, 30f };

        public const float DefaultEnvoyCooldownDays = 15f;

        public JoinMode joinMode = JoinMode.Choose;

        public int maxConcurrentPerMap = 1;

        public float decisionHours = 24f;

        public bool includePermanentEnemies;

        public bool allowChildren;

        /// <summary>
        /// Relative weights of getting 0, 1, 2 and 3 applicants when the colonist who starts the drive has Social 0.
        /// (Saved under its 1.0 name, when it applied to every Social level.)
        /// </summary>
        public List<float> applicantWeights = DefaultWeights();

        /// <summary>The same weights at Social 20; levels in between blend the two (<see cref="SocialWeights"/>).</summary>
        public List<float> applicantWeightsAtMaxSocial = DefaultWeights(DefaultWeightsAtMaxSocial);

        // Coefficients of the candidate score used with a silver premium (ApplicantScore).
        public float scoreMinorPassion = ScoreWeights.Default.MinorPassion;

        public float scoreMajorPassion = ScoreWeights.Default.MajorPassion;

        public float scoreIncapablePenalty = ScoreWeights.Default.IncapablePenalty;

        public float scoreHealthPenalty = ScoreWeights.Default.HealthPenalty;

        /// <summary>Days before the same settlement can be asked by an envoy again.</summary>
        public float envoyCooldownDays = DefaultEnvoyCooldownDays;

        // Keyed by RecruitmentMethodDef.defName; a missing entry means "use the def's value".
        public Dictionary<string, int> costOverrides = new Dictionary<string, int>();

        public Dictionary<string, float> minDaysOverrides = new Dictionary<string, float>();

        public Dictionary<string, float> maxDaysOverrides = new Dictionary<string, float>();

        public ScoreWeights Score => new ScoreWeights(scoreMinorPassion, scoreMajorPassion, scoreIncapablePenalty, scoreHealthPenalty);

        /// <summary>The chances of 0..3 applicants for a colonist with <paramref name="social"/>.</summary>
        public float[] ApplicantChances(int social)
        {
            return SocialWeights.Chances(applicantWeights, applicantWeightsAtMaxSocial, social);
        }

        public int EnvoyCooldownTicks => (int)(System.Math.Max(0f, envoyCooldownDays) * RecruitmentDuration.TicksPerDay);

        public int DecisionTicks => System.Math.Max(RecruitmentDuration.MinTicks, (int)(decisionHours * 2500f));

        public int CostFor(RecruitmentMethodDef method)
        {
            return costOverrides.TryGetValue(method.defName, out int cost) ? cost : method.baseCost;
        }

        public FloatRange DaysFor(RecruitmentMethodDef method)
        {
            float min = minDaysOverrides.TryGetValue(method.defName, out float a) ? a : method.baseDays.min;
            float max = maxDaysOverrides.TryGetValue(method.defName, out float b) ? b : method.baseDays.max;
            return new FloatRange(min, max);
        }

        public void SetCost(RecruitmentMethodDef method, int cost)
        {
            if (cost == method.baseCost)
            {
                costOverrides.Remove(method.defName);
            }
            else
            {
                costOverrides[method.defName] = cost;
            }
        }

        public void SetDays(RecruitmentMethodDef method, float min, float max)
        {
            SetOverride(minDaysOverrides, method.defName, min, method.baseDays.min);
            SetOverride(maxDaysOverrides, method.defName, max, method.baseDays.max);
        }

        public void ResetToDefaults()
        {
            joinMode = JoinMode.Choose;
            maxConcurrentPerMap = 1;
            decisionHours = 24f;
            includePermanentEnemies = false;
            allowChildren = false;
            applicantWeights = DefaultWeights();
            applicantWeightsAtMaxSocial = DefaultWeights(DefaultWeightsAtMaxSocial);
            scoreMinorPassion = ScoreWeights.Default.MinorPassion;
            scoreMajorPassion = ScoreWeights.Default.MajorPassion;
            scoreIncapablePenalty = ScoreWeights.Default.IncapablePenalty;
            scoreHealthPenalty = ScoreWeights.Default.HealthPenalty;
            envoyCooldownDays = DefaultEnvoyCooldownDays;
            costOverrides.Clear();
            minDaysOverrides.Clear();
            maxDaysOverrides.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref joinMode, "joinMode", JoinMode.Choose);
            Scribe_Values.Look(ref maxConcurrentPerMap, "maxConcurrentPerMap", 1);
            Scribe_Values.Look(ref decisionHours, "decisionHours", 24f);
            Scribe_Values.Look(ref includePermanentEnemies, "includePermanentEnemies", false);
            Scribe_Values.Look(ref allowChildren, "allowChildren", false);
            Scribe_Collections.Look(ref applicantWeights, "applicantWeights", LookMode.Value);
            Scribe_Collections.Look(ref applicantWeightsAtMaxSocial, "applicantWeightsAtMaxSocial", LookMode.Value);
            Scribe_Values.Look(ref scoreMinorPassion, "scoreMinorPassion", ScoreWeights.Default.MinorPassion);
            Scribe_Values.Look(ref scoreMajorPassion, "scoreMajorPassion", ScoreWeights.Default.MajorPassion);
            Scribe_Values.Look(ref scoreIncapablePenalty, "scoreIncapablePenalty", ScoreWeights.Default.IncapablePenalty);
            Scribe_Values.Look(ref scoreHealthPenalty, "scoreHealthPenalty", ScoreWeights.Default.HealthPenalty);
            Scribe_Values.Look(ref envoyCooldownDays, "envoyCooldownDays", DefaultEnvoyCooldownDays);
            Scribe_Collections.Look(ref costOverrides, "costOverrides", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref minDaysOverrides, "minDaysOverrides", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref maxDaysOverrides, "maxDaysOverrides", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (applicantWeights == null || applicantWeights.Count != ApplicantRoll.OutcomeCount)
                {
                    applicantWeights = DefaultWeights();
                }
                if (applicantWeightsAtMaxSocial == null || applicantWeightsAtMaxSocial.Count != ApplicantRoll.OutcomeCount)
                {
                    applicantWeightsAtMaxSocial = DefaultWeights(DefaultWeightsAtMaxSocial);
                }
                costOverrides ??= new Dictionary<string, int>();
                minDaysOverrides ??= new Dictionary<string, float>();
                maxDaysOverrides ??= new Dictionary<string, float>();
            }
        }

        private static List<float> DefaultWeights(float[] values = null)
        {
            var weights = new List<float>();
            for (int i = 0; i < ApplicantRoll.OutcomeCount; i++)
            {
                weights.Add(values?[i] ?? DefaultApplicantWeight);
            }
            return weights;
        }

        private static void SetOverride(Dictionary<string, float> overrides, string key, float value, float defaultValue)
        {
            if (value == defaultValue)
            {
                overrides.Remove(key);
            }
            else
            {
                overrides[key] = value;
            }
        }
    }
}
