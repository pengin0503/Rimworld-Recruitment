using RimWorld;
using Verse;

namespace Recruitment
{
    public enum DriveState
    {
        /// <summary>The envoy is walking to the map edge; the arrival time is set once they leave.</summary>
        EnvoyDeparting,

        /// <summary>The drive is running and applicants arrive at <see cref="RecruitmentDrive.arrivalTick"/>.</summary>
        Underway,
    }

    /// <summary>A recruitment drive that has been paid for and has not arrived yet.</summary>
    public class RecruitmentDrive : IExposable
    {
        public int id;

        public RecruitmentMethodDef method;

        public DriveState state = DriveState.Underway;

        /// <summary>The map the drive was started from. Null once that map is gone.</summary>
        public Map map;

        /// <summary>The faction the applicants come from, fixed when the drive starts.</summary>
        public Faction origin;

        /// <summary>Fixed when the drive starts and kept secret from the player.</summary>
        public int applicantCount;

        public int paidSilver;

        public int startTick;

        public int durationTicks;

        public int arrivalTick = -1;

        public Pawn envoy;

        /// <summary>Up to when the away envoy has been aged (see <see cref="Logic.MothballSchedule"/>).</summary>
        public int lastAgedTick;

        /// <summary>Set once the player has been told that the envoy is waiting for a home to return to.</summary>
        public bool envoyWaitingNotified;

        public int TicksUntilArrival => state == DriveState.Underway ? arrivalTick - Find.TickManager.TicksGame : durationTicks;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && map != null && !Find.Maps.Contains(map))
            {
                map = null;
            }
            Scribe_Values.Look(ref id, "id");
            Scribe_Defs.Look(ref method, "method");
            Scribe_Values.Look(ref state, "state", DriveState.Underway);
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref origin, "origin");
            Scribe_Values.Look(ref applicantCount, "applicantCount");
            Scribe_Values.Look(ref paidSilver, "paidSilver");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref durationTicks, "durationTicks");
            Scribe_Values.Look(ref arrivalTick, "arrivalTick", -1);
            Scribe_References.Look(ref envoy, "envoy");
            // Saves from before aging existed have no value; aging then starts from the trip's start.
            Scribe_Values.Look(ref lastAgedTick, "lastAgedTick", -1);
            if (Scribe.mode == LoadSaveMode.LoadingVars && lastAgedTick < 0)
            {
                lastAgedTick = startTick;
            }
            Scribe_Values.Look(ref envoyWaitingNotified, "envoyWaitingNotified");
        }
    }
}
