using System.Collections.Generic;
using System.Linq;
using Recruitment.Logic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Recruitment
{
    /// <summary>Overview of running drives, waiting applicants and how to start each method.</summary>
    public class MainTabWindow_Recruitment : MainTabWindow
    {
        private Vector2 scrollPosition;

        private float viewHeight = 400f;

        // Research lookups and translations do not need to run every frame.
        private readonly TimedCache<int, List<string>> methodLines = new TimedCache<int, List<string>>(RecruitmentUI.CacheSeconds, () => RecruitmentUI.Now);

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
                    RecruitmentUI.OpenApplicants(group);
                }
            }

            Heading(listing, "Recruitment_Tab_Methods");
            foreach (string line in methodLines.Get(0, MethodLines))
            {
                listing.Label(line);
            }

            viewHeight = listing.CurHeight + 10f;
            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawDrive(Listing_Standard listing, GameComponent_Recruitment component, RecruitmentDrive drive)
        {
            string where = drive.map != null && Find.Maps.Contains(drive.map) ? drive.map.Parent.LabelCap : "Recruitment_Tab_MapLost".Translate().Resolve();
            string origin = drive.origin != null ? drive.origin.Name : "Recruitment_FromNowhere".Translate().Resolve();
            string timing = "Recruitment_Tab_ArrivesIn".Translate(Mathf.Max(0, drive.TicksUntilArrival).ToStringTicksToPeriod()).Resolve();
            string line = "Recruitment_Tab_DriveLine".Translate(drive.method.LabelCap, where, origin, timing);
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

        private static List<string> MethodLines()
        {
            var lines = new List<string>();
            foreach (RecruitmentMethodDef method in RecruitmentMethodDef.AllInOrder)
            {
                FloatRange days = method.Days;
                string research = method.ResearchFinished ? "" : " " + "Recruitment_Tab_NeedsResearch".Translate(method.MissingResearch.Select(r => r.LabelCap.Resolve()).ToCommaList()).Colorize(ColorLibrary.RedReadable);
                lines.Add("Recruitment_Tab_MethodLine".Translate(method.LabelCap, method.Cost, days.min.ToString("0.#"), days.max.ToString("0.#")) + research);
                lines.Add("    " + ("Recruitment_Tab_HowTo_" + method.startKind).Translate());
            }
            return lines;
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
