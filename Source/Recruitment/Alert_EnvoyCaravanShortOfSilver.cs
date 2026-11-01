using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Recruitment
{
    /// <summary>
    /// A normal (non-critical) alert for caravans heading off to ask for settlers that no longer carry enough
    /// silver for the request (sold, dropped or lost on the way).
    /// </summary>
    public class Alert_EnvoyCaravanShortOfSilver : Alert
    {
        private readonly List<Caravan> culprits = new List<Caravan>();

        public Alert_EnvoyCaravanShortOfSilver()
        {
            defaultLabel = "Recruitment_Alert_ShortOfSilver".Translate();
            defaultPriority = AlertPriority.Medium;
        }

        public override TaggedString GetExplanation()
        {
            return "Recruitment_Alert_ShortOfSilverDesc".Translate(string.Join("\n", culprits.Select(c => "  - " + "Recruitment_Alert_ShortOfSilverLine".Translate(c.LabelCap, NeededSilver(c), RecruitmentUtility.CountCaravanSilver(c)).Resolve())));
        }

        private static int NeededSilver(Caravan caravan)
        {
            return (caravan.pather.ArrivalAction as CaravanArrivalAction_RequestRecruitment)?.Cost ?? RecruitmentDefOf.Recruitment_Envoy.Cost;
        }

        public override AlertReport GetReport()
        {
            culprits.Clear();
            List<Caravan> caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++)
            {
                Caravan caravan = caravans[i];
                // The premium chosen for the trip is part of what the caravan must carry.
                if (caravan.IsPlayerControlled && caravan.pather.Moving && caravan.pather.ArrivalAction is CaravanArrivalAction_RequestRecruitment request
                    && RecruitmentUtility.CountCaravanSilver(caravan) < request.Cost)
                {
                    culprits.Add(caravan);
                }
            }
            return AlertReport.CulpritsAre(culprits);
        }
    }
}
