using System.Collections.Generic;
using System.Linq;
using Recruitment.Logic;
using RimWorld;
using Verse;
using Verse.AI;

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
            return map.listerThings.ThingsOfDef(ThingDefOf.Silver)
                .Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer) && (t.IsInAnyStorage() || map.areaManager.Home[t.Position]))
                .ToList();
        }

        public static int CountSpendableSilver(Map map)
        {
            return SpendableSilver(map).Sum(t => t.stackCount);
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
            if (map == null || amount <= 0)
            {
                return;
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

        // ---- Factions -----------------------------------------------------------------------------------

        public static bool IsEligibleOrigin(Faction faction, bool checkPermit)
        {
            List<RoyalTitlePermitDef> permits = TradePermits(faction);
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

        public static List<Faction> EligibleOrigins(bool checkPermit)
        {
            return Find.FactionManager.AllFactionsListForReading.Where(f => IsEligibleOrigin(f, checkPermit)).ToList();
        }

        /// <summary>The royal permits this faction's caravan traders require (the Empire's need Knight or above).</summary>
        private static List<RoyalTitlePermitDef> TradePermits(Faction faction)
        {
            return faction.def.caravanTraderKinds
                .Where(k => k.permitRequiredForTrading != null)
                .Select(k => k.permitRequiredForTrading)
                .Distinct()
                .ToList();
        }

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

        // ---- Applicants ---------------------------------------------------------------------------------

        public static int RollApplicantCount(RecruitmentMethodDef method, int envoySocial)
        {
            List<float> weights = RecruitmentMod.Settings.applicantWeights;
            if (method.UsesEnvoySocial)
            {
                float zeroChance = RecruitmentMod.Settings.EnvoyCurve.ZeroChance(envoySocial);
                return ApplicantRoll.Roll(ApplicantRoll.WithZeroChance(weights, zeroChance), Rand.Value);
            }
            return ApplicantRoll.Roll(weights, Rand.Value);
        }

        public static int RollDurationTicks(RecruitmentMethodDef method, int envoySocial)
        {
            FloatRange days = method.Days;
            float factor = method.UsesEnvoySocial ? RecruitmentMod.Settings.EnvoyCurve.DurationFactor(envoySocial) : 1f;
            return RecruitmentDuration.RollTicks(days.min, days.max, factor, Rand.Value);
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

        public static int SocialLevel(Pawn pawn)
        {
            return pawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
        }

        /// <summary>Whether <paramref name="pawn"/> can be sent as an envoy right now.</summary>
        public static AcceptanceReport CanBeEnvoy(Pawn pawn)
        {
            if (!pawn.Spawned || !pawn.IsFreeNonSlaveColonist || pawn.IsQuestLodger())
            {
                return "Recruitment_EnvoyReason_NotFreeColonist".Translate();
            }
            if (!pawn.DevelopmentalStage.Adult())
            {
                return "Recruitment_EnvoyReason_NotAdult".Translate();
            }
            if (pawn.Downed || pawn.InMentalState)
            {
                return "Recruitment_EnvoyReason_Incapacitated".Translate();
            }
            if (pawn.skills == null || pawn.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
            {
                return "IncapableOfCapacity".Translate(SkillDefOf.Social.label, pawn.Named("PAWN"));
            }
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
            {
                return "IncapableOfCapacity".Translate(PawnCapacityDefOf.Talking.label, pawn.Named("PAWN"));
            }
            if (!RCellFinder.TryFindBestExitSpot(pawn, out _))
            {
                return "Recruitment_EnvoyReason_NoExit".Translate();
            }
            return AcceptanceReport.WasAccepted;
        }
    }
}
