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

        public const int DefaultConfirmThreshold = 200;

        public const float DefaultEnvoyCooldownDays = 15f;

        public JoinMode joinMode = JoinMode.Choose;

        public int maxConcurrentPerMap = 1;

        public float decisionHours = 24f;

        public bool includePermanentEnemies;

        public bool allowChildren;

        /// <summary>Ask before starting a drive that costs at least <see cref="confirmThreshold"/> silver.</summary>
        public bool confirmExpensive = true;

        public int confirmThreshold = DefaultConfirmThreshold;

        /// <summary>Relative weights of getting 0, 1, 2 and 3 applicants.</summary>
        public List<float> applicantWeights = DefaultWeights();

        public float envoyZeroChanceAtZero = EnvoySocialCurve.Default.ZeroChanceAtZero;

        public float envoyZeroChancePerLevel = EnvoySocialCurve.Default.ZeroChancePerLevel;

        /// <summary>Days before the same settlement can be asked by an envoy again.</summary>
        public float envoyCooldownDays = DefaultEnvoyCooldownDays;

        // Keyed by RecruitmentMethodDef.defName; a missing entry means "use the def's value".
        public Dictionary<string, int> costOverrides = new Dictionary<string, int>();

        public Dictionary<string, float> minDaysOverrides = new Dictionary<string, float>();

        public Dictionary<string, float> maxDaysOverrides = new Dictionary<string, float>();

        public EnvoySocialCurve EnvoyCurve => new EnvoySocialCurve(envoyZeroChanceAtZero, envoyZeroChancePerLevel);

        public int EnvoyCooldownTicks => (int)(System.Math.Max(0f, envoyCooldownDays) * RecruitmentDuration.TicksPerDay);

        /// <summary>The threshold for <see cref="Logic.ConfirmationRule"/>; negative when confirmation is off.</summary>
        public int EffectiveConfirmThreshold => confirmExpensive ? System.Math.Max(0, confirmThreshold) : -1;

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
            confirmExpensive = true;
            confirmThreshold = DefaultConfirmThreshold;
            applicantWeights = DefaultWeights();
            envoyZeroChanceAtZero = EnvoySocialCurve.Default.ZeroChanceAtZero;
            envoyZeroChancePerLevel = EnvoySocialCurve.Default.ZeroChancePerLevel;
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
            Scribe_Values.Look(ref confirmExpensive, "confirmExpensive", true);
            Scribe_Values.Look(ref confirmThreshold, "confirmThreshold", DefaultConfirmThreshold);
            Scribe_Collections.Look(ref applicantWeights, "applicantWeights", LookMode.Value);
            Scribe_Values.Look(ref envoyZeroChanceAtZero, "envoyZeroChanceAtZero", EnvoySocialCurve.Default.ZeroChanceAtZero);
            Scribe_Values.Look(ref envoyZeroChancePerLevel, "envoyZeroChancePerLevel", EnvoySocialCurve.Default.ZeroChancePerLevel);
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
                costOverrides ??= new Dictionary<string, int>();
                minDaysOverrides ??= new Dictionary<string, float>();
                maxDaysOverrides ??= new Dictionary<string, float>();
            }
        }

        private static List<float> DefaultWeights()
        {
            var weights = new List<float>();
            for (int i = 0; i < ApplicantRoll.OutcomeCount; i++)
            {
                weights.Add(DefaultApplicantWeight);
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
