using Verse;

namespace Recruitment
{
    public enum RecruitmentOutcome
    {
        Completed, AutoJoined, Expired, NoApplicants, NoDestination, GenerationFailed, RecoveryPending,
    }

    /// <summary>Value snapshots only: history must not keep pawns, maps, factions or removed defs alive.</summary>
    public class RecruitmentHistoryEntry : IExposable
    {
        public int id;
        public string methodLabel;
        public string originName;
        public string plannedMap;
        public string actualMap;
        public int paidSilver = -1;
        public int startTick = -1;
        public int completionTick = -1;
        public int arrived;
        public int joined;
        public int rejected;
        public int expired;
        public int generationFailures;
        public RecruitmentOutcome outcome;

        public RecruitmentHistoryEntry Copy()
        {
            return (RecruitmentHistoryEntry)MemberwiseClone();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref methodLabel, "methodLabel");
            Scribe_Values.Look(ref originName, "originName");
            Scribe_Values.Look(ref plannedMap, "plannedMap");
            Scribe_Values.Look(ref actualMap, "actualMap");
            Scribe_Values.Look(ref paidSilver, "paidSilver", -1);
            Scribe_Values.Look(ref startTick, "startTick", -1);
            Scribe_Values.Look(ref completionTick, "completionTick", -1);
            Scribe_Values.Look(ref arrived, "arrived");
            Scribe_Values.Look(ref joined, "joined");
            Scribe_Values.Look(ref rejected, "rejected");
            Scribe_Values.Look(ref expired, "expired");
            Scribe_Values.Look(ref generationFailures, "generationFailures");
            Scribe_Values.Look(ref outcome, "outcome");
        }
    }
}
