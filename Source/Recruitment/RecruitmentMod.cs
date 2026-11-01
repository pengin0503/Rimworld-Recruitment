using System.Collections.Generic;
using Recruitment.Logic;
using RimWorld;
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
            new HarmonyLib.Harmony("pengin0503.recruitment").PatchAll(typeof(RecruitmentMod).Assembly);
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
            listing.Label("Recruitment_Settings_ApplicantsDesc".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor));
            WeightSliders(listing, "Recruitment_Settings_AtSocialZero", s.applicantWeights, s.ApplicantChances(0));
            WeightSliders(listing, "Recruitment_Settings_AtSocialMax", s.applicantWeightsAtMaxSocial, s.ApplicantChances(SocialWeights.MaxSkill));
            listing.Label("Recruitment_Settings_AtSocialTen".Translate(RecruitmentUI.ChancesText(s.ApplicantChances(10))));

            Heading(listing, "Recruitment_Settings_Score");
            listing.Label("Recruitment_Settings_ScoreDesc".Translate(ApplicantScore.TopSkills).Resolve().Colorize(ColoredText.SubtleGrayColor));
            s.scoreMinorPassion = Mathf.Round(listing.SliderLabeled("Recruitment_Settings_ScoreMinorPassion".Translate(s.scoreMinorPassion.ToString("0")), s.scoreMinorPassion, 0f, 20f));
            s.scoreMajorPassion = Mathf.Round(listing.SliderLabeled("Recruitment_Settings_ScoreMajorPassion".Translate(s.scoreMajorPassion.ToString("0")), s.scoreMajorPassion, 0f, 20f));
            s.scoreIncapablePenalty = Mathf.Round(listing.SliderLabeled("Recruitment_Settings_ScoreIncapable".Translate(s.scoreIncapablePenalty.ToString("0")), s.scoreIncapablePenalty, 0f, 30f));
            s.scoreHealthPenalty = Mathf.Round(listing.SliderLabeled("Recruitment_Settings_ScoreHealth".Translate(s.scoreHealthPenalty.ToString("0")), s.scoreHealthPenalty, 0f, 30f));

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
            s.envoyCooldownDays = Mathf.Round(listing.SliderLabeled("Recruitment_Settings_EnvoyCooldown".Translate(s.envoyCooldownDays.ToString("0")), s.envoyCooldownDays, 0f, 60f));

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

        /// <summary>Sliders for one set of 0..3 applicant weights, each labelled with the chance it gives.</summary>
        private static void WeightSliders(Listing_Standard listing, string headingKey, List<float> weights, float[] chances)
        {
            listing.Label(headingKey.Translate());
            for (int i = 0; i < ApplicantRoll.OutcomeCount; i++)
            {
                string label = "  " + "Recruitment_Settings_ApplicantWeight".Translate(i, weights[i].ToString("0"), chances[i].ToStringPercent());
                weights[i] = Mathf.Round(listing.SliderLabeled(label, weights[i], 0f, 100f));
            }
        }
    }
}
