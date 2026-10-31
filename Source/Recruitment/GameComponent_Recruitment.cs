using System.Collections.Generic;
using System.Linq;
using Recruitment.Logic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Recruitment
{
    /// <summary>
    /// Owns all recruitment state: running drives, applicants waiting for a decision, and envoys who are away.
    /// Away envoys and waiting applicants are held here rather than as world pawns, because a free player
    /// pawn passed to the world is reassigned to another faction by <see cref="Pawn.Notify_PassedToWorld"/>.
    /// </summary>
    public class GameComponent_Recruitment : GameComponent, IThingHolder
    {
        private const int CheckInterval = 250;

        private List<RecruitmentDrive> drives = new List<RecruitmentDrive>();

        private List<ApplicantGroup> groups = new List<ApplicantGroup>();

        private ThingOwner<Pawn> heldPawns;

        private int nextId = 1;

        public GameComponent_Recruitment(Game game)
        {
            heldPawns = new ThingOwner<Pawn>(this, oneStackOnly: false, LookMode.Deep);
        }

        public static GameComponent_Recruitment Instance => Current.Game?.GetComponent<GameComponent_Recruitment>();

        public IReadOnlyList<RecruitmentDrive> Drives => drives;

        public IReadOnlyList<ApplicantGroup> Groups => groups;

        public IThingHolder ParentHolder => null;

        public ThingOwner GetDirectlyHeldThings()
        {
            return heldPawns;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public int ActiveDrivesOn(Map map)
        {
            return drives.Count(d => d.map == map);
        }

        public bool IsEnvoy(Pawn pawn)
        {
            return drives.Any(d => d.envoy == pawn);
        }

        public ApplicantGroup GroupById(int id)
        {
            return groups.FirstOrDefault(g => g.id == id);
        }

        // ---- Starting ---------------------------------------------------------------------------------

        /// <summary>Checks everything about starting <paramref name="method"/> on <paramref name="map"/> except who starts it.</summary>
        public AcceptanceReport CanStart(RecruitmentMethodDef method, Map map, bool needsOriginLottery)
        {
            if (map == null)
            {
                return false;
            }
            if (!method.ResearchFinished)
            {
                return "Recruitment_Reason_Research".Translate(method.MissingResearch.Select(r => r.LabelCap.Resolve()).ToCommaList());
            }
            if (!method.AllowsMap(map))
            {
                return (map.IsSpace() ? "Recruitment_Reason_NotInSpace" : "Recruitment_Reason_OnlyInSpace").Translate();
            }
            int max = RecruitmentMod.Settings.maxConcurrentPerMap;
            if (ActiveDrivesOn(map) >= max)
            {
                return "Recruitment_Reason_Concurrent".Translate(max);
            }
            int silver = RecruitmentUtility.CountSpendableSilver(map);
            if (silver < method.Cost)
            {
                return "Recruitment_Reason_Silver".Translate(method.Cost, silver);
            }
            if (needsOriginLottery && !RecruitmentUtility.AnyEligibleOrigin(checkPermit: true))
            {
                return "Recruitment_Reason_NoFaction".Translate();
            }
            return true;
        }

        /// <summary>Starts a trader or comms drive. <paramref name="fixedOrigin"/> is the trader's faction, or null to draw one.</summary>
        public bool TryStart(RecruitmentMethodDef method, Map map, Faction fixedOrigin, Pawn starter)
        {
            AcceptanceReport report = CanStart(method, map, fixedOrigin == null);
            if (!report.Accepted)
            {
                RejectMessage(report, starter);
                return false;
            }
            Faction origin = fixedOrigin ?? RecruitmentUtility.EligibleOrigins(checkPermit: true).RandomElement();
            int cost = method.Cost;
            if (!RecruitmentUtility.TryPay(map, cost))
            {
                RejectMessage("Recruitment_Reason_Silver".Translate(cost, RecruitmentUtility.CountSpendableSilver(map)), starter);
                return false;
            }
            int now = Find.TickManager.TicksGame;
            var drive = new RecruitmentDrive
            {
                id = nextId++,
                method = method,
                state = DriveState.Underway,
                map = map,
                origin = origin,
                applicantCount = RecruitmentUtility.RollApplicantCount(method, 0),
                paidSilver = cost,
                startTick = now,
                durationTicks = RecruitmentUtility.RollDurationTicks(method, 0),
            };
            drive.arrivalTick = now + drive.durationTicks;
            drives.Add(drive);
            Messages.Message("Recruitment_Started".Translate(method.LabelCap, drive.durationTicks.ToStringTicksToPeriod()), starter, MessageTypeDefOf.TaskCompletion, historical: false);
            return true;
        }

        public AcceptanceReport CanDispatchEnvoy(Pawn pawn)
        {
            if (IsEnvoy(pawn))
            {
                return "Recruitment_EnvoyReason_AlreadyEnvoy".Translate();
            }
            AcceptanceReport report = RecruitmentUtility.CanBeEnvoy(pawn);
            if (!report.Accepted)
            {
                return report;
            }
            if (!AnyColonistStaysBehind(pawn))
            {
                return "Recruitment_EnvoyReason_LastColonist".Translate();
            }
            return CanStart(RecruitmentDefOf.Recruitment_Envoy, pawn.Map, needsOriginLottery: true);
        }

        /// <summary>
        /// Whether some free colonist other than <paramref name="envoy"/> stays on a map. Colonists already
        /// walking out as envoys do not count, or two envoys sent together could leave nobody behind.
        /// </summary>
        private bool AnyColonistStaysBehind(Pawn envoy)
        {
            return PawnsFinder.AllMaps_FreeColonistsSpawned.Any(p => p != envoy && !IsEnvoy(p));
        }

        public bool TryDispatchEnvoy(Pawn pawn)
        {
            AcceptanceReport report = CanDispatchEnvoy(pawn);
            if (!report.Accepted || !RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 exit))
            {
                RejectMessage(report.Accepted ? "Recruitment_EnvoyReason_NoExit".Translate() : report, pawn);
                return false;
            }
            RecruitmentMethodDef method = RecruitmentDefOf.Recruitment_Envoy;
            Map map = pawn.Map;
            int cost = method.Cost;
            if (!RecruitmentUtility.TryPay(map, cost))
            {
                RejectMessage("Recruitment_Reason_Silver".Translate(cost, RecruitmentUtility.CountSpendableSilver(map)), pawn);
                return false;
            }
            int social = RecruitmentUtility.SocialLevel(pawn);
            var drive = new RecruitmentDrive
            {
                id = nextId++,
                method = method,
                state = DriveState.EnvoyDeparting,
                map = map,
                origin = RecruitmentUtility.EligibleOrigins(checkPermit: true).RandomElement(),
                applicantCount = RecruitmentUtility.RollApplicantCount(method, social),
                paidSilver = cost,
                startTick = Find.TickManager.TicksGame,
                durationTicks = RecruitmentUtility.RollDurationTicks(method, social),
                envoy = pawn,
            };
            drives.Add(drive);
            if (pawn.Drafted)
            {
                pawn.drafter.Drafted = false;
            }
            Job job = JobMaker.MakeJob(method.startJob, exit);
            job.playerForced = true;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            Messages.Message("Recruitment_EnvoyDispatched".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.TaskCompletion, historical: false);
            return true;
        }

        /// <summary>Called by the envoy's job when they reach the map edge.</summary>
        public void Notify_EnvoyReachedExit(Pawn pawn)
        {
            RecruitmentDrive drive = drives.FirstOrDefault(d => d.envoy == pawn && d.state == DriveState.EnvoyDeparting);
            if (drive == null)
            {
                return;
            }
            // The others may have died or left while the envoy was walking out.
            if (!AnyColonistStaysBehind(pawn))
            {
                drives.Remove(drive);
                RecruitmentUtility.Refund(pawn.Map, pawn.Position, drive.paidSilver);
                Messages.Message("Recruitment_EnvoyStayed".Translate(pawn.Named("PAWN"), drive.paidSilver), pawn, MessageTypeDefOf.NegativeEvent, historical: false);
                return;
            }
            if (Depart(drive))
            {
                Messages.Message("Recruitment_EnvoyLeft".Translate(pawn.Named("PAWN"), drive.durationTicks.ToStringTicksToPeriod()), MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }

        /// <summary>Takes the envoy off the map and starts the trip. Returns false if the trip had to be called off.</summary>
        private bool Depart(RecruitmentDrive drive)
        {
            Pawn pawn = drive.envoy;
            pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.ExitedMap);
            if (pawn.carryTracker?.CarriedThing != null)
            {
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing _);
            }
            Map map = pawn.Map;
            IntVec3 position = pawn.Position;
            pawn.DeSpawnOrDeselect();
            if (!heldPawns.TryAdd(pawn))
            {
                // Never leave the envoy nowhere: put them back and call the trip off.
                Log.Error($"[Recruitment] Could not hold envoy {pawn}; cancelling the trip.");
                GenSpawn.Spawn(pawn, position, map);
                drives.Remove(drive);
                RecruitmentUtility.Refund(map, position, drive.paidSilver);
                return false;
            }
            int now = Find.TickManager.TicksGame;
            drive.state = DriveState.Underway;
            drive.startTick = now;
            drive.arrivalTick = now + drive.durationTicks;
            drive.lastAgedTick = now;
            return true;
        }

        // ---- Ticking ----------------------------------------------------------------------------------

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            // Deadlines are checked every tick (the list is tiny) so applicants leave exactly when their letter expires.
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                if (i < groups.Count && now >= groups[i].deadlineTick)
                {
                    Decide(groups[i], new List<Pawn>(), expired: true);
                }
            }
            if (now % CheckInterval != 0)
            {
                return;
            }
            foreach (RecruitmentDrive drive in drives.ToList())
            {
                if (drive.state == DriveState.EnvoyDeparting)
                {
                    CheckDepartingEnvoy(drive);
                    continue;
                }
                AgeAwayEnvoy(drive, now);
                if (now >= drive.arrivalTick)
                {
                    Arrive(drive);
                }
            }
            RecoverOrphans();
        }

        /// <summary>Whether the envoy is still on their way out: doing the departure job, or about to (queued behind another job).</summary>
        private static bool IsWalkingOut(Pawn pawn, JobDef departJob)
        {
            if (pawn.CurJobDef == departJob)
            {
                return true;
            }
            foreach (QueuedJob queued in pawn.jobs.jobQueue)
            {
                if (queued.job?.def == departJob)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Cancels and refunds an envoy drive whose envoy stopped walking out before leaving.</summary>
        private void CheckDepartingEnvoy(RecruitmentDrive drive)
        {
            Pawn pawn = drive.envoy;
            if (pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Spawned && IsWalkingOut(pawn, drive.method.startJob))
            {
                return;
            }
            drives.Remove(drive);
            bool onMap = pawn != null && pawn.Spawned;
            RecruitmentUtility.Refund(onMap ? pawn.Map : drive.map, onMap ? pawn.Position : IntVec3.Invalid, drive.paidSilver);
            Messages.Message("Recruitment_EnvoyCancelled".Translate(pawn?.LabelShort ?? "?", drive.paidSilver), onMap ? pawn : LookTargets.Invalid, MessageTypeDefOf.NegativeEvent, historical: false);
        }

        /// <summary>Ages an away envoy the way the game ages idle world pawns, in whole mothball intervals.</summary>
        private void AgeAwayEnvoy(RecruitmentDrive drive, int now)
        {
            Pawn envoy = drive.envoy;
            if (envoy == null || !heldPawns.Contains(envoy))
            {
                return;
            }
            int due = MothballSchedule.DueTicks(drive.lastAgedTick, now, MothballSchedule.Interval);
            for (int ticks = 0; ticks < due; ticks += MothballSchedule.Interval)
            {
                envoy.TickMothballed(MothballSchedule.Interval);
            }
            drive.lastAgedTick += due;
        }

        /// <summary>
        /// Held pawns that no drive or group refers to any more (e.g. after a def went missing on load, or after
        /// an arrival that failed) would otherwise be lost: colonists are brought home, others are discarded.
        /// </summary>
        private void RecoverOrphans()
        {
            if (heldPawns.Count == 0)
            {
                return;
            }
            var referenced = new HashSet<Pawn>();
            foreach (RecruitmentDrive drive in drives)
            {
                if (drive.envoy != null)
                {
                    referenced.Add(drive.envoy);
                }
            }
            foreach (ApplicantGroup group in groups)
            {
                referenced.UnionWith(group.pawns);
            }
            foreach (Pawn pawn in heldPawns.InnerListForReading.ToList())
            {
                if (referenced.Contains(pawn))
                {
                    continue;
                }
                if (pawn.Faction == Faction.OfPlayer && !pawn.Dead)
                {
                    Map home = Find.AnyPlayerHomeMap;
                    if (home == null)
                    {
                        continue;
                    }
                    Log.WarningOnce($"[Recruitment] Bringing back {pawn}, who was held without a recruitment drive.", pawn.thingIDNumber ^ 0x4F525048);
                    heldPawns.Remove(pawn);
                    BringHome(new List<Pawn> { pawn }, home, RecruitmentDefOf.Recruitment_Envoy);
                }
                else
                {
                    heldPawns.Remove(pawn);
                    Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
                }
            }
        }

        /// <summary>
        /// Puts pawns taken out of <see cref="heldPawns"/> onto <paramref name="map"/>. Whatever goes wrong, every
        /// pawn ends up either spawned or back in <see cref="heldPawns"/> (and then recovered later), never nowhere.
        /// </summary>
        private void BringHome(List<Pawn> pawns, Map map, RecruitmentMethodDef method)
        {
            if (pawns.Count == 0)
            {
                return;
            }
            foreach (Pawn pawn in pawns)
            {
                if (pawn.Faction == Faction.OfPlayer)
                {
                    RecruitmentUtility.Notify_BackFromAway(pawn);
                }
            }
            try
            {
                RecruitmentUtility.Arrive(pawns, map, method);
            }
            catch (System.Exception e)
            {
                Log.ErrorOnce($"[Recruitment] Arrival on {map} failed; placing the pawns directly. {e}", pawns[0].thingIDNumber ^ 0x41525256);
            }
            foreach (Pawn pawn in pawns)
            {
                if (pawn.Spawned || pawn.Destroyed || pawn.holdingOwner != null)
                {
                    continue;
                }
                try
                {
                    GenPlace.TryPlaceThing(pawn, DropCellFinder.TradeDropSpot(map), map, ThingPlaceMode.Near);
                }
                catch (System.Exception e)
                {
                    // Retried every check while it keeps failing, so report it once per pawn.
                    Log.ErrorOnce($"[Recruitment] Could not place {pawn}; keeping them to try again later. {e}", pawn.thingIDNumber ^ 0x52454352);
                }
                if (!pawn.Spawned && !pawn.Destroyed && pawn.holdingOwner == null)
                {
                    heldPawns.TryAdd(pawn);
                }
            }
        }

        private void Arrive(RecruitmentDrive drive)
        {
            Map dest = RecruitmentUtility.ResolveDestination(drive.method, drive.map);
            if (dest == null)
            {
                // A gravship in flight has no map yet, and an envoy needs a home to come back to: both wait.
                bool gravshipInFlight = Find.CurrentGravship != null;
                if (drive.envoy != null || gravshipInFlight)
                {
                    if (drive.envoy != null && !drive.envoyWaitingNotified && !gravshipInFlight)
                    {
                        drive.envoyWaitingNotified = true;
                        Messages.Message("Recruitment_EnvoyWaiting".Translate(drive.envoy.Named("PAWN")), MessageTypeDefOf.NeutralEvent);
                    }
                    return;
                }
                drives.Remove(drive);
                Find.LetterStack.ReceiveLetter("Recruitment_LetterFailedLabel".Translate(), "Recruitment_LetterNoDestination".Translate(drive.method.label), LetterDefOf.NegativeEvent);
                return;
            }
            drives.Remove(drive);

            var returning = new List<Pawn>();
            if (drive.envoy != null && heldPawns.Contains(drive.envoy))
            {
                heldPawns.Remove(drive.envoy);
                drive.envoy.TickMothballed(MothballSchedule.RemainderTicks(drive.lastAgedTick, Find.TickManager.TicksGame));
                returning.Add(drive.envoy);
            }

            Faction origin = drive.applicantCount > 0 ? ValidOrigin(drive) : null;
            var applicants = new List<Pawn>();
            for (int i = 0; i < drive.applicantCount; i++)
            {
                // Generation failures are logged and skipped, so the envoy still comes home below.
                Pawn pawn = RecruitmentUtility.TryGenerateApplicant(origin, dest);
                if (pawn == null)
                {
                    continue;
                }
                if (pawn.IsWorldPawn())
                {
                    Find.WorldPawns.RemovePawn(pawn);
                }
                applicants.Add(pawn);
            }

            if (applicants.Count == 0)
            {
                BringHome(returning, dest, drive.method);
                TaggedString text = "Recruitment_LetterNoApplicants".Translate(drive.method.label);
                if (returning.Count > 0)
                {
                    text += "\n\n" + "Recruitment_EnvoyReturnedAlone".Translate(returning[0].Named("PAWN"));
                }
                Find.LetterStack.ReceiveLetter("Recruitment_LetterFailedLabel".Translate(), text, LetterDefOf.NegativeEvent, new LookTargets(returning));
                return;
            }

            if (RecruitmentMod.Settings.joinMode == JoinMode.AutoJoin)
            {
                foreach (Pawn pawn in applicants)
                {
                    pawn.SetFaction(Faction.OfPlayer);
                }
                BringHome(returning.Concat(applicants).ToList(), dest, drive.method);
                SendJoinedLetter(applicants, drive.method, returning.FirstOrDefault());
                return;
            }

            if (returning.Count > 0)
            {
                BringHome(returning, dest, drive.method);
                Messages.Message("Recruitment_EnvoyReturned".Translate(returning[0].Named("PAWN")), returning[0], MessageTypeDefOf.PositiveEvent);
            }
            foreach (Pawn pawn in applicants)
            {
                heldPawns.TryAdd(pawn);
            }
            var group = new ApplicantGroup
            {
                id = nextId++,
                method = drive.method,
                map = dest,
                pawns = applicants,
                deadlineTick = Find.TickManager.TicksGame + RecruitmentMod.Settings.DecisionTicks,
            };
            groups.Add(group);
            Letter_Applicants.Send(group, origin);
        }

        /// <summary>The drive's faction if it is still acceptable; otherwise another one, or null for a factionless wanderer.</summary>
        private static Faction ValidOrigin(RecruitmentDrive drive)
        {
            // Permits were checked when the drive started; losing a title later does not cancel it.
            if (drive.origin != null && RecruitmentUtility.IsEligibleOrigin(drive.origin, checkPermit: false))
            {
                return drive.origin;
            }
            // A newly drawn faction is a new choice, so it must pass the permit check like at the start.
            return RecruitmentUtility.EligibleOrigins(checkPermit: true).RandomElementWithFallback();
        }

        // ---- Decisions --------------------------------------------------------------------------------

        /// <summary>
        /// Lets the <paramref name="accepted"/> applicants join and sends the rest away. Fails without changing
        /// anything if someone was accepted but no map can take them (unless the decision <paramref name="expired"/>).
        /// </summary>
        public AcceptanceReport Decide(ApplicantGroup group, ICollection<Pawn> accepted, bool expired)
        {
            List<Pawn> joining = group.pawns.Where(p => accepted.Contains(p) && !p.Destroyed).ToList();
            Map dest = null;
            if (joining.Count > 0)
            {
                dest = RecruitmentUtility.ResolveDestination(group.method, group.map);
                if (dest == null)
                {
                    if (!expired)
                    {
                        return "Recruitment_Reason_NoDestination".Translate();
                    }
                    joining.Clear();
                }
            }

            groups.Remove(group);
            foreach (Letter letter in Find.LetterStack.LettersListForReading.OfType<Letter_Applicants>().Where(l => l.groupId == group.id).ToList())
            {
                Find.LetterStack.RemoveLetter(letter);
            }
            foreach (Pawn pawn in group.pawns)
            {
                if (heldPawns.Contains(pawn))
                {
                    heldPawns.Remove(pawn);
                }
            }

            foreach (Pawn pawn in joining)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }
            if (joining.Count > 0)
            {
                BringHome(joining, dest, group.method);
                SendJoinedLetter(joining, group.method, null);
            }

            List<Pawn> leaving = group.pawns.Where(p => !joining.Contains(p) && !p.Destroyed).ToList();
            foreach (Pawn pawn in leaving)
            {
                PawnDiedOrDownedThoughtsUtility.TryGiveThoughts(pawn, null, PawnDiedOrDownedThoughtsKind.DeniedJoining);
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
            }
            if (expired && leaving.Count > 0)
            {
                Messages.Message("Recruitment_ApplicantsLeft".Translate(leaving.Count), MessageTypeDefOf.NeutralEvent);
            }
            return true;
        }

        private static void SendJoinedLetter(List<Pawn> joined, RecruitmentMethodDef method, Pawn returningEnvoy)
        {
            TaggedString text = "Recruitment_LetterJoined".Translate(method.label, joined.Select(p => p.NameFullColored.Resolve()).ToLineList("  - "));
            if (returningEnvoy != null)
            {
                text += "\n\n" + "Recruitment_EnvoyReturnedWith".Translate(returningEnvoy.Named("PAWN"));
            }
            Find.LetterStack.ReceiveLetter("Recruitment_LetterJoinedLabel".Translate(joined.Count), text, LetterDefOf.PositiveEvent, new LookTargets(joined));
        }

        // ---- Debug ------------------------------------------------------------------------------------

        public void DevArriveNow(RecruitmentDrive drive)
        {
            if (drive.state == DriveState.EnvoyDeparting)
            {
                if (drive.envoy == null || !drive.envoy.Spawned || !Depart(drive))
                {
                    return;
                }
            }
            Arrive(drive);
        }

        // ---- Saving -----------------------------------------------------------------------------------

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref heldPawns, "heldPawns", this);
            Scribe_Collections.Look(ref drives, "drives", LookMode.Deep);
            Scribe_Collections.Look(ref groups, "groups", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                heldPawns ??= new ThingOwner<Pawn>(this, oneStackOnly: false, LookMode.Deep);
                drives ??= new List<RecruitmentDrive>();
                groups ??= new List<ApplicantGroup>();
                drives.RemoveAll(d => d?.method == null);
                groups.RemoveAll(g => g?.method == null);
            }
        }

        private static void RejectMessage(AcceptanceReport report, Pawn pawn)
        {
            if (!report.Reason.NullOrEmpty())
            {
                Messages.Message(report.Reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
            }
        }
    }
}
