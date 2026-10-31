using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Recruitment
{
    /// <summary>Shows every applicant side by side so the player can accept or reject each one.</summary>
    public class Dialog_Applicants : Window
    {
        private const float RowHeight = 190f;

        private const float PortraitWidth = 120f;

        private const float ButtonHeight = 35f;

        private readonly HashSet<Pawn> accepted;

        // Applicants are held unspawned and do not change while waiting, so their text is built once.
        private readonly Dictionary<Pawn, string> descriptions = new Dictionary<Pawn, string>();

        private Vector2 scrollPosition;

        public ApplicantGroup Group { get; }

        public override Vector2 InitialSize => new Vector2(880f, 760f);

        public Dialog_Applicants(ApplicantGroup group)
        {
            Group = group;
            accepted = new HashSet<Pawn>(group.pawns);
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            if (component == null || !component.Groups.Contains(Group))
            {
                Close();
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), "Recruitment_Dialog_Title".Translate(Group.pawns.Count));
            Text.Font = GameFont.Small;
            string subtitle = "Recruitment_Dialog_Subtitle".Translate(Group.method.LabelCap, Mathf.Max(0, Group.TicksLeft).ToStringTicksToPeriod());
            Widgets.Label(new Rect(inRect.x, inRect.y + 35f, inRect.width, 25f), subtitle);

            Rect outRect = new Rect(inRect.x, inRect.y + 65f, inRect.width, inRect.height - 65f - ButtonHeight - 10f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, RowHeight * Group.pawns.Count);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            for (int i = 0; i < Group.pawns.Count; i++)
            {
                DrawApplicant(new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 6f), Group.pawns[i], i);
            }
            Widgets.EndScrollView();

            DrawButtons(new Rect(inRect.x, inRect.yMax - ButtonHeight, inRect.width, ButtonHeight), component);
        }

        private void DrawApplicant(Rect rect, Pawn pawn, int index)
        {
            if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(rect);
            }
            bool isAccepted = accepted.Contains(pawn);
            if (isAccepted)
            {
                Widgets.DrawBoxSolidWithOutline(rect, new Color(0.2f, 0.4f, 0.2f, 0.15f), new Color(0.4f, 0.7f, 0.4f, 0.6f));
            }

            Rect portraitRect = new Rect(rect.x + 4f, rect.y + 4f, PortraitWidth, rect.height - 8f);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawn, portraitRect.size, Rot4.South, default, 1.2f));

            Rect controls = new Rect(rect.xMax - 130f, rect.y + 6f, 124f, rect.height - 12f);
            Widgets.InfoCardButton(controls.x, controls.y, pawn);
            Rect toggleRect = new Rect(controls.x, controls.y + 34f, controls.width, 30f);
            if (Widgets.ButtonText(toggleRect, (isAccepted ? "Recruitment_Dialog_Accepted" : "Recruitment_Dialog_Rejected").Translate()))
            {
                if (isAccepted)
                {
                    accepted.Remove(pawn);
                }
                else
                {
                    accepted.Add(pawn);
                }
            }

            Rect textRect = new Rect(portraitRect.xMax + 10f, rect.y + 4f, controls.x - portraitRect.xMax - 16f, rect.height - 8f);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(textRect.x, textRect.y, textRect.width, 24f), pawn.Name?.ToStringFull ?? pawn.LabelCap);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(textRect.x, textRect.y + 24f, textRect.width, textRect.height - 24f), Description(pawn));
            Text.Font = GameFont.Small;
        }

        private void DrawButtons(Rect rect, GameComponent_Recruitment component)
        {
            float width = (rect.width - 30f) / 4f;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, width, rect.height), "Recruitment_Dialog_AcceptAll".Translate()))
            {
                accepted.UnionWith(Group.pawns);
            }
            if (Widgets.ButtonText(new Rect(rect.x + (width + 10f), rect.y, width, rect.height), "Recruitment_Dialog_RejectAll".Translate()))
            {
                accepted.Clear();
            }
            if (Widgets.ButtonText(new Rect(rect.x + (width + 10f) * 2f, rect.y, width, rect.height), "Recruitment_Dialog_Later".Translate()))
            {
                Close();
            }
            int count = Group.pawns.Count(accepted.Contains);
            if (Widgets.ButtonText(new Rect(rect.x + (width + 10f) * 3f, rect.y, width, rect.height), "Recruitment_Dialog_Confirm".Translate(count)))
            {
                AcceptanceReport report = component.Decide(Group, accepted, expired: false);
                if (report.Accepted)
                {
                    Close();
                }
                else if (!report.Reason.NullOrEmpty())
                {
                    Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
                }
            }
        }

        private string Description(Pawn pawn)
        {
            if (!descriptions.TryGetValue(pawn, out string text))
            {
                text = Describe(pawn);
                descriptions[pawn] = text;
            }
            return text;
        }

        private static string Describe(Pawn pawn)
        {
            var lines = new List<string>();
            var basics = new List<string>
            {
                "Recruitment_Dialog_Age".Translate(pawn.ageTracker.AgeBiologicalYears),
                pawn.gender.GetLabel(pawn.RaceProps.Animal).CapitalizeFirst(),
            };
            if (pawn.def != ThingDefOf.Human)
            {
                basics.Add(pawn.def.LabelCap);
            }
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                basics.Add(pawn.genes.XenotypeLabelCap);
            }
            lines.Add(string.Join(" / ", basics));
            lines.Add("Recruitment_Dialog_Origin".Translate(pawn.Faction != null ? pawn.Faction.Name : "Recruitment_FromNowhere".Translate().Resolve()));
            if (ModsConfig.IdeologyActive && pawn.Ideo != null)
            {
                lines.Add("Recruitment_Dialog_Ideo".Translate(pawn.Ideo.name));
            }
            if (pawn.story != null)
            {
                IEnumerable<string> backstories = new[] { pawn.story.Childhood, pawn.story.Adulthood }.Where(b => b != null).Select(b => b.TitleCapFor(pawn.gender));
                lines.Add("Recruitment_Dialog_Backstory".Translate(string.Join(", ", backstories)));
                if (pawn.story.traits.allTraits.Count > 0)
                {
                    lines.Add("Recruitment_Dialog_Traits".Translate(string.Join(", ", pawn.story.traits.allTraits.Select(t => t.LabelCap))));
                }
            }
            if (pawn.skills != null)
            {
                IEnumerable<string> skills = pawn.skills.skills
                    .Where(s => !s.TotallyDisabled)
                    .OrderByDescending(s => s.Level)
                    .Take(6)
                    .Select(s => s.def.LabelCap + " " + s.Level + PassionMark(s.passion));
                lines.Add("Recruitment_Dialog_Skills".Translate(string.Join(", ", skills)));
            }
            List<string> disabledWork = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w => w.visible && pawn.WorkTypeIsDisabled(w)).Select(w => w.labelShort.CapitalizeFirst()).ToList();
            if (disabledWork.Count > 0)
            {
                lines.Add("Recruitment_Dialog_Incapable".Translate(string.Join(", ", disabledWork)));
            }
            List<string> health = pawn.health.hediffSet.hediffs
                .Where(h => h.Visible && (h.def.isBad || h is Hediff_AddedPart))
                .Select(h => h.Part != null ? h.LabelCap + " (" + h.Part.LabelCap + ")" : h.LabelCap)
                .ToList();
            lines.Add("Recruitment_Dialog_Health".Translate(health.Count > 0 ? string.Join(", ", health) : "Recruitment_Dialog_Healthy".Translate().Resolve()));
            return string.Join("\n", lines);
        }

        private static string PassionMark(Passion passion)
        {
            switch (passion)
            {
                case Passion.None:
                    return "";
                case Passion.Minor:
                    return " (+)";
                case Passion.Major:
                    return " (++)";
                default:
                    return " (" + passion.GetLabel() + ")";
            }
        }
    }
}
