using System.Collections.Generic;
using System.Linq;
using Recruitment.Logic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Recruitment
{
    public static class RecruitmentUtility
    {
        public static bool IsSpace(this Map map)
        {
            return map.Tile.Valid && map.Tile.LayerDef != null && map.Tile.LayerDef.isSpace;
        }

        public static bool AllowsMap(this RecruitmentMethodDef method, Map map)
        {
            return map.IsSpace() ? method.allowSpace : method.allowSurface;
        }

        // ---- Silver -------------------------------------------------------------------------------------

        /// <summary>Silver the colony can spend on this map: not forbidden, and in storage or in the home area.</summary>
        public static List<Thing> SpendableSilver(Map map)
        {
            var result = new List<Thing>();
            foreach (Thing t in map.listerThings.ThingsOfDef(ThingDefOf.Silver))
            {
                if (IsSpendable(t, map))
                {
                    result.Add(t);
                }
            }
            return result;
        }

        /// <summary>Same as summing <see cref="SpendableSilver"/>, without building a list.</summary>
        public static int CountSpendableSilver(Map map)
        {
            int total = 0;
            List<Thing> silver = map.listerThings.ThingsOfDef(ThingDefOf.Silver);
            for (int i = 0; i < silver.Count; i++)
            {
                if (IsSpendable(silver[i], map))
                {
                    total += silver[i].stackCount;
                }
            }
            return total;
        }

        private static bool IsSpendable(Thing t, Map map)
        {
            return t.Spawned && !t.IsForbidden(Faction.OfPlayer) && (t.IsInAnyStorage() || map.areaManager.Home[t.Position]);
        }

        public static bool TryPay(Map map, int cost)
        {
            if (cost <= 0)
            {
                return true;
            }
            List<Thing> silver = SpendableSilver(map);
            int[] plan = PaymentPlan.Plan(silver.Select(t => t.stackCount).ToList(), cost);
            if (plan == null)
            {
                return false;
            }
            for (int i = 0; i < silver.Count; i++)
            {
                if (plan[i] <= 0)
                {
                    continue;
                }
                if (plan[i] >= silver[i].stackCount)
                {
                    silver[i].Destroy();
                }
                else
                {
                    silver[i].SplitOff(plan[i]).Destroy();
                }
            }
            return true;
        }

        public static void Refund(Map map, IntVec3 near, int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            // The map may be gone (abandoned, or left behind by a gravship); refund to a colony that still exists.
            if (map == null || !Find.Maps.Contains(map))
            {
                map = Find.AnyPlayerHomeMap;
                near = IntVec3.Invalid;
                if (map == null)
                {
                    return;
                }
            }
            if (!near.IsValid || !near.InBounds(map))
            {
                near = DropCellFinder.TradeDropSpot(map);
            }
            while (amount > 0)
            {
                Thing stack = ThingMaker.MakeThing(ThingDefOf.Silver);
                stack.stackCount = System.Math.Min(amount, ThingDefOf.Silver.stackLimit);
                amount -= stack.stackCount;
                GenPlace.TryPlaceThing(stack, near, map, ThingPlaceMode.Near);
            }
        }

        public static int CountCaravanSilver(Caravan caravan)
        {
            int total = 0;
            List<Thing> items = CaravanInventoryUtility.AllInventoryItems(caravan);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].def == ThingDefOf.Silver)
                {
                    total += items[i].stackCount;
                }
            }
            return total;
        }

        public static bool TryPayFromCaravan(Caravan caravan, int cost)
        {
            if (cost <= 0)
            {
                return true;
            }
            if (CountCaravanSilver(caravan) < cost)
            {
                return false;
            }
            int remaining = cost;
            foreach (Thing taken in CaravanInventoryUtility.TakeThings(caravan, t =>
            {
                if (t.def != ThingDefOf.Silver || remaining <= 0)
                {
                    return 0;
                }
                int amount = System.Math.Min(t.stackCount, remaining);
                remaining -= amount;
                return amount;
            }))
            {
                taken.Destroy();
            }
            return true;
        }

        /// <summary>
        /// The warning to show when a caravan leaves for <paramref name="destination"/> without the silver an envoy
        /// request there would cost, or null when there is nothing to warn about (no destination, the settlement
        /// cannot be asked, or the caravan carries enough).
        /// </summary>
        public static string DepartureWarning(PlanetTile destination, List<TransferableOneWay> transferables)
        {
            if (!destination.Valid || transferables == null)
            {
                return null;
            }
            Settlement settlement = Find.WorldObjects.SettlementAt(destination);
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            if (settlement == null || component == null || !component.CanAskSettlement(settlement).Accepted)
            {
                return null;
            }
            int cost = RecruitmentDefOf.Recruitment_Envoy.Cost;
            int silver = 0;
            foreach (TransferableOneWay transferable in transferables)
            {
                if (transferable.CountToTransfer <= 0 || !transferable.HasAnyThing)
                {
                    continue;
                }
                if (transferable.ThingDef == ThingDefOf.Silver)
                {
                    silver += transferable.CountToTransfer;
                }
                else if (transferable.AnyThing is Pawn pawn && pawn.inventory != null)
                {
                    // Silver already in a departing pawn's inventory travels too.
                    silver += pawn.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.Silver);
                }
            }
            if (silver >= cost)
            {
                return null;
            }
            return "Recruitment_DepartureWarning".Translate(settlement.Label, cost, silver);
        }

        // ---- Factions -----------------------------------------------------------------------------------

        public static bool IsEligibleOrigin(Faction faction, bool checkPermit)
        {
            List<RoyalTitlePermitDef> permits = checkPermit ? TradePermits(faction) : NoPermits;
            var traits = new FactionTraits
            {
                IsPlayer = faction.IsPlayer,
                Hidden = faction.Hidden,
                Defeated = faction.defeated,
                Deactivated = faction.deactivated,
                Temporary = faction.temporary,
                Humanlike = faction.def.humanlikeFaction,
                PermanentEnemy = faction.def.permanentEnemy,
                RequiresPermit = checkPermit && permits.Count > 0,
                PlayerHasPermit = checkPermit && permits.Count > 0 && AnyColonistHasPermit(faction, permits),
            };
            return FactionEligibility.IsEligible(traits, RecruitmentMod.Settings.includePermanentEnemies);
        }

        /// <summary>Whether <see cref="EligibleOrigins"/> would be non-empty, stopping at the first match.</summary>
        public static bool AnyEligibleOrigin(bool checkPermit)
        {
            List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < factions.Count; i++)
            {
                if (IsEligibleOrigin(factions[i], checkPermit))
                {
                    return true;
                }
            }
            return false;
        }

        public static List<Faction> EligibleOrigins(bool checkPermit)
        {
            return Find.FactionManager.AllFactionsListForReading.Where(f => IsEligibleOrigin(f, checkPermit)).ToList();
        }

        /// <summary>
        /// The goodwill a direct request to <paramref name="faction"/> actually costs: the fixed
        /// <see cref="DirectRequestRule.GoodwillCost"/>, after the game's own adjustment (a loss is up to 25%
        /// larger while goodwill is above the faction's natural level, as for requesting a trade caravan).
        /// </summary>
        public static int DirectGoodwillCost(Faction faction)
        {
            return -Faction.OfPlayer.CalculateAdjustedGoodwillChange(faction, -DirectRequestRule.GoodwillCost);
        }

        /// <summary>Factions that could ever be asked directly: visible, humanlike, not the player's.</summary>
        public static bool IsDirectRequestCandidate(Faction faction)
        {
            return faction != null && !faction.IsPlayer && !faction.Hidden && !faction.temporary && !faction.defeated && !faction.deactivated && faction.def.humanlikeFaction;
        }

        /// <summary>The royal permits this faction's caravan traders require (the Empire's need Knight or above).</summary>
        private static List<RoyalTitlePermitDef> TradePermits(Faction faction)
        {
            List<RoyalTitlePermitDef> permits = null;
            foreach (TraderKindDef kind in faction.def.caravanTraderKinds)
            {
                RoyalTitlePermitDef permit = kind.permitRequiredForTrading;
                if (permit != null)
                {
                    permits ??= new List<RoyalTitlePermitDef>();
                    if (!permits.Contains(permit))
                    {
                        permits.Add(permit);
                    }
                }
            }
            return permits ?? NoPermits;
        }

        private static readonly List<RoyalTitlePermitDef> NoPermits = new List<RoyalTitlePermitDef>();

        private static bool AnyColonistHasPermit(Faction faction, List<RoyalTitlePermitDef> permits)
        {
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
            {
                if (pawn.royalty != null && permits.Any(p => pawn.royalty.HasPermit(p, faction)))
                {
                    return true;
                }
            }
            return false;
        }

        // ---- Maps ---------------------------------------------------------------------------------------

        /// <summary>Where pawns using <paramref name="method"/> should arrive, preferring <paramref name="original"/>.</summary>
        public static Map ResolveDestination(RecruitmentMethodDef method, Map original)
        {
            var candidates = Find.Maps.Select(m => new MapCandidate(m.uniqueID, m.IsPlayerHome, m.IsSpace(), m.mapPawns.FreeColonistsSpawnedCount)).ToList();
            int? id = DestinationPicker.Pick(candidates, original?.uniqueID ?? -1, method.allowSurface, method.allowSpace);
            return id == null ? null : Find.Maps.FirstOrDefault(m => m.uniqueID == id.Value);
        }

        public static List<Map> StartDestinations(RecruitmentMethodDef method)
        {
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            return Find.Maps.Where(m => m.IsPlayerHome && method.AllowsMap(m)
                && component.ActiveDrivesOn(m) < RecruitmentMod.Settings.maxConcurrentPerMap)
                .OrderByDescending(m => m.mapPawns.FreeColonistsSpawnedCount).ThenBy(m => m.uniqueID).ToList();
        }

        public static Map ResolveStartDestination(RecruitmentMethodDef method)
        {
            var candidates = Find.Maps.Select(m => new MapCandidate(m.uniqueID, m.IsPlayerHome, m.IsSpace(),
                m.mapPawns.FreeColonistsSpawnedCount, GameComponent_Recruitment.Instance.ActiveDrivesOn(m) < RecruitmentMod.Settings.maxConcurrentPerMap));
            int? id = DestinationPicker.Pick(candidates, -1, method.allowSurface, method.allowSpace, requireCapacity: true);
            return id == null ? null : Find.Maps.FirstOrDefault(m => m.uniqueID == id.Value);
        }

        // ---- Applicants ---------------------------------------------------------------------------------

        /// <summary>
        /// Gives a newly recruited applicant the most common ideoligion among living free colonists.
        /// Ties prefer the player's primary ideoligion. With no eligible colonists, that primary
        /// ideoligion is used; without Ideology or a valid ideoligion nothing changes.
        /// </summary>
        public static void MatchColonyIdeology(Pawn pawn)
        {
            if (!ModsConfig.IdeologyActive || pawn?.ideo == null || Find.IdeoManager == null)
            {
                return;
            }
            try
            {
                List<Ideo> activeIdeos = Find.IdeoManager.IdeosListForReading;
                Ideo primary = Faction.OfPlayer?.ideos?.PrimaryIdeo;
                if (primary != null && !activeIdeos.Contains(primary))
                {
                    primary = null;
                }
                Ideo selected = IdeologySelection.MostCommonOrDefault(
                    PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                        .Where(colonist => colonist?.ideo != null && colonist.Ideo != null && activeIdeos.Contains(colonist.Ideo))
                        .Select(colonist => colonist.Ideo), primary);
                if (selected != null && pawn.Ideo != selected)
                {
                    pawn.ideo.SetIdeo(selected);
                }
            }
            catch (System.Exception error)
            {
                Log.ErrorOnce($"[Recruitment] Could not apply colony ideology to applicant {pawn}: {error}", pawn.thingIDNumber ^ 0x4944454F);
            }
        }

        /// <summary>Rolls 0..3 applicants; the starting colonist's Social skill shifts the chances (every method).</summary>
        public static int RollApplicantCount(int social)
        {
            return ApplicantRoll.Roll(RecruitmentMod.Settings.ApplicantChances(social), Rand.Value);
        }

        public static int RollDurationTicks(RecruitmentMethodDef method)
        {
            FloatRange days = method.Days;
            return RecruitmentDuration.RollTicks(days.min, days.max, 1f, Rand.Value);
        }

        /// <summary>
        /// Like <see cref="GenerateApplicant"/>, but falls back to a factionless wanderer if the faction's pawn kind
        /// fails to generate (e.g. a broken modded faction), and returns null if even that fails.
        /// </summary>
        public static Pawn TryGenerateApplicant(Faction faction, Map map)
        {
            try
            {
                return GenerateApplicant(faction, map);
            }
            catch (System.Exception e)
            {
                Log.Error($"[Recruitment] Could not generate an applicant from {faction?.ToStringSafe() ?? "no faction"}: {e}");
            }
            if (faction == null)
            {
                return null;
            }
            try
            {
                return GenerateApplicant(null, map);
            }
            catch (System.Exception e)
            {
                Log.Error($"[Recruitment] Could not generate a factionless applicant either: {e}");
                return null;
            }
        }

        /// <summary>
        /// Generates <paramref name="candidates"/> applicants and keeps the one with the best
        /// <see cref="ApplicantScore"/>; the others are discarded. Pawns are never altered. Returns null if every
        /// candidate failed to generate.
        /// </summary>
        public static Pawn TryGenerateBestApplicant(Faction faction, Map map, int candidates)
        {
            if (candidates <= 1)
            {
                return TryGenerateApplicant(faction, map);
            }
            ScoreWeights weights = RecruitmentMod.Settings.Score;
            var pawns = new List<Pawn>(candidates);
            var scores = new List<float>(candidates);
            for (int i = 0; i < candidates; i++)
            {
                Pawn candidate = TryGenerateApplicant(faction, map);
                if (candidate != null)
                {
                    pawns.Add(candidate);
                    scores.Add(ApplicantScore.Score(FactsOf(candidate), weights));
                }
            }
            int best = ApplicantScore.Best(scores);
            for (int i = 0; i < pawns.Count; i++)
            {
                if (i != best)
                {
                    DiscardCandidate(pawns[i]);
                }
            }
            return best < 0 ? null : pawns[best];
        }

        private static void DiscardCandidate(Pawn pawn)
        {
            try
            {
                if (pawn.IsWorldPawn())
                {
                    Find.WorldPawns.RemovePawn(pawn);
                }
                // The same path as a rejected applicant: destroys the pawn and clears its relations.
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
            }
            catch (System.Exception e)
            {
                Log.Error($"[Recruitment] Could not discard the unused candidate {pawn}: {e}");
            }
        }

        public static ApplicantFacts FactsOf(Pawn pawn)
        {
            var levels = new List<int>();
            var passions = new List<int>();
            if (pawn.skills != null)
            {
                foreach (SkillRecord skill in pawn.skills.skills)
                {
                    levels.Add(skill.TotallyDisabled ? 0 : skill.Level);
                    passions.Add(skill.TotallyDisabled ? 0 : (int)skill.passion);
                }
            }
            return new ApplicantFacts(levels, passions, DisabledWorkTypes(pawn).Count(), HealthProblems(pawn).Count());
        }

        /// <summary>Work types the pawn cannot do, as listed in the applicant dialog and counted by the score.</summary>
        public static IEnumerable<WorkTypeDef> DisabledWorkTypes(Pawn pawn)
        {
            return DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w => w.visible && pawn.WorkTypeIsDisabled(w));
        }

        /// <summary>Visible bad hediffs: what the applicant dialog lists as health problems and the score counts.</summary>
        public static IEnumerable<Hediff> HealthProblems(Pawn pawn)
        {
            return pawn.health.hediffSet.hediffs.Where(h => h.Visible && h.def.isBad);
        }

        /// <summary>Generates an applicant as an ordinary member of <paramref name="faction"/> (or a factionless wanderer).</summary>
        public static Pawn GenerateApplicant(Faction faction, Map map)
        {
            PawnKindDef kind = faction?.def.basicMemberKind;
            if (kind == null || kind.RaceProps == null || !kind.RaceProps.Humanlike)
            {
                kind = PawnKindDefOf.Villager;
            }
            DevelopmentalStage stages = DevelopmentalStage.Adult;
            if (RecruitmentMod.Settings.allowChildren && Find.Storyteller.difficulty.ChildrenAllowed)
            {
                stages |= DevelopmentalStage.Child;
            }
            var request = new PawnGenerationRequest(
                kind,
                faction,
                PawnGenerationContext.NonPlayer,
                map?.Tile,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: true,
                mustBeCapableOfViolence: false,
                colonistRelationChanceFactor: 20f,
                allowPregnant: true,
                forbidAnyTitle: true,
                developmentalStages: stages,
                forceRecruitable: true);
            return PawnGenerator.GeneratePawn(request);
        }

        // ---- Arrival ------------------------------------------------------------------------------------

        /// <summary>Brings unspawned pawns onto <paramref name="map"/> the way <paramref name="method"/> describes.</summary>
        public static void Arrive(List<Pawn> pawns, Map map, RecruitmentMethodDef method)
        {
            if (pawns.Count == 0)
            {
                return;
            }
            PawnsArrivalModeDef mode = method.arrivalModes.FirstOrDefault(m => m.Worker.CanUseOnMap(map));
            if (mode == null)
            {
                DefDatabase<PawnsArrivalModeDef>.AllDefsListForReading.Where(m => m.canBeBackup && m.Worker.CanUseOnMap(map)).TryRandomElement(out mode);
            }
            if (mode == null)
            {
                Log.Warning($"[Recruitment] No usable arrival mode on {map}; placing {pawns.Count} pawn(s) near the trade drop spot.");
                IntVec3 spot = DropCellFinder.TradeDropSpot(map);
                foreach (Pawn pawn in pawns)
                {
                    GenPlace.TryPlaceThing(pawn, spot, map, ThingPlaceMode.Near);
                }
                return;
            }
            var parms = new IncidentParms
            {
                target = map,
                spawnCenter = IntVec3.Invalid,
            };
            mode.Worker.TryResolveRaidSpawnCenter(parms);
            mode.Worker.Arrive(pawns, parms);
        }

        // ---- Envoys -------------------------------------------------------------------------------------

        /// <summary>
        /// Repairs what the game could not update while <paramref name="pawn"/> was held outside every map and the
        /// world (game-wide pawn scans do not see them): deleted policies and a removed ideoligion.
        /// </summary>
        public static void Notify_BackFromAway(Pawn pawn)
        {
            Game game = Current.Game;
            if (pawn.outfits != null && pawn.outfits.CurrentApparelPolicy != null && !game.outfitDatabase.AllOutfits.Contains(pawn.outfits.CurrentApparelPolicy))
            {
                pawn.outfits.CurrentApparelPolicy = null;
            }
            if (pawn.drugs != null && pawn.drugs.CurrentPolicy != null && !game.drugPolicyDatabase.AllPolicies.Contains(pawn.drugs.CurrentPolicy))
            {
                pawn.drugs.CurrentPolicy = null;
            }
            if (pawn.foodRestriction != null && pawn.foodRestriction.CurrentFoodPolicy != null && !game.foodRestrictionDatabase.AllFoodRestrictions.Contains(pawn.foodRestriction.CurrentFoodPolicy))
            {
                pawn.foodRestriction.CurrentFoodPolicy = null;
            }
            if (pawn.reading != null && pawn.reading.CurrentPolicy != null && !game.readingPolicyDatabase.AllReadingPolicies.Contains(pawn.reading.CurrentPolicy))
            {
                pawn.reading.CurrentPolicy = null;
            }
            if (ModsConfig.IdeologyActive && pawn.ideo != null && pawn.Ideo != null && !Find.IdeoManager.IdeosListForReading.Contains(pawn.Ideo))
            {
                Ideo fallback = Faction.OfPlayer.ideos?.PrimaryIdeo;
                if (fallback != null)
                {
                    pawn.ideo.SetIdeo(fallback);
                }
            }
        }

        public static int SocialLevel(Pawn pawn)
        {
            return pawn?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
        }
    }
}
