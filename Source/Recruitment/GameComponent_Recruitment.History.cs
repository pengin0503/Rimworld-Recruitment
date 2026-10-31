using System.Collections.Generic;
using Recruitment.Logic;
using Verse;

namespace Recruitment
{
    public partial class GameComponent_Recruitment
    {
        public const int HistoryLimit = 100;

        private List<RecruitmentHistoryEntry> history = new List<RecruitmentHistoryEntry>();

        public IReadOnlyList<RecruitmentHistoryEntry> History => history;

        private static string MapName(Map map)
        {
            return map?.Parent?.LabelCap ?? "";
        }

        private RecruitmentHistoryEntry DriveHistory(RecruitmentDrive drive)
        {
            return new RecruitmentHistoryEntry
            {
                id = drive.id, methodLabel = drive.method.LabelCap.Resolve(), originName = drive.origin?.Name ?? "",
                plannedMap = drive.plannedMapName.NullOrEmpty() ? MapName(drive.map) : drive.plannedMapName,
                paidSilver = drive.paidSilver, startTick = drive.startTick,
            };
        }

        private void RecordHistory(RecruitmentHistoryEntry entry, RecruitmentOutcome outcome)
        {
            entry.outcome = outcome;
            entry.completionTick = Find.TickManager.TicksGame;
            BoundedHistory.Store(history, entry.Copy(), h => h.id, HistoryLimit);
        }

        private void RecordDecision(ApplicantGroup group, bool complete, Map destination)
        {
            // Old saves have no snapshot; build one from their still-available group information.
            group.historyEntry ??= new RecruitmentHistoryEntry
            {
                id = group.id, methodLabel = group.method.LabelCap.Resolve(), plannedMap = MapName(group.map),
                arrived = group.pawns.Count + group.joinedCount + group.rejectedCount + group.expiredCount,
            };
            RecruitmentHistoryEntry entry = group.historyEntry;
            if (destination != null) entry.actualMap = MapName(destination);
            entry.joined = group.joinedCount;
            entry.rejected = group.rejectedCount;
            entry.expired = group.expiredCount;
            RecruitmentOutcome outcome = !complete ? RecruitmentOutcome.RecoveryPending
                : group.decisionExpired ? RecruitmentOutcome.Expired
                : group.autoJoin ? RecruitmentOutcome.AutoJoined : RecruitmentOutcome.Completed;
            // Do not churn the history/UI every retry tick while nothing has changed.
            if (group.historyRecorded && !complete && entry.outcome == outcome && entry.joined == group.lastRecordedJoined
                && entry.rejected + entry.expired == group.lastRecordedLeft) return;
            RecordHistory(entry, outcome);
            group.historyRecorded = true;
            group.lastRecordedJoined = entry.joined;
            group.lastRecordedLeft = entry.rejected + entry.expired;
        }

        private void ExposeHistory()
        {
            Scribe_Collections.Look(ref history, "recruitmentHistory", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                history ??= new List<RecruitmentHistoryEntry>();
                history.RemoveAll(entry => entry == null);
                if (history.Count > HistoryLimit) history.RemoveRange(0, history.Count - HistoryLimit);
            }
        }
    }
}
