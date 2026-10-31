using System.Collections.Generic;
using System.Linq;
using Recruitment.Logic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Recruitment
{
    /// <summary>Lists the colonists on a map with what each would achieve as an envoy.</summary>
    public class Dialog_SelectEnvoy : Window
    {
        private const float RowHeight = 40f;

        private readonly Map map;

        // The checks below involve reachability, silver counts and the faction lottery; doing them for every
        // colonist every frame is wasteful, so results are reused for a short while.
        private readonly TimedCache<int, List<Pawn>> colonists = new TimedCache<int, List<Pawn>>(RecruitmentUI.CacheSeconds, () => RecruitmentUI.Now);

        private readonly TimedCache<Pawn, AcceptanceReport> reports = new TimedCache<Pawn, AcceptanceReport>(RecruitmentUI.CacheSeconds, () => RecruitmentUI.Now);

        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(720f, 560f);

        public Dialog_SelectEnvoy(Map map)
        {
            this.map = map;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            if (component == null || map == null || !Find.Maps.Contains(map))
            {
                Close();
                return;
            }
            RecruitmentMethodDef method = RecruitmentDefOf.Recruitment_Envoy;
            FloatRange days = method.Days;
            EnvoySocialCurve curve = RecruitmentMod.Settings.EnvoyCurve;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), "Recruitment_Envoy_Title".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 35f, inRect.width, 45f), "Recruitment_Envoy_Explanation".Translate(method.Cost, days.min.ToString("0.#"), days.max.ToString("0.#")));

            List<Pawn> list = colonists.Get(0, () => map.mapPawns.FreeColonistsSpawned.OrderByDescending(RecruitmentUtility.SocialLevel).ToList());
            Rect outRect = new Rect(inRect.x, inRect.y + 85f, inRect.width, inRect.height - 85f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, RowHeight * list.Count);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            for (int i = 0; i < list.Count; i++)
            {
                Rect row = new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 2f);
                // Only rows in view are drawn, so only they pay for their checks.
                if (row.yMax < scrollPosition.y || row.y > scrollPosition.y + outRect.height)
                {
                    continue;
                }
                DrawRow(row, list[i], i, component, method, curve);
            }
            Widgets.EndScrollView();
        }

        private void DrawRow(Rect row, Pawn pawn, int index, GameComponent_Recruitment component, RecruitmentMethodDef method, EnvoySocialCurve curve)
        {
            if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(row);
            }
            if (pawn.Destroyed)
            {
                return;
            }
            Widgets.ThingIcon(new Rect(row.x + 2f, row.y + 2f, RowHeight - 6f, RowHeight - 6f), pawn);
            int social = RecruitmentUtility.SocialLevel(pawn);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + RowHeight + 4f, row.y, 200f, row.height), pawn.LabelShortCap);
            Widgets.Label(new Rect(row.x + RowHeight + 210f, row.y, 260f, row.height), "Recruitment_Envoy_Preview".Translate(social, curve.DurationFactor(social).ToStringPercent(), curve.ZeroChance(social).ToStringPercent()));
            Text.Anchor = TextAnchor.UpperLeft;
            Rect button = new Rect(row.xMax - 140f, row.y + 4f, 136f, row.height - 8f);
            AcceptanceReport report = reports.Get(pawn, () => component.CanDispatchEnvoy(pawn));
            if (!report.Accepted)
            {
                Widgets.ButtonText(button, "Recruitment_Envoy_Send".Translate(), active: false);
                TooltipHandler.TipRegion(button, report.Reason);
                return;
            }
            if (Widgets.ButtonText(button, "Recruitment_Envoy_Send".Translate()))
            {
                TaggedString extra = "Recruitment_Confirm_Envoy".Translate(pawn.Named("PAWN"));
                RecruitmentUI.ConfirmThen(method, () =>
                {
                    if (component.TryDispatchEnvoy(pawn))
                    {
                        Close();
                    }
                    else
                    {
                        reports.Clear();
                    }
                }, extra);
            }
        }
    }
}
