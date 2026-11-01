using System.Collections.Generic;
using System.Linq;
using Recruitment.Logic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Recruitment
{
    /// <summary>
    /// Overview of running drives (with progress), waiting applicants (with the time left), whether each method can
    /// be used from the current map, which factions can be asked directly, and the history.
    /// </summary>
    public class MainTabWindow_Recruitment : MainTabWindow
    {
        private const float RowHeight = 40f;

        private const float IconSize = 32f;

        private const float BarWidth = 210f;

        private const float ButtonWidth = 120f;

        private Vector2 scrollPosition;

        private float viewHeight = 400f;

        private readonly Dictionary<RecruitmentHistoryEntry, string> historyText = new Dictionary<RecruitmentHistoryEntry, string>();

        // Silver counts, research lookups, faction checks and translations do not need to run every frame.
        private readonly TimedCache<int, List<MethodRow>> methodRows = new TimedCache<int, List<MethodRow>>(RecruitmentUI.CacheSeconds, () => RecruitmentUI.Now);

        private readonly TimedCache<int, List<FactionRow>> factionRows = new TimedCache<int, List<FactionRow>>(RecruitmentUI.CacheSeconds, () => RecruitmentUI.Now);

        private sealed class MethodRow
        {
            public RecruitmentMethodDef method;
            public string label;
            public string status;
            public string howTo;
        }

        private sealed class FactionRow
        {
            public Faction faction;
            public string label;
            public string status;
            public string cooldowns;
        }

        public override Vector2 RequestedTabSize => new Vector2(860f, 640f);

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            if (component == null)
            {
                return;
            }
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            float y = 0f;
            float width = viewRect.width;

            Heading(ref y, width, "Recruitment_Tab_Active");
            if (component.Drives.Count == 0)
            {
                Line(ref y, width, "Recruitment_Tab_None".Translate());
            }
            // By index: the dev button can finish a drive (removing it) while the list is drawn.
            for (int i = 0; i < component.Drives.Count; i++)
            {
                RecruitmentDrive drive = component.Drives[i];
                if (DrawDrive(new Rect(0f, y, width, RowHeight), component, drive, i))
                {
                    break;
                }
                y += RowHeight + 4f;
            }

            Heading(ref y, width, "Recruitment_Tab_Waiting");
            if (component.Groups.Count == 0)
            {
                Line(ref y, width, "Recruitment_Tab_None".Translate());
            }
            for (int i = 0; i < component.Groups.Count; i++)
            {
                DrawGroup(new Rect(0f, y, width, RowHeight), component.Groups[i], i);
                y += RowHeight + 4f;
            }

            Map map = Find.CurrentMap;
            Heading(ref y, width, "Recruitment_Tab_Methods");
            Line(ref y, width, "Recruitment_Tab_MethodsOn".Translate(map?.Parent?.LabelCap ?? "Recruitment_History_Unknown".Translate().Resolve()).Resolve().Colorize(ColoredText.SubtleGrayColor));
            foreach (MethodRow row in methodRows.Get(map?.uniqueID ?? -1, () => BuildMethodRows(component, map)))
            {
                DrawMethod(ref y, width, row);
            }

            Heading(ref y, width, "Recruitment_Tab_Factions");
            List<FactionRow> factions = factionRows.Get(0, () => BuildFactionRows(component));
            if (factions.Count == 0)
            {
                Line(ref y, width, "Recruitment_Tab_None".Translate());
            }
            foreach (FactionRow row in factions)
            {
                DrawFaction(ref y, width, row);
            }

            Heading(ref y, width, "Recruitment_History_Title");
            if (component.History.Count == 0)
            {
                Line(ref y, width, "Recruitment_Tab_None".Translate());
            }
            if (historyText.Count > GameComponent_Recruitment.HistoryLimit * 2)
            {
                historyText.Clear();
            }
            for (int i = component.History.Count - 1; i >= 0; i--)
            {
                RecruitmentHistoryEntry entry = component.History[i];
                if (!historyText.TryGetValue(entry, out string description))
                {
                    description = HistoryDescription(entry);
                    historyText[entry] = description;
                }
                Line(ref y, width, description);
                Widgets.DrawLineHorizontal(0f, y + 2f, width, Widgets.SeparatorLineColor);
                y += 6f;
            }

            viewHeight = y + 10f;
            Widgets.EndScrollView();
        }

        // ---- Drives and waiting applicants -------------------------------------------------------------

        /// <summary>Draws one running drive. Returns true if the dev button finished it (the list changed).</summary>
        private static bool DrawDrive(Rect row, GameComponent_Recruitment component, RecruitmentDrive drive, int index)
        {
            if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(row);
            }
            Rect icon = new Rect(row.x + 4f, row.y + (row.height - IconSize) / 2f, IconSize, IconSize);
            GUI.DrawTexture(icon, drive.method.Icon);
            float right = row.xMax - 4f;
            bool finished = false;
            if (Prefs.DevMode)
            {
                Rect dev = new Rect(right - ButtonWidth, row.y + 5f, ButtonWidth, row.height - 10f);
                if (Widgets.ButtonText(dev, "DEV: arrive now"))
                {
                    component.DevArriveNow(drive);
                    finished = true;
                }
                TooltipHandler.TipRegion(dev, "DEV: " + drive.applicantCount + " applicant(s)");
                right = dev.x - 6f;
            }
            Rect bar = new Rect(right - BarWidth, row.y + 8f, BarWidth, row.height - 16f);
            Bar(bar, drive.Progress, "Recruitment_Tab_ArrivesIn".Translate(Mathf.Max(0, drive.TicksUntilArrival).ToStringTicksToPeriod()));

            string where = drive.map != null && Find.Maps.Contains(drive.map) ? drive.map.Parent.LabelCap : "Recruitment_Tab_MapLost".Translate().Resolve();
            string origin = drive.origin != null ? drive.origin.Name : "Recruitment_FromNowhere".Translate().Resolve();
            string title = drive.method.LabelCap;
            if (drive.direct)
            {
                title += " " + "Recruitment_Tab_DirectMark".Translate();
            }
            string details = "Recruitment_Tab_DriveDetails".Translate(origin, where, ("Recruitment_Premium_" + Dialog_StartRecruitment.TierKey(drive.premiumTier)).Translate());
            Rect text = new Rect(icon.xMax + 8f, row.y, bar.x - icon.xMax - 14f, row.height);
            TwoLines(text, title, details);
            return finished;
        }

        private static void DrawGroup(Rect row, ApplicantGroup group, int index)
        {
            if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(row);
            }
            Rect icon = new Rect(row.x + 4f, row.y + (row.height - IconSize) / 2f, IconSize, IconSize);
            GUI.DrawTexture(icon, group.method.Icon);
            Rect button = new Rect(row.xMax - 4f - ButtonWidth, row.y + 5f, ButtonWidth, row.height - 10f);
            if (Widgets.ButtonText(button, "Recruitment_Tab_Review".Translate()))
            {
                RecruitmentUI.OpenApplicants(group);
            }
            Rect bar = new Rect(button.x - 6f - BarWidth, row.y + 8f, BarWidth, row.height - 16f);
            int left = Mathf.Max(0, group.TicksLeft);
            Bar(bar, Mathf.Clamp01((float)left / RecruitmentMod.Settings.DecisionTicks), "Recruitment_Tab_TimeLeft".Translate(left.ToStringTicksToPeriod()));
            Rect text = new Rect(icon.xMax + 8f, row.y, bar.x - icon.xMax - 14f, row.height);
            TwoLines(text, "Recruitment_Tab_GroupTitle".Translate(group.pawns.Count), group.method.LabelCap);
        }

        // ---- Methods ---------------------------------------------------------------------------------------

        private static List<MethodRow> BuildMethodRows(GameComponent_Recruitment component, Map map)
        {
            var rows = new List<MethodRow>();
            foreach (RecruitmentMethodDef method in RecruitmentMethodDef.AllInOrder)
            {
                FloatRange days = method.Days;
                AcceptanceReport report = component.Availability(method, map);
                rows.Add(new MethodRow
                {
                    method = method,
                    label = "Recruitment_Tab_MethodLine".Translate(method.LabelCap, method.Cost, days.min.ToString("0.#"), days.max.ToString("0.#")),
                    status = report.Accepted
                        ? "Recruitment_Tab_Usable".Translate().Resolve().Colorize(ColorLibrary.Green)
                        : (report.Reason.NullOrEmpty() ? "Recruitment_Tab_Unusable".Translate().Resolve() : report.Reason).Colorize(ColorLibrary.RedReadable),
                    howTo = ("Recruitment_Tab_HowTo_" + method.startKind).Translate(),
                });
            }
            return rows;
        }

        private static void DrawMethod(ref float y, float width, MethodRow row)
        {
            float textWidth = width - IconSize - 12f;
            string body = row.status + "\n" + row.howTo.Colorize(ColoredText.SubtleGrayColor);
            Text.Font = GameFont.Small;
            float height = Mathf.Max(IconSize, Text.CalcHeight(row.label, textWidth) + Text.CalcHeight(body, textWidth)) + 6f;
            GUI.DrawTexture(new Rect(4f, y + 2f, IconSize, IconSize), row.method.Icon);
            float labelHeight = Text.CalcHeight(row.label, textWidth);
            Widgets.Label(new Rect(IconSize + 12f, y, textWidth, labelHeight), row.label);
            Widgets.Label(new Rect(IconSize + 12f, y + labelHeight, textWidth, height - labelHeight), body);
            y += height + 4f;
        }

        // ---- Factions --------------------------------------------------------------------------------------

        private static List<FactionRow> BuildFactionRows(GameComponent_Recruitment component)
        {
            var rows = new List<FactionRow>();
            foreach (Faction faction in Find.FactionManager.AllFactionsVisibleInViewOrder)
            {
                if (!RecruitmentUtility.IsDirectRequestCandidate(faction))
                {
                    continue;
                }
                AcceptanceReport report = component.CanAskFaction(faction);
                string status;
                if (component.HasPendingDirectRequest(faction))
                {
                    status = "Recruitment_Tab_DirectPending".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
                }
                else if (report.Accepted)
                {
                    status = "Recruitment_Tab_DirectAvailable".Translate(RecruitmentUtility.DirectGoodwillCost(faction)).Resolve().Colorize(ColorLibrary.Green);
                }
                else
                {
                    status = (report.Reason.NullOrEmpty() ? "Recruitment_Tab_Unusable".Translate().Resolve() : report.Reason).Colorize(ColorLibrary.RedReadable);
                }
                List<(RimWorld.Planet.Settlement settlement, int ticks)> cooldowns = component.SettlementsOnCooldown(faction);
                rows.Add(new FactionRow
                {
                    faction = faction,
                    label = "Recruitment_Tab_FactionLine".Translate(faction.Name.Colorize(faction.Color), faction.PlayerGoodwill.ToStringWithSign(), faction.PlayerRelationKind.GetLabelCap()),
                    status = status,
                    cooldowns = cooldowns.Count == 0 ? null
                        : "Recruitment_Tab_Cooldowns".Translate(string.Join(", ", cooldowns.Select(c => "Recruitment_Tab_CooldownEntry".Translate(c.settlement.LabelCap, c.ticks.ToStringTicksToPeriod()).Resolve()))),
                });
            }
            return rows;
        }

        private static void DrawFaction(ref float y, float width, FactionRow row)
        {
            float textWidth = width - IconSize - 12f;
            string body = row.status + (row.cooldowns != null ? "\n" + row.cooldowns : "");
            Text.Font = GameFont.Small;
            float labelHeight = Text.CalcHeight(row.label, textWidth);
            float height = Mathf.Max(IconSize, labelHeight + Text.CalcHeight(body, textWidth)) + 6f;
            Rect icon = new Rect(4f, y + 2f, IconSize, IconSize);
            GUI.color = row.faction.Color;
            GUI.DrawTexture(icon, row.faction.def.FactionIcon);
            GUI.color = Color.white;
            Widgets.Label(new Rect(IconSize + 12f, y, textWidth, labelHeight), row.label);
            Widgets.Label(new Rect(IconSize + 12f, y + labelHeight, textWidth, height - labelHeight), body);
            y += height + 4f;
        }

        // ---- Drawing helpers -------------------------------------------------------------------------------

        private static void Bar(Rect rect, float fill, string label)
        {
            Widgets.FillableBar(rect, fill);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static void TwoLines(Rect rect, string first, string second)
        {
            Text.Font = GameFont.Small;
            Text.WordWrap = false;
            Widgets.Label(new Rect(rect.x, rect.y + 1f, rect.width, 22f), first.Truncate(rect.width));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, rect.y + 21f, rect.width, 18f), second.Truncate(rect.width));
            Text.WordWrap = true;
            Text.Font = GameFont.Small;
        }

        private static void Line(ref float y, float width, string text)
        {
            Text.Font = GameFont.Small;
            float height = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(0f, y, width, height), text);
            y += height + 2f;
        }

        private static void Heading(ref float y, float width, string key)
        {
            y += 6f;
            Text.Font = GameFont.Medium;
            string text = key.Translate();
            float height = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(0f, y, width, height), text);
            Text.Font = GameFont.Small;
            y += height;
            Widgets.DrawLineHorizontal(0f, y + 2f, width, Widgets.SeparatorLineColor);
            y += 8f;
        }

        private static string HistoryDescription(RecruitmentHistoryEntry entry)
        {
            string unknown = "Recruitment_History_Unknown".Translate();
            string result = entry.outcome switch
            {
                RecruitmentOutcome.AutoJoined => "Recruitment_History_Result_AutoJoined".Translate(),
                RecruitmentOutcome.Expired => "Recruitment_History_Result_Expired".Translate(),
                RecruitmentOutcome.NoApplicants => "Recruitment_History_Result_NoApplicants".Translate(),
                RecruitmentOutcome.NoDestination => "Recruitment_History_Result_NoDestination".Translate(),
                RecruitmentOutcome.GenerationFailed => "Recruitment_History_Result_GenerationFailed".Translate(),
                RecruitmentOutcome.RecoveryPending => "Recruitment_History_Result_RecoveryPending".Translate(),
                _ => "Recruitment_History_Result_Completed".Translate(),
            };
            string origin = entry.originName.NullOrEmpty() ? unknown : entry.originName;
            string planned = entry.plannedMap.NullOrEmpty() ? unknown : entry.plannedMap;
            string actual = entry.actualMap.NullOrEmpty() ? unknown : entry.actualMap;
            return "Recruitment_History_Summary".Translate(entry.methodLabel, result, entry.paidSilver < 0 ? unknown : entry.paidSilver.ToString())
                + "\n" + "Recruitment_History_Route".Translate(origin, planned, actual)
                + "\n" + "Recruitment_History_Counts".Translate(entry.arrived, entry.joined, entry.rejected, entry.expired, entry.generationFailures)
                + "\n" + "Recruitment_History_Times".Translate(entry.startTick < 0 ? unknown : (entry.startTick / 60000f + 1f).ToString("0.##"), entry.completionTick < 0 ? unknown : (entry.completionTick / 60000f + 1f).ToString("0.##"));
        }
    }
}
