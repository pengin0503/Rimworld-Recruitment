using System;
using System.Linq;
using Recruitment.Logic;
using UnityEngine;
using Verse;

namespace Recruitment
{
    internal static class RecruitmentUI
    {
        /// <summary>How long UI checks (reachability, silver, factions) are reused before being recomputed.</summary>
        public const float CacheSeconds = 0.5f;

        public static float Now => Time.realtimeSinceStartup;

        /// <summary>Runs <paramref name="action"/>, first asking for confirmation if the method costs at least the configured threshold.</summary>
        public static void ConfirmThen(RecruitmentMethodDef method, Action action, TaggedString? extra = null)
        {
            int cost = method.Cost;
            if (!ConfirmationRule.ShouldConfirm(cost, RecruitmentMod.Settings.EffectiveConfirmThreshold))
            {
                action();
                return;
            }
            FloatRange days = method.Days;
            TaggedString text = "Recruitment_Confirm_Text".Translate(method.LabelCap, cost, days.min.ToString("0.#"), days.max.ToString("0.#"), method.arrivalLabel);
            if (extra.HasValue)
            {
                text += "\n\n" + extra.Value;
            }
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, action, title: "Recruitment_Confirm_Title".Translate()));
        }

        public static void ChooseDestination(Action<Map> action)
        {
            var maps = RecruitmentUtility.StartDestinations(RecruitmentDefOf.Recruitment_Envoy);
            if (maps.Count == 0)
            {
                Messages.Message("Recruitment_Reason_NoDestination".Translate(), RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            if (maps.Count == 1)
            {
                action(maps[0]);
                return;
            }
            Find.WindowStack.Add(new FloatMenu(maps.Select(map => new FloatMenuOption(
                "Recruitment_DestinationOption".Translate(map.Parent.LabelCap), () => action(map))).ToList()));
        }

        /// <summary>Opens the review dialog for <paramref name="group"/> unless it is already open.</summary>
        public static void OpenApplicants(ApplicantGroup group)
        {
            if (!Find.WindowStack.Windows.OfType<Dialog_Applicants>().Any(w => w.Group == group))
            {
                Find.WindowStack.Add(new Dialog_Applicants(group));
            }
        }
    }
}
