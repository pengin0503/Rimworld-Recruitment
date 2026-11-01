using System.Collections.Generic;
using System.Linq;
using Recruitment.Logic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Recruitment
{
    /// <summary>
    /// Owns all recruitment state: running drives, applicants waiting for a decision and settlement cooldowns.
    /// Waiting applicants are held here (not spawned, not world pawns) until the player decides on them.
    /// </summary>
    public partial class GameComponent_Recruitment : GameComponent, IThingHolder
    {
        private const int CheckInterval = 250;

        private List<RecruitmentDrive> drives = new List<RecruitmentDrive>();

        private List<ApplicantGroup> groups = new List<ApplicantGroup>();

        private ThingOwner<Pawn> heldPawns;

        /// <summary>Settlement ID to the tick an envoy last asked there.</summary>
        private Dictionary<int, int> settlementRequests = new Dictionary<int, int>();

        private RecruitmentCommTarget commTarget = new RecruitmentCommTarget();

        private int nextId = 1;

        /// <summary>
        /// Set when held pawns may have lost their drive or group (after loading, or after a pawn could not be
        /// placed), so the orphan scan only runs when it can find something.
        /// </summary>
        private bool orphanCheckPending = true;

        /// <summary>Older saves may contain envoys from before envoys travelled by caravan; they are settled once after loading.</summary>
        private bool legacyCheckPending = true;

        public GameComponent_Recruitment(Game game)
        {
            heldPawns = new ThingOwner<Pawn>(this, oneStackOnly: false, LookMode.Deep);
        }

        public static GameComponent_Recruitment Instance => Current.Game?.GetComponent<GameComponent_Recruitment>();

        public IReadOnlyList<RecruitmentDrive> Drives => drives;

        public IReadOnlyList<ApplicantGroup> Groups => groups;

        /// <summary>The comms console target that opens the recruitment broadcast dialog.</summary>
        public RecruitmentCommTarget CommTarget => commTarget;

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

        public ApplicantGroup GroupById(int id)
        {
            return groups.FirstOrDefault(g => g.id == id);
        }

        // ---- Starting from the colony -----------------------------------------------------------------

        /// <summary>
        /// Checks everything about starting <paramref name="method"/> on <paramref name="map"/> with
        /// <paramref name="premiumTier"/> except who starts it.
        /// </summary>
        public AcceptanceReport CanStart(RecruitmentMethodDef method, Map map, bool needsOriginLottery, int premiumTier = 0)
        {
            if (map == null)
            {
                return false;
            }
            AcceptanceReport common = CanStartCommon(method, map);
            if (!common.Accepted)
            {
                return common;
            }
            int cost = PremiumTier.TotalCost(method.Cost, premiumTier);
            int silver = RecruitmentUtility.CountSpendableSilver(map);
            if (silver < cost)
            {
                return "Recruitment_Reason_Silver".Translate(cost, silver);
            }
            if (needsOriginLottery && !RecruitmentUtility.AnyEligibleOrigin(checkPermit: true))
            {
                return "Recruitment_Reason_NoFaction".Translate();
            }
            return true;
        }

        /// <summary>Research, surface/space and the per-map limit, shared by every method.</summary>
        private AcceptanceReport CanStartCommon(RecruitmentMethodDef method, Map map)
        {
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
            return true;
        }

        /// <summary>
        /// Starts a trader or comms drive. <paramref name="fixedOrigin"/> is the trader's faction (or the faction
        /// asked directly), or null to draw one. With <paramref name="direct"/>, the request goes to
        /// <paramref name="fixedOrigin"/> itself and also costs goodwill.
        /// </summary>
        public bool TryStart(RecruitmentMethodDef method, Map map, Faction fixedOrigin, Pawn starter, int premiumTier, bool direct = false)
        {
            premiumTier = PremiumTier.Clamp(premiumTier);
            AcceptanceReport report = direct ? CanRequestDirect(method, map, fixedOrigin, premiumTier) : CanStart(method, map, fixedOrigin == null, premiumTier);
            if (!report.Accepted)
            {
                RejectMessage(report, starter);
                return false;
            }
            Faction origin = fixedOrigin ?? RecruitmentUtility.EligibleOrigins(checkPermit: true).RandomElement();
            int cost = PremiumTier.TotalCost(method.Cost, premiumTier);
            if (!RecruitmentUtility.TryPay(map, cost))
            {
                RejectMessage("Recruitment_Reason_Silver".Translate(cost, RecruitmentUtility.CountSpendableSilver(map)), starter);
                return false;
            }
            int goodwill = 0;
            if (direct)
            {
                goodwill = RecruitmentUtility.DirectGoodwillCost(origin);
                Faction.OfPlayer.TryAffectGoodwillWith(origin, -DirectRequestRule.GoodwillCost, canSendMessage: false, canSendHostilityLetter: true, RecruitmentDefOf.Recruitment_RequestedSettlers);
            }
            int social = RecruitmentUtility.SocialLevel(starter);
            RecruitmentDrive drive = AddDrive(method, map, origin, cost, social, premiumTier, direct);
            string period = drive.durationTicks.ToStringTicksToPeriod();
            TaggedString text = direct
                ? "Recruitment_StartedDirect".Translate(origin.Name, method.label, goodwill, period)
                : "Recruitment_Started".Translate(method.LabelCap, period);
            Messages.Message(text, starter, MessageTypeDefOf.TaskCompletion, historical: false);
            return true;
        }

        private RecruitmentDrive AddDrive(RecruitmentMethodDef method, Map map, Faction origin, int paid, int social, int premiumTier, bool direct = false)
        {
            int now = Find.TickManager.TicksGame;
            var drive = new RecruitmentDrive
            {
                id = nextId++,
                method = method,
                state = DriveState.Underway,
                map = map,
                origin = origin,
                applicantCount = RecruitmentUtility.RollApplicantCount(social),
                paidSilver = paid,
                premiumTier = premiumTier,
                direct = direct,
                starterSocial = social,
                plannedMapName = MapName(map),
                startTick = now,
                durationTicks = RecruitmentUtility.RollDurationTicks(method),
            };
            drive.arrivalTick = now + drive.durationTicks;
            drives.Add(drive);
            return drive;
        }

        // ---- Direct requests (asking a faction over the comms console) ----------------------------------

        /// <summary>Whether a direct request to <paramref name="faction"/> is still waiting for its result.</summary>
        public bool HasPendingDirectRequest(Faction faction)
        {
            for (int i = 0; i < drives.Count; i++)
            {
                if (drives[i].direct && drives[i].origin == faction)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The faction-side conditions of a direct request: it can send settlers (the Empire needs a colonist with
        /// the trade permit), is not hostile, keeps goodwill at 0 or above after paying, and has no earlier
        /// request still waiting. A rejection without a reason means the faction is never offered.
        /// </summary>
        public AcceptanceReport CanAskFaction(Faction faction)
        {
            if (!RecruitmentUtility.IsDirectRequestCandidate(faction))
            {
                return false;
            }
            int goodwillCost = RecruitmentUtility.DirectGoodwillCost(faction);
            switch (DirectRequestRule.Check(faction.HostileTo(Faction.OfPlayer), faction.PlayerGoodwill, goodwillCost, HasPendingDirectRequest(faction)))
            {
                case DirectRequestBlock.Hostile:
                    return "Recruitment_Reason_FactionHostile".Translate();
                case DirectRequestBlock.Goodwill:
                    return "Recruitment_Reason_Goodwill".Translate(goodwillCost, faction.PlayerGoodwill);
                case DirectRequestBlock.Pending:
                    return "Recruitment_Reason_DirectPending".Translate();
            }
            if (!RecruitmentUtility.IsEligibleOrigin(faction, checkPermit: false))
            {
                return "Recruitment_Reason_SettlementUnwilling".Translate(faction.Name);
            }
            if (!RecruitmentUtility.IsEligibleOrigin(faction, checkPermit: true))
            {
                return "Recruitment_Reason_NeedsPermit".Translate(faction.Name);
            }
            if (!Faction.OfPlayer.CanChangeGoodwillFor(faction, -goodwillCost))
            {
                return "Recruitment_Reason_GoodwillLocked".Translate();
            }
            return true;
        }

        /// <summary>Everything about asking <paramref name="faction"/> for settlers with <paramref name="method"/>.</summary>
        public AcceptanceReport CanRequestDirect(RecruitmentMethodDef method, Map map, Faction faction, int premiumTier)
        {
            AcceptanceReport target = CanAskFaction(faction);
            if (!target.Accepted)
            {
                return target;
            }
            return CanStart(method, map, needsOriginLottery: false, premiumTier);
        }

        // ---- Envoys (caravans visiting settlements) ---------------------------------------------------

        public int CooldownTicksRemaining(Settlement settlement)
        {
            int? last = settlementRequests.TryGetValue(settlement.ID, out int tick) ? tick : (int?)null;
            return CooldownRule.TicksRemaining(last, Find.TickManager.TicksGame, RecruitmentMod.Settings.EnvoyCooldownTicks);
        }

        /// <summary>Settlements of <paramref name="faction"/> that cannot be asked again yet, with the ticks left.</summary>
        public List<(Settlement settlement, int ticks)> SettlementsOnCooldown(Faction faction)
        {
            var result = new List<(Settlement, int)>();
            if (settlementRequests.Count == 0)
            {
                return result;
            }
            foreach (Settlement settlement in Find.WorldObjects.Settlements)
            {
                if (settlement.Faction == faction && settlementRequests.ContainsKey(settlement.ID))
                {
                    int ticks = CooldownTicksRemaining(settlement);
                    if (ticks > 0)
                    {
                        result.Add((settlement, ticks));
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Whether <paramref name="method"/> could be used from <paramref name="map"/> right now, for the tab's
        /// overview. The trader needs a visiting trader and the envoy a caravan, so for them only what the colony
        /// controls is checked; the comms methods are checked in full.
        /// </summary>
        public AcceptanceReport Availability(RecruitmentMethodDef method, Map map)
        {
            if (method.startKind == RecruitmentStartKind.Envoy)
            {
                if (!method.ResearchFinished)
                {
                    return CanStartCommon(method, map);
                }
                return RecruitmentUtility.ResolveStartDestination(method) != null ? (AcceptanceReport)true : "Recruitment_Reason_NoDestination".Translate();
            }
            if (map == null || !map.IsPlayerHome)
            {
                return "Recruitment_Reason_NotColonyMap".Translate();
            }
            return CanStart(method, map, needsOriginLottery: method.startKind == RecruitmentStartKind.CommsConsole);
        }

        /// <summary>
        /// Whether <paramref name="caravan"/> can ask <paramref name="settlement"/> to send settlers. With
        /// <paramref name="checkSilver"/> false the caravan's silver is ignored (used for the departure warning);
        /// otherwise it must cover the cost with <paramref name="premiumTier"/>.
        /// </summary>
        public AcceptanceReport CanRequestAt(Caravan caravan, Settlement settlement, bool checkSilver, Map chosenMap = null, bool destinationSelected = false, int premiumTier = 0)
        {
            RecruitmentMethodDef method = RecruitmentDefOf.Recruitment_Envoy;
            AcceptanceReport target = CanAskSettlement(settlement);
            if (!target.Accepted)
            {
                return target;
            }
            Faction faction = settlement.Faction;
            if (caravan != null)
            {
                Pawn negotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan, faction, settlement.TraderKind);
                if (negotiator == null)
                {
                    // FindBestNegotiator only returns pawns who may deal with this faction (e.g. have the Empire's title).
                    Pawn anyone = BestCaravanPawnUtility.FindBestNegotiator(caravan);
                    if (anyone == null || anyone.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
                    {
                        return "Recruitment_Reason_NoNegotiator".Translate();
                    }
                    AcceptanceReport canTrade = anyone.CanTradeWith(faction, settlement.TraderKind);
                    return canTrade.Reason.NullOrEmpty() ? "Recruitment_Reason_NoNegotiator".Translate().Resolve() : canTrade.Reason;
                }
            }
            Map destination = destinationSelected ? chosenMap : RecruitmentUtility.ResolveStartDestination(method);
            if (destination == null || !Find.Maps.Contains(destination) || !destination.IsPlayerHome)
            {
                return "Recruitment_Reason_NoDestination".Translate();
            }
            AcceptanceReport common = CanStartCommon(method, destination);
            if (!common.Accepted)
            {
                return common;
            }
            if (checkSilver && caravan != null)
            {
                int cost = PremiumTier.TotalCost(method.Cost, premiumTier);
                int silver = RecruitmentUtility.CountCaravanSilver(caravan);
                if (silver < cost)
                {
                    return "Recruitment_Reason_CaravanSilver".Translate(cost, silver);
                }
            }
            return true;
        }

        /// <summary>The settlement-side conditions: a willing, non-hostile faction and no recent request.</summary>
        public AcceptanceReport CanAskSettlement(Settlement settlement)
        {
            if (settlement == null || !settlement.Spawned || settlement.HasMap || settlement.Tile.LayerDef.isSpace)
            {
                return false;
            }
            Faction faction = settlement.Faction;
            if (faction == null || faction.IsPlayer || faction.HostileTo(Faction.OfPlayer))
            {
                return "Recruitment_Reason_SettlementHostile".Translate();
            }
            // Permits depend on the negotiator, so they are checked against the caravan, not here.
            if (!RecruitmentUtility.IsEligibleOrigin(faction, checkPermit: false))
            {
                return "Recruitment_Reason_SettlementUnwilling".Translate(faction.Name);
            }
            int cooldown = CooldownTicksRemaining(settlement);
            if (cooldown > 0)
            {
                return "Recruitment_Reason_Cooldown".Translate(cooldown.ToStringTicksToPeriod());
            }
            return true;
        }

        /// <summary>Called when an envoy caravan arrives at (or is already visiting) a settlement and asks for settlers.</summary>
        public bool TryRequestAt(Caravan caravan, Settlement settlement, Map chosenMap = null, bool destinationSelected = false, int premiumTier = 0)
        {
            premiumTier = PremiumTier.Clamp(premiumTier);
            AcceptanceReport report = CanRequestAt(caravan, settlement, checkSilver: true, chosenMap, destinationSelected, premiumTier);
            if (!report.Accepted)
            {
                if (!report.Reason.NullOrEmpty())
                {
                    Messages.Message(report.Reason, caravan, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }
            RecruitmentMethodDef method = RecruitmentDefOf.Recruitment_Envoy;
            int cost = PremiumTier.TotalCost(method.Cost, premiumTier);
            if (!RecruitmentUtility.TryPayFromCaravan(caravan, cost))
            {
                Messages.Message("Recruitment_Reason_CaravanSilver".Translate(cost, RecruitmentUtility.CountCaravanSilver(caravan)), caravan, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            Pawn negotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan, settlement.Faction, settlement.TraderKind);
            Map destination = destinationSelected ? chosenMap : RecruitmentUtility.ResolveStartDestination(method);
            RecruitmentDrive drive = AddDrive(method, destination, settlement.Faction, cost, RecruitmentUtility.SocialLevel(negotiator), premiumTier);
            settlementRequests[settlement.ID] = Find.TickManager.TicksGame;
            Messages.Message("Recruitment_EnvoyRequested".Translate(negotiator.Named("PAWN"), settlement.Label, drive.durationTicks.ToStringTicksToPeriod()), caravan, MessageTypeDefOf.TaskCompletion, historical: false);
            return true;
        }

        // ---- Ticking ----------------------------------------------------------------------------------

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            // Deadlines are checked every tick (the list is tiny) so applicants leave exactly when their letter expires.
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                if (i < groups.Count && !groups[i].decisionStarted && now >= groups[i].deadlineTick)
                {
                    Decide(groups[i], new List<Pawn>(), expired: true);
                }
            }
            if (now % CheckInterval != 0)
            {
                return;
            }
            foreach (ApplicantGroup group in groups.Where(g => g.decisionStarted).ToList())
            {
                ProcessDecision(group);
            }
            if (legacyCheckPending)
            {
                legacyCheckPending = false;
                SettleLegacyEnvoys();
            }
            // Backwards by index: Arrive may remove the current drive, and this avoids copying the list.
            for (int i = drives.Count - 1; i >= 0; i--)
            {
                if (i < drives.Count && now >= drives[i].arrivalTick)
                {
                    Arrive(drives[i]);
                }
            }
            if (orphanCheckPending)
            {
                orphanCheckPending = false;
                RecoverOrphans();
            }
        }

        /// <summary>
        /// Before envoys travelled by caravan they walked off the map and were held by this component. Such envoys
        /// in an old save are brought home (the orphan scan does that) and a trip that had not left yet is refunded;
        /// the applicants of a trip already underway still come.
        /// </summary>
        private void SettleLegacyEnvoys()
        {
            for (int i = drives.Count - 1; i >= 0; i--)
            {
                RecruitmentDrive drive = drives[i];
                if (drive.envoy == null)
                {
                    continue;
                }
                Pawn envoy = drive.envoy;
                drive.envoy = null;
                if (drive.state == DriveState.EnvoyDeparting)
                {
                    drives.RemoveAt(i);
                    bool onMap = envoy.Spawned;
                    RecruitmentUtility.Refund(onMap ? envoy.Map : drive.map, onMap ? envoy.Position : IntVec3.Invalid, drive.paidSilver);
                    if (onMap && envoy.CurJobDef == RecruitmentDefOf.Recruitment_DepartAsEnvoy)
                    {
                        envoy.jobs.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
                    }
                }
                orphanCheckPending = true;
                Messages.Message("Recruitment_LegacyEnvoyRecalled".Translate(envoy.Named("PAWN")), MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }

        /// <summary>
        /// Held pawns that no group refers to any more (e.g. after a def went missing on load, an arrival that
        /// failed, or an envoy from an old save) would otherwise be lost: colonists are brought home, others discarded.
        /// </summary>
        private void RecoverOrphans()
        {
            if (heldPawns.Count == 0)
            {
                return;
            }
            var referenced = new HashSet<Pawn>();
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
                        // No colony to go to yet; look again on a later check.
                        orphanCheckPending = true;
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
                if (pawn.holdingOwner == null && !pawn.Spawned && !pawn.Destroyed)
                    heldPawns.TryAdd(pawn);
                TryDeliver(pawn, map, method);
            }
        }

        private void Arrive(RecruitmentDrive drive)
        {
            Map dest = RecruitmentUtility.ResolveDestination(drive.method, drive.map);
            if (dest == null)
            {
                // A gravship in flight has no map yet: wait for it to land rather than failing.
                if (Find.CurrentGravship != null)
                {
                    return;
                }
                RecordHistory(DriveHistory(drive), RecruitmentOutcome.NoDestination);
                drives.Remove(drive);
                Find.LetterStack.ReceiveLetter("Recruitment_LetterFailedLabel".Translate(), "Recruitment_LetterNoDestination".Translate(drive.method.label), LetterDefOf.NegativeEvent);
                return;
            }
            Faction origin = drive.applicantCount > 0 ? ValidOrigin(drive) : null;
            var group = new ApplicantGroup
            {
                id = nextId++, method = drive.method, map = dest,
                deadlineTick = Find.TickManager.TicksGame + RecruitmentMod.Settings.DecisionTicks,
                autoJoin = RecruitmentMod.Settings.joinMode == JoinMode.AutoJoin,
                historyEntry = DriveHistory(drive),
            };
            group.historyEntry.actualMap = MapName(dest);
            group.historyEntry.originName = origin?.Name ?? "";
            groups.Add(group);
            drives.Remove(drive);
            List<Pawn> applicants = group.pawns;
            int candidates = PremiumTier.Candidates(drive.premiumTier);
            for (int i = 0; i < drive.applicantCount; i++)
            {
                // Generation failures are logged and skipped. With a premium, the best of several candidates comes.
                Pawn pawn = RecruitmentUtility.TryGenerateBestApplicant(origin, dest, candidates);
                if (pawn == null)
                {
                    group.historyEntry.generationFailures++;
                    continue;
                }
                if (pawn.IsWorldPawn())
                {
                    Find.WorldPawns.RemovePawn(pawn);
                }
                applicants.Add(pawn);
                if (!heldPawns.TryAdd(pawn) && pawn.holdingOwner == null && !pawn.Spawned && !pawn.Destroyed)
                {
                    // A modded container can reject an add; WorldPawns is a second deep-saving owner.
                    Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                    Log.Warning($"[Recruitment] Keeping generated applicant {pawn} in WorldPawns until delivery.");
                }
            }

            group.historyEntry.arrived = applicants.Count;
            if (applicants.Count == 0)
            {
                RecordHistory(group.historyEntry, drive.applicantCount == 0 ? RecruitmentOutcome.NoApplicants : RecruitmentOutcome.GenerationFailed);
                groups.Remove(group);
                Find.LetterStack.ReceiveLetter("Recruitment_LetterFailedLabel".Translate(), "Recruitment_LetterNoApplicants".Translate(drive.method.label), LetterDefOf.NegativeEvent);
                return;
            }

            if (group.autoJoin)
            {
                group.decisionStarted = true;
                group.acceptedPawns.AddRange(applicants);
                ProcessDecision(group);
                return;
            }

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
            if (!groups.Contains(group)) return true;
            if (!group.decisionStarted)
            {
                List<Pawn> joining = group.pawns.Where(p => accepted.Contains(p) && !p.Destroyed).ToList();
                if (joining.Count > 0 && RecruitmentUtility.ResolveDestination(group.method, group.map) == null)
                    return "Recruitment_Reason_NoDestination".Translate();
                // Persist the choice before any fallible pawn callbacks. Retries never change it.
                group.acceptedPawns = joining;
                group.decisionExpired = expired;
                group.decisionStarted = true;
            }
            return ProcessDecision(group) ? (AcceptanceReport)true : "Recruitment_Reason_RecoveryPending".Translate();
        }

        private static void SendJoinedLetter(List<Pawn> joined, RecruitmentMethodDef method)
        {
            TaggedString text = "Recruitment_LetterJoined".Translate(method.label, joined.Select(p => p.NameFullColored.Resolve()).ToLineList("  - "));
            Find.LetterStack.ReceiveLetter("Recruitment_LetterJoinedLabel".Translate(joined.Count), text, LetterDefOf.PositiveEvent, new LookTargets(joined));
        }

        // ---- Debug ------------------------------------------------------------------------------------

        public void DevArriveNow(RecruitmentDrive drive)
        {
            Arrive(drive);
        }

        // ---- Saving -----------------------------------------------------------------------------------

        public override void ExposeData()
        {
            base.ExposeData();
            ExposeHistory();
            Scribe_Deep.Look(ref heldPawns, "heldPawns", this);
            Scribe_Collections.Look(ref drives, "drives", LookMode.Deep);
            Scribe_Collections.Look(ref groups, "groups", LookMode.Deep);
            Scribe_Collections.Look(ref settlementRequests, "settlementRequests", LookMode.Value, LookMode.Value);
            // Deep-saved so that comms console jobs, which save their target by reference, can find it on load.
            Scribe_Deep.Look(ref commTarget, "commTarget");
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                heldPawns ??= new ThingOwner<Pawn>(this, oneStackOnly: false, LookMode.Deep);
                drives ??= new List<RecruitmentDrive>();
                groups ??= new List<ApplicantGroup>();
                settlementRequests ??= new Dictionary<int, int>();
                commTarget ??= new RecruitmentCommTarget();
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
