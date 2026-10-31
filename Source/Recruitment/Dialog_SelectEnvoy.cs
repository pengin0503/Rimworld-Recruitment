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

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.OrderByDescending(RecruitmentUtility.SocialLevel).ToList();
            Rect outRect = new Rect(inRect.x, inRect.y + 85f, inRect.width, inRect.height - 85f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, RowHeight * colonists.Count);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                Rect row = new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 2f);
                if (i % 2 == 1)
                {
                    Widgets.DrawLightHighlight(row);
                }
                Widgets.ThingIcon(new Rect(row.x + 2f, row.y + 2f, RowHeight - 6f, RowHeight - 6f), pawn);
                int social = RecruitmentUtility.SocialLevel(pawn);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(row.x + RowHeight + 4f, row.y, 200f, row.height), pawn.LabelShortCap);
                Widgets.Label(new Rect(row.x + RowHeight + 210f, row.y, 260f, row.height), "Recruitment_Envoy_Preview".Translate(social, curve.DurationFactor(social).ToStringPercent(), curve.ZeroChance(social).ToStringPercent()));
                Text.Anchor = TextAnchor.UpperLeft;
                Rect button = new Rect(row.xMax - 140f, row.y + 4f, 136f, row.height - 8f);
                AcceptanceReport report = component.CanDispatchEnvoy(pawn);
                if (report.Accepted)
                {
                    if (Widgets.ButtonText(button, "Recruitment_Envoy_Send".Translate()) && component.TryDispatchEnvoy(pawn))
                    {
                        Close();
                    }
                }
                else
                {
                    Widgets.ButtonText(button, "Recruitment_Envoy_Send".Translate(), active: false);
                    TooltipHandler.TipRegion(button, report.Reason);
                }
            }
            Widgets.EndScrollView();
        }
    }
}
