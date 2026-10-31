using System.Collections.Generic;
using Recruitment.Logic;
using UnityEngine;
using Verse;

namespace Recruitment
{
    public class RecruitmentMod : Mod
    {
        public static RecruitmentSettings Settings { get; private set; }

        private readonly Dictionary<string, string> buffers = new Dictionary<string, string>();

        private Vector2 scrollPosition;

        private float viewHeight = 1200f;

        public RecruitmentMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RecruitmentSettings>();
        }

        public override string SettingsCategory()
        {
            return "Recruitment_ModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            RecruitmentSettings s = Settings;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, viewHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            Heading(listing, "Recruitment_Settings_General");
            if (listing.ButtonTextLabeled("Recruitment_Settings_JoinMode".Translate(), ("Recruitment_JoinMode_" + s.joinMode).Translate()))
            {
                var options = new List<FloatMenuOption>();
                foreach (JoinMode mode in new[] { JoinMode.Choose, JoinMode.AutoJoin })
                {
                    options.Add(new FloatMenuOption(("Recruitment_JoinMode_" + mode).Translate(), () => s.joinMode = mode));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            s.maxConcurrentPerMap = Mathf.RoundToInt(listing.SliderLabeled("Recruitment_Settings_MaxConcurrent".Translate(s.maxConcurrentPerMap), s.maxConcurrentPerMap, 1f, 10f));
            s.decisionHours = Mathf.Round(listing.SliderLabeled("Recruitment_Settings_DecisionHours".Translate(s.decisionHours.ToString("0")), s.decisionHours, 1f, 120f));
            listing.CheckboxLabeled("Recruitment_Settings_IncludePermanentEnemies".Translate(), ref s.includePermanentEnemies, "Recruitment_Settings_IncludePermanentEnemiesDesc".Translate());
            listing.CheckboxLabeled("Recruitment_Settings_AllowChildren".Translate(), ref s.allowChildren, "Recruitment_Settings_AllowChildrenDesc".Translate());

            Heading(listing, "Recruitment_Settings_Applicants");
            float total = 0f;
            foreach (float w in s.applicantWeights)
            {
                total += Mathf.Max(0f, w);
            }
            for (int i = 0; i < ApplicantRoll.OutcomeCount; i++)
            {
                float chance = total > 0f ? Mathf.Max(0f, s.applicantWeights[i]) / total : (i == 0 ? 1f : 0f);
                string label = "Recruitment_Settings_ApplicantWeight".Translate(i, s.applicantWeights[i].ToString("0"), chance.ToStringPercent());
                s.applicantWeights[i] = Mathf.Round(listing.SliderLabeled(label, s.applicantWeights[i], 0f, 100f));
            }

            Heading(listing, "Recruitment_Settings_Methods");
            foreach (RecruitmentMethodDef method in RecruitmentMethodDef.AllInOrder)
            {
                listing.Label(method.LabelCap);
                int cost = s.CostFor(method);
                FloatRange days = s.DaysFor(method);
                float min = days.min;
                float max = days.max;
                string costBuffer = Buffer("cost_" + method.defName, cost.ToString());
                string minBuffer = Buffer("min_" + method.defName, min.ToString());
                string maxBuffer = Buffer("max_" + method.defName, max.ToString());
                listing.TextFieldNumericLabeled("  " + "Recruitment_Settings_Cost".Translate(), ref cost, ref costBuffer, 0f, 1000000f);
                listing.TextFieldNumericLabeled("  " + "Recruitment_Settings_MinDays".Translate(), ref min, ref minBuffer, 0f, 120f);
                listing.TextFieldNumericLabeled("  " + "Recruitment_Settings_MaxDays".Translate(), ref max, ref maxBuffer, 0f, 120f);
                buffers["cost_" + method.defName] = costBuffer;
                buffers["min_" + method.defName] = minBuffer;
                buffers["max_" + method.defName] = maxBuffer;
                s.SetCost(method, cost);
                s.SetDays(method, min, max);
                if (min > max)
                {
                    listing.Label("  " + "Recruitment_Settings_MinAboveMax".Translate().Colorize(ColorLibrary.RedReadable));
                }
            }

            Heading(listing, "Recruitment_Settings_Envoy");
            s.envoyDurationFactorAtZero = Rounded(listing.SliderLabeled("Recruitment_Settings_EnvoyDurationAtZero".Translate(s.envoyDurationFactorAtZero.ToStringPercent()), s.envoyDurationFactorAtZero, 0.1f, 3f));
            s.envoyDurationFactorPerLevel = Rounded(listing.SliderLabeled("Recruitment_Settings_EnvoyDurationPerLevel".Translate(s.envoyDurationFactorPerLevel.ToStringPercent("0.#")), s.envoyDurationFactorPerLevel, 0f, 0.1f), 1000f);
            s.envoyZeroChanceAtZero = Rounded(listing.SliderLabeled("Recruitment_Settings_EnvoyZeroAtZero".Translate(s.envoyZeroChanceAtZero.ToStringPercent()), s.envoyZeroChanceAtZero, 0f, 1f));
            s.envoyZeroChancePerLevel = Rounded(listing.SliderLabeled("Recruitment_Settings_EnvoyZeroPerLevel".Translate(s.envoyZeroChancePerLevel.ToStringPercent("0.#")), s.envoyZeroChancePerLevel, 0f, 0.1f), 1000f);
            EnvoySocialCurve curve = s.EnvoyCurve;
            listing.Label("Recruitment_Settings_EnvoyPreview".Translate(curve.DurationFactor(0).ToStringPercent(), curve.ZeroChance(0).ToStringPercent(), curve.DurationFactor(10).ToStringPercent(), curve.ZeroChance(10).ToStringPercent(), curve.DurationFactor(20).ToStringPercent(), curve.ZeroChance(20).ToStringPercent()));

            listing.GapLine();
            if (listing.ButtonText("Recruitment_Settings_Reset".Translate()))
            {
                s.ResetToDefaults();
                buffers.Clear();
            }

            viewHeight = listing.CurHeight + 20f;
            listing.End();
            Widgets.EndScrollView();
        }

        private static void Heading(Listing_Standard listing, string key)
        {
            listing.Gap();
            Text.Font = GameFont.Medium;
            listing.Label(key.Translate());
            Text.Font = GameFont.Small;
            listing.GapLine(6f);
        }

        private string Buffer(string key, string initial)
        {
            if (!buffers.TryGetValue(key, out string value))
            {
                value = initial;
                buffers[key] = value;
            }
            return value;
        }

        private static float Rounded(float value, float steps = 100f)
        {
            return Mathf.Round(value * steps) / steps;
        }
    }
}
