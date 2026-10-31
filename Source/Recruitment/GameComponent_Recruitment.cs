using System.Collections.Generic;
using System.Linq;
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
            if (needsOriginLottery && !RecruitmentUtility.EligibleOrigins(checkPermit: true).Any())
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
            Messages.Message("Recruitment_Started".Translate(method.label, drive.durationTicks.ToStringTicksToPeriod()), starter, MessageTypeDefOf.TaskCompletion, historical: false);
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
            return CanStart(RecruitmentDefOf.Recruitment_Envoy, pawn.Map, needsOriginLottery: true);
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
            Depart(drive);
            Messages.Message("Recruitment_EnvoyLeft".Translate(pawn.Named("PAWN"), drive.durationTicks.ToStringTicksToPeriod()), MessageTypeDefOf.NeutralEvent, historical: false);
        }

        private void Depart(RecruitmentDrive drive)
        {
            Pawn pawn = drive.envoy;
            pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.ExitedMap);
            if (pawn.carryTracker?.CarriedThing != null)
            {
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing _);
            }
            pawn.DeSpawnOrDeselect();
            heldPawns.TryAdd(pawn);
            int now = Find.TickManager.TicksGame;
            drive.state = DriveState.Underway;
            drive.startTick = now;
            drive.arrivalTick = now + drive.durationTicks;
        }

        // ---- Ticking ----------------------------------------------------------------------------------

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % CheckInterval != 0)
            {
                return;
            }
            // While a gravship is in flight the colony has no map to arrive at yet; wait for it to land.
            bool gravshipInFlight = Find.CurrentGravship != null;
            foreach (RecruitmentDrive drive in drives.ToList())
            {
                if (drive.state == DriveState.EnvoyDeparting)
                {
                    CheckDepartingEnvoy(drive);
                }
                else if (now >= drive.arrivalTick && !gravshipInFlight)
                {
                    Arrive(drive);
                }
            }
            foreach (ApplicantGroup group in groups.ToList())
            {
                if (now >= group.deadlineTick)
                {
                    Decide(group, new List<Pawn>(), expired: true);
                }
            }
        }

        /// <summary>Cancels and refunds an envoy drive whose envoy stopped walking out before leaving.</summary>
        private void CheckDepartingEnvoy(RecruitmentDrive drive)
        {
            Pawn pawn = drive.envoy;
            if (pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Spawned && pawn.CurJobDef == drive.method.startJob)
            {
                return;
            }
            drives.Remove(drive);
            bool onMap = pawn != null && pawn.Spawned;
            RecruitmentUtility.Refund(onMap ? pawn.Map : drive.map, onMap ? pawn.Position : IntVec3.Invalid, drive.paidSilver);
            Messages.Message("Recruitment_EnvoyCancelled".Translate(pawn?.LabelShort ?? "?", drive.paidSilver), onMap ? pawn : LookTargets.Invalid, MessageTypeDefOf.NegativeEvent, historical: false);
        }

        private void Arrive(RecruitmentDrive drive)
        {
            Map dest = RecruitmentUtility.ResolveDestination(drive.method, drive.map);
            if (dest == null)
            {
                if (drive.envoy != null)
                {
                    // An envoy keeps waiting until there is a home to come back to.
                    if (!drive.envoyWaitingNotified)
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
                RecruitmentUtility.Arrive(returning, dest, drive.method);
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
                RecruitmentUtility.Arrive(returning.Concat(applicants).ToList(), dest, drive.method);
                SendJoinedLetter(applicants, drive.method, returning.FirstOrDefault());
                return;
            }

            if (returning.Count > 0)
            {
                RecruitmentUtility.Arrive(returning, dest, drive.method);
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
            return RecruitmentUtility.EligibleOrigins(checkPermit: false).RandomElementWithFallback();
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
                RecruitmentUtility.Arrive(joining, dest, group.method);
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
                if (drive.envoy == null || !drive.envoy.Spawned)
                {
                    return;
                }
                Depart(drive);
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
