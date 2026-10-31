using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Recruitment
{
    /// <summary>
    /// A normal (non-critical) alert listing envoys who are away. They are held outside every map and caravan,
    /// so the colonist bar cannot show them.
    /// </summary>
    public class Alert_EnvoyAway : Alert
    {
        private readonly List<RecruitmentDrive> away = new List<RecruitmentDrive>();

        // The label is drawn every frame while the alert shows; rebuild it only when the count changes.
        private int labelCount = -1;

        private string label;

        public Alert_EnvoyAway()
        {
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel()
        {
            // GetReport (run every few frames by the alerts readout) keeps the list current.
            if (away.Count != labelCount)
            {
                labelCount = away.Count;
                label = "Recruitment_Alert_EnvoyAway".Translate(labelCount);
            }
            return label;
        }

        public override TaggedString GetExplanation()
        {
            var sb = new StringBuilder("Recruitment_Alert_EnvoyAwayDesc".Translate());
            sb.AppendLine();
            foreach (RecruitmentDrive drive in Away())
            {
                sb.AppendLine();
                sb.Append("  - ").Append(drive.envoy.LabelShortCap).Append(": ");
                sb.Append(drive.TicksUntilArrival > 0
                    ? "Recruitment_Tab_ArrivesIn".Translate(drive.TicksUntilArrival.ToStringTicksToPeriod()).Resolve()
                    : "Recruitment_Alert_EnvoyWaiting".Translate().Resolve());
            }
            return sb.ToString().TrimEndNewlines();
        }

        public override AlertReport GetReport()
        {
            return Away().Count > 0;
        }

        protected override void OnClick()
        {
            Find.MainTabsRoot.SetCurrentTab(RecruitmentDefOf.Recruitment);
        }

        /// <summary>Envoys who have left the map. Cheap: a scan of the few running drives.</summary>
        private List<RecruitmentDrive> Away()
        {
            away.Clear();
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            if (component == null)
            {
                return away;
            }
            foreach (RecruitmentDrive drive in component.Drives)
            {
                if (drive.envoy != null && drive.state == DriveState.Underway)
                {
                    away.Add(drive);
                }
            }
            return away;
        }
    }
}
