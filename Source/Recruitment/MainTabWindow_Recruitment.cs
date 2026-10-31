using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Recruitment
{
    /// <summary>Overview of running drives and waiting applicants, plus the entry point for sending envoys.</summary>
    public class MainTabWindow_Recruitment : MainTabWindow
    {
        private Vector2 scrollPosition;

        private float viewHeight = 400f;

        public override Vector2 RequestedTabSize => new Vector2(760f, 560f);

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            if (component == null)
            {
                return;
            }
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            Map current = Find.CurrentMap;
            AcceptanceReport envoyReport = current == null ? "Recruitment_Tab_NoMap".Translate() : component.CanStart(RecruitmentDefOf.Recruitment_Envoy, current, needsOriginLottery: true);
            Rect envoyButton = listing.GetRect(32f);
            envoyButton.width = 260f;
            if (envoyReport.Accepted)
            {
                if (Widgets.ButtonText(envoyButton, "Recruitment_Tab_SendEnvoy".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_SelectEnvoy(current));
                }
            }
            else
            {
                Widgets.ButtonText(envoyButton, "Recruitment_Tab_SendEnvoy".Translate(), active: false);
                TooltipHandler.TipRegion(envoyButton, envoyReport.Reason);
            }
            listing.Gap();

            Heading(listing, "Recruitment_Tab_Active");
            if (component.Drives.Count == 0)
            {
                listing.Label("Recruitment_Tab_None".Translate());
            }
            foreach (RecruitmentDrive drive in component.Drives.ToList())
            {
                DrawDrive(listing, component, drive);
            }

            Heading(listing, "Recruitment_Tab_Waiting");
            if (component.Groups.Count == 0)
            {
                listing.Label("Recruitment_Tab_None".Translate());
            }
            foreach (ApplicantGroup group in component.Groups.ToList())
            {
                Rect row = listing.GetRect(30f);
                Widgets.Label(row.LeftPart(0.7f), "Recruitment_Tab_GroupLine".Translate(group.pawns.Count, group.method.LabelCap, Mathf.Max(0, group.TicksLeft).ToStringTicksToPeriod()));
                if (Widgets.ButtonText(row.RightPart(0.28f), "Recruitment_Tab_Review".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_Applicants(group));
                }
            }

            Heading(listing, "Recruitment_Tab_Methods");
            foreach (RecruitmentMethodDef method in RecruitmentMethodDef.AllInOrder)
            {
                FloatRange days = method.Days;
                string research = method.ResearchFinished ? "" : " " + "Recruitment_Tab_NeedsResearch".Translate(method.MissingResearch.Select(r => r.LabelCap.Resolve()).ToCommaList()).Colorize(ColorLibrary.RedReadable);
                listing.Label("Recruitment_Tab_MethodLine".Translate(method.LabelCap, method.Cost, days.min.ToString("0.#"), days.max.ToString("0.#")) + research);
                listing.Label("    " + ("Recruitment_Tab_HowTo_" + method.startKind).Translate());
            }

            viewHeight = listing.CurHeight + 10f;
            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawDrive(Listing_Standard listing, GameComponent_Recruitment component, RecruitmentDrive drive)
        {
            string where = drive.map != null && Find.Maps.Contains(drive.map) ? drive.map.Parent.LabelCap : "Recruitment_Tab_MapLost".Translate().Resolve();
            string origin = drive.origin != null ? drive.origin.Name : "Recruitment_FromNowhere".Translate().Resolve();
            string timing = drive.state == DriveState.EnvoyDeparting
                ? "Recruitment_Tab_EnvoyDeparting".Translate(drive.envoy?.LabelShort ?? "?").Resolve()
                : "Recruitment_Tab_ArrivesIn".Translate(Mathf.Max(0, drive.TicksUntilArrival).ToStringTicksToPeriod()).Resolve();
            string line = "Recruitment_Tab_DriveLine".Translate(drive.method.LabelCap, where, origin, timing);
            if (drive.envoy != null && drive.state == DriveState.Underway)
            {
                line += " " + "Recruitment_Tab_Envoy".Translate(drive.envoy.LabelShort);
            }
            if (!Prefs.DevMode)
            {
                listing.Label(line);
                return;
            }
            Rect row = listing.GetRect(Text.CalcHeight(line, listing.ColumnWidth * 0.7f));
            Widgets.Label(row.LeftPart(0.7f), line + " [DEV: " + drive.applicantCount + "]");
            if (Widgets.ButtonText(row.RightPart(0.28f), "DEV: arrive now"))
            {
                component.DevArriveNow(drive);
            }
        }

        private static void Heading(Listing_Standard listing, string key)
        {
            Text.Font = GameFont.Medium;
            listing.Label(key.Translate());
            Text.Font = GameFont.Small;
            listing.GapLine(6f);
        }
    }
}
