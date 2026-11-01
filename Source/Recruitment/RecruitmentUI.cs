using System;
using System.Linq;
using Recruitment.Logic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Recruitment
{
    internal static class RecruitmentUI
    {
        /// <summary>How long UI checks (reachability, silver, factions) are reused before being recomputed.</summary>
        public const float CacheSeconds = 0.5f;

        public static float Now => Time.realtimeSinceStartup;

        /// <summary>"0: 25% / 1: 25% / ..." for the chances of each applicant count.</summary>
        public static string ChancesText(float[] chances)
        {
            return "Recruitment_Start_Chances".Translate(chances[0].ToStringPercent(), chances[1].ToStringPercent(), chances[2].ToStringPercent(), chances[3].ToStringPercent());
        }

        private static string MapLabel(Map map)
        {
            return map?.Parent?.LabelCap ?? "";
        }

        /// <summary>Asking a visiting trader: the colonist walks over and the drive starts once they have talked.</summary>
        public static void OpenForTrader(RecruitmentMethodDef method, Pawn negotiator, Pawn trader)
        {
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            Map map = negotiator.Map;
            Find.WindowStack.Add(new Dialog_StartRecruitment(new StartPlan
            {
                method = method,
                starter = negotiator,
                fixedOrigin = trader.Faction,
                destinationLabel = MapLabel(map),
                paymentNote = "Recruitment_Start_PayAfterTalk".Translate(negotiator.Named("PAWN")),
                availableSilver = () => RecruitmentUtility.CountSpendableSilver(map),
                check = tier => component.CanStart(method, map, needsOriginLottery: false, tier),
                start = tier =>
                {
                    Job job = JobMaker.MakeJob(method.startJob, trader);
                    // The trader job carries the chosen premium in Job.count, which the game saves with the job.
                    job.count = tier;
                    job.playerForced = true;
                    negotiator.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                },
            }));
        }

        /// <summary>
        /// Broadcasting from the comms console. With <paramref name="faction"/>, the call goes to that faction
        /// directly: its applicants come, and it costs goodwill.
        /// </summary>
        public static void OpenForComms(RecruitmentMethodDef method, Pawn negotiator, Faction faction = null)
        {
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            Map map = negotiator.Map;
            bool direct = faction != null;
            Find.WindowStack.Add(new Dialog_StartRecruitment(new StartPlan
            {
                method = method,
                starter = negotiator,
                fixedOrigin = faction,
                destinationLabel = MapLabel(map),
                goodwillCost = direct ? RecruitmentUtility.DirectGoodwillCost(faction) : 0,
                paymentNote = "Recruitment_Start_PayNow".Translate(),
                availableSilver = () => RecruitmentUtility.CountSpendableSilver(map),
                check = tier => direct ? component.CanRequestDirect(method, map, faction, tier) : component.CanStart(method, map, needsOriginLottery: true, tier),
                start = tier => component.TryStart(method, map, faction, negotiator, tier, direct),
            }));
        }

        /// <summary>
        /// A caravan asking a settlement. <paramref name="start"/> receives the premium: it either sets the caravan
        /// off (paying on arrival) or asks right away when the caravan is already there.
        /// </summary>
        public static void OpenForEnvoy(Caravan caravan, Settlement settlement, Map destination, bool payOnArrival, Action<int> start)
        {
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            RecruitmentMethodDef method = RecruitmentDefOf.Recruitment_Envoy;
            Find.WindowStack.Add(new Dialog_StartRecruitment(new StartPlan
            {
                method = method,
                starter = BestCaravanPawnUtility.FindBestNegotiator(caravan, settlement.Faction, settlement.TraderKind),
                fixedOrigin = settlement.Faction,
                destinationLabel = MapLabel(destination),
                paymentNote = (payOnArrival ? "Recruitment_Start_PayOnArrival" : "Recruitment_Start_PayFromCaravan").Translate(settlement.Label),
                availableSilver = () => RecruitmentUtility.CountCaravanSilver(caravan),
                check = tier => component.CanRequestAt(caravan, settlement, checkSilver: true, destination, destinationSelected: true, tier),
                start = start,
            }));
        }

        public static void ChooseDestination(Action<Map> action)
        {
            var maps = RecruitmentUtility.StartDestinations(RecruitmentDefOf.Recruitment_Envoy);
            if (maps.Count == 0)
            {
                Messages.Message("Recruitment_Reason_NoDestination".Translate(), MessageTypeDefOf.RejectInput, historical: false);
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
