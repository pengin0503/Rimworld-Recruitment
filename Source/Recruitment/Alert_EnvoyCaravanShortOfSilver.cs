using System.Collections.Generic;
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
            return "Recruitment_Alert_ShortOfSilverDesc".Translate(RecruitmentDefOf.Recruitment_Envoy.Cost);
        }

        public override AlertReport GetReport()
        {
            culprits.Clear();
            int cost = RecruitmentDefOf.Recruitment_Envoy.Cost;
            List<Caravan> caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++)
            {
                Caravan caravan = caravans[i];
                if (caravan.IsPlayerControlled && caravan.pather.Moving && caravan.pather.ArrivalAction is CaravanArrivalAction_RequestRecruitment
                    && RecruitmentUtility.CountCaravanSilver(caravan) < cost)
                {
                    culprits.Add(caravan);
                }
            }
            return AlertReport.CulpritsAre(culprits);
        }
    }
}
