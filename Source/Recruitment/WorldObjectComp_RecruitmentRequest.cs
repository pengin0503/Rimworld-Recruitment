using System.Collections.Generic;
using Recruitment.Logic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Recruitment
{
    public class WorldObjectCompProperties_RecruitmentRequest : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_RecruitmentRequest()
        {
            compClass = typeof(WorldObjectComp_RecruitmentRequest);
        }
    }

    /// <summary>
    /// Added to settlements by a patch (Patches/Settlement.xml). Offers "ask for settlers" as a caravan
    /// destination, and as a button for a caravan that is already visiting.
    /// </summary>
    [StaticConstructorOnStartup]
    public class WorldObjectComp_RecruitmentRequest : WorldObjectComp
    {
        private static readonly Texture2D RequestIcon = ContentFinder<Texture2D>.Get("UI/Commands/Recruitment_Request");

        // Gizmos are drawn every frame while the caravan is selected; the full check (negotiator, silver, the
        // colony's limit) only needs to run a few times a second.
        private static readonly TimedCache<(int caravan, int settlement), AcceptanceReport> GizmoReports =
            new TimedCache<(int, int), AcceptanceReport>(RecruitmentUI.CacheSeconds, () => RecruitmentUI.Now);

        private Settlement Settlement => parent as Settlement;

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            Settlement settlement = Settlement;
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            if (settlement == null || component == null || !caravan.IsPlayerControlled)
            {
                yield break;
            }
            // Hostile or invalid settlements get no entry at all, like the game's own "offer gifts".
            if (!IsCandidate(settlement))
            {
                yield break;
            }
            string label = RecruitmentFloatMenu.OptionLabel(RecruitmentDefOf.Recruitment_Envoy);
            foreach (FloatMenuOption option in CaravanArrivalActionUtility.GetFloatMenuOptions(
                () => ToFloatMenuReport(component.CanRequestAt(caravan, settlement, checkSilver: true)),
                () => new CaravanArrivalAction_RequestRecruitment(settlement),
                label, caravan, settlement.Tile, settlement,
                confirmActionProxy: go => RecruitmentUI.ConfirmThen(RecruitmentDefOf.Recruitment_Envoy, go)))
            {
                yield return option;
            }
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            Settlement settlement = Settlement;
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            if (settlement == null || component == null || !caravan.IsPlayerControlled || CaravanVisitUtility.SettlementVisitedNow(caravan) != settlement || !IsCandidate(settlement))
            {
                yield break;
            }
            var command = new Command_Action
            {
                defaultLabel = "Recruitment_Command_Request".Translate(),
                defaultDesc = "Recruitment_Command_RequestDesc".Translate(RecruitmentFloatMenu.OptionLabel(RecruitmentDefOf.Recruitment_Envoy)),
                icon = RequestIcon,
                action = () => RecruitmentUI.ConfirmThen(RecruitmentDefOf.Recruitment_Envoy, () => component.TryRequestAt(caravan, settlement)),
            };
            AcceptanceReport report = GizmoReports.Get((caravan.ID, settlement.ID), () => component.CanRequestAt(caravan, settlement, checkSilver: true));
            if (!report.Accepted)
            {
                command.Disable(report.Reason);
            }
            yield return command;
        }

        /// <summary>Settlements an envoy could ever talk to: spawned, on the surface, of a non-hostile other faction.</summary>
        private static bool IsCandidate(Settlement settlement)
        {
            Faction faction = settlement.Faction;
            return settlement.Spawned && !settlement.HasMap && !settlement.Tile.LayerDef.isSpace
                && faction != null && !faction.IsPlayer && !faction.HostileTo(Faction.OfPlayer);
        }

        private static FloatMenuAcceptanceReport ToFloatMenuReport(AcceptanceReport report)
        {
            if (report.Accepted)
            {
                return true;
            }
            return report.Reason.NullOrEmpty() ? FloatMenuAcceptanceReport.WasRejected : FloatMenuAcceptanceReport.WithFailReason(report.Reason);
        }
    }

    /// <summary>Travel to a settlement and ask it to send settlers to the colony.</summary>
    public class CaravanArrivalAction_RequestRecruitment : CaravanArrivalAction
    {
        private Settlement settlement;

        public CaravanArrivalAction_RequestRecruitment()
        {
        }

        public CaravanArrivalAction_RequestRecruitment(Settlement settlement)
        {
            this.settlement = settlement;
        }

        public Settlement Settlement => settlement;

        public override string Label => "Recruitment_Command_Request".Translate();

        public override string ReportString => "Recruitment_CaravanReport".Translate(settlement.Label);

        /// <summary>
        /// Checked every tick while the caravan travels, so only cheap settlement-side conditions are here; the
        /// rest (silver, cooldown, the colony's limit) is checked on arrival, where failing is reported instead of
        /// stopping the caravan in the middle of nowhere.
        /// </summary>
        public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
        {
            FloatMenuAcceptanceReport report = base.StillValid(caravan, destinationTile);
            if (!report)
            {
                return report;
            }
            if (settlement == null || settlement.Tile != destinationTile || !settlement.Spawned || settlement.Faction == null || settlement.Faction.HostileTo(Faction.OfPlayer))
            {
                return false;
            }
            return true;
        }

        public override void Arrived(Caravan caravan)
        {
            GameComponent_Recruitment.Instance?.TryRequestAt(caravan, settlement);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref settlement, "settlement");
        }
    }
}
