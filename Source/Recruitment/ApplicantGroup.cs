using System.Collections.Generic;
using Verse;

namespace Recruitment
{
    /// <summary>Applicants who have arrived and are waiting for the player's decision.</summary>
    public class ApplicantGroup : IExposable
    {
        public int id;

        public RecruitmentMethodDef method;

        /// <summary>Where the applicants will appear if accepted. Re-resolved when the decision is made.</summary>
        public Map map;

        /// <summary>The applicant pawns. They are held by <see cref="GameComponent_Recruitment"/>, not spawned.</summary>
        public List<Pawn> pawns = new List<Pawn>();

        public int deadlineTick;

        public bool decisionStarted;
        public bool decisionExpired;
        public bool autoJoin;
        public List<Pawn> acceptedPawns = new List<Pawn>();
        public List<Pawn> rejectionNotified = new List<Pawn>();
        public int joinedCount;
        public int rejectedCount;
        public int expiredCount;
        public RecruitmentHistoryEntry historyEntry;
        // UI/history retry throttling is deliberately transient; a reload refreshes the snapshot once.
        public bool historyRecorded;
        public int lastRecordedJoined;
        public int lastRecordedLeft;

        public int TicksLeft => deadlineTick - Find.TickManager.TicksGame;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && map != null && !Find.Maps.Contains(map))
            {
                map = null;
            }
            Scribe_Values.Look(ref id, "id");
            Scribe_Defs.Look(ref method, "method");
            Scribe_References.Look(ref map, "map");
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            Scribe_Values.Look(ref deadlineTick, "deadlineTick");
            Scribe_Values.Look(ref decisionStarted, "decisionStarted");
            Scribe_Values.Look(ref decisionExpired, "decisionExpired");
            Scribe_Values.Look(ref autoJoin, "autoJoin");
            Scribe_Collections.Look(ref acceptedPawns, "acceptedPawns", LookMode.Reference);
            Scribe_Collections.Look(ref rejectionNotified, "rejectionNotified", LookMode.Reference);
            Scribe_Values.Look(ref joinedCount, "joinedCount");
            Scribe_Values.Look(ref rejectedCount, "rejectedCount");
            Scribe_Values.Look(ref expiredCount, "expiredCount");
            Scribe_Deep.Look(ref historyEntry, "historyEntry");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawns ??= new List<Pawn>();
                pawns.RemoveAll(p => p == null);
                acceptedPawns ??= new List<Pawn>();
                rejectionNotified ??= new List<Pawn>();
                acceptedPawns.RemoveAll(p => p == null || !pawns.Contains(p));
                rejectionNotified.RemoveAll(p => p == null || !pawns.Contains(p));
            }
        }
    }
}
