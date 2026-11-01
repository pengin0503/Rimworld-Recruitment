using System;
using System.Collections.Generic;
using System.Linq;
using Recruitment.Logic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Recruitment
{
    public partial class GameComponent_Recruitment
    {
        private bool ExternallyOwned(Pawn pawn)
        {
            return pawn.Spawned || pawn.Destroyed || pawn.Discarded
                || (pawn.holdingOwner != null && pawn.holdingOwner != heldPawns);
        }

        private void KeepForRetry(Pawn pawn)
        {
            if (pawn.IsWorldPawn()) return;
            if (!ExternallyOwned(pawn) && pawn.holdingOwner == null && !heldPawns.TryAdd(pawn))
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            orphanCheckPending = true;
        }

        private bool TryDeliver(Pawn pawn, Map map, RecruitmentMethodDef method)
        {
            if (ExternallyOwned(pawn)) return true;
            return TransferAttempt.Run(
                () =>
                {
                    if (pawn.Faction != Faction.OfPlayer) pawn.SetFaction(Faction.OfPlayer);
                    RecruitmentUtility.Notify_BackFromAway(pawn);
                    // Recheck on joining: covers applicants waiting in saves made before this update.
                    RecruitmentUtility.MatchColonyIdeology(pawn);
                },
                () =>
                {
                    if (pawn.IsWorldPawn()) Find.WorldPawns.RemovePawn(pawn);
                    heldPawns.Remove(pawn);
                    try
                    {
                        RecruitmentUtility.Arrive(new List<Pawn> { pawn }, map, method);
                    }
                    catch (Exception error)
                    {
                        Log.ErrorOnce($"[Recruitment] Arrival failed; trying direct placement. {error}", pawn.thingIDNumber ^ 0x41525256);
                    }
                    if (!ExternallyOwned(pawn))
                        GenPlace.TryPlaceThing(pawn, DropCellFinder.TradeDropSpot(map), map, ThingPlaceMode.Near);
                },
                () => ExternallyOwned(pawn),
                () => KeepForRetry(pawn),
                error => Log.ErrorOnce($"[Recruitment] Delivery failed; applicant retained for retry. {error}", pawn.thingIDNumber ^ 0x52454352));
        }

        private bool TryReject(Pawn pawn, ApplicantGroup group)
        {
            if (ExternallyOwned(pawn)) return true;
            bool passed = false;
            return TransferAttempt.Run(
                () =>
                {
                    if (!group.rejectionNotified.Contains(pawn))
                    {
                        PawnDiedOrDownedThoughtsUtility.TryGiveThoughts(pawn, null, PawnDiedOrDownedThoughtsKind.DeniedJoining);
                        group.rejectionNotified.Add(pawn);
                    }
                },
                () =>
                {
                    if (pawn.IsWorldPawn()) Find.WorldPawns.RemovePawn(pawn);
                    heldPawns.Remove(pawn);
                    Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
                    passed = true;
                },
                () => passed || ExternallyOwned(pawn) || pawn.IsWorldPawn(),
                () => KeepForRetry(pawn),
                error => Log.ErrorOnce($"[Recruitment] Rejection failed; applicant retained for retry. {error}", pawn.thingIDNumber ^ 0x52454A43));
        }

        private bool ProcessDecision(ApplicantGroup group)
        {
            Map destination = RecruitmentUtility.ResolveDestination(group.method, group.map);
            var joinedNow = new List<Pawn>();
            int leftNow = 0;
            foreach (Pawn pawn in group.pawns.ToList())
            {
                bool joining = group.acceptedPawns.Contains(pawn);
                if (joining && destination == null) continue;
                bool settled = joining ? TryDeliver(pawn, destination, group.method) : TryReject(pawn, group);
                if (!settled) continue;
                // Commit per pawn. A later failure cannot repeat this pawn's faction change or arrival.
                group.pawns.Remove(pawn);
                group.acceptedPawns.Remove(pawn);
                group.rejectionNotified.Remove(pawn);
                if (joining)
                {
                    group.joinedCount++;
                    joinedNow.Add(pawn);
                }
                else
                {
                    if (group.decisionExpired) group.expiredCount++;
                    else group.rejectedCount++;
                    leftNow++;
                }
            }
            if (joinedNow.Count > 0) SendJoinedLetter(joinedNow, group.method);
            if (group.decisionExpired && leftNow > 0)
                Messages.Message("Recruitment_ApplicantsLeft".Translate(leftNow), MessageTypeDefOf.NeutralEvent);
            RecordDecision(group, group.pawns.Count == 0, destination);
            if (group.pawns.Count > 0) return false;
            groups.Remove(group);
            foreach (Letter letter in Find.LetterStack.LettersListForReading.OfType<Letter_Applicants>().Where(l => l.groupId == group.id).ToList())
                Find.LetterStack.RemoveLetter(letter);
            return true;
        }
    }
}
