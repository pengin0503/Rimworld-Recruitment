using RimWorld;
using Verse;

namespace Recruitment
{
    public enum DriveState
    {
        /// <summary>Legacy (old saves only): an envoy walking to the map edge before envoys travelled by caravan.</summary>
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

        /// <summary>Everything paid in silver, premium included.</summary>
        public int paidSilver;

        /// <summary>The silver premium (<see cref="Logic.PremiumTier"/>); 0 for none and for drives from 1.0.</summary>
        public int premiumTier;

        /// <summary>
        /// A request made to <see cref="origin"/> itself over the comms console (it cost goodwill). Only one such
        /// request per faction can wait for its result at a time.
        /// </summary>
        public bool direct;

        /// <summary>Social skill of the colonist who started the drive, for display.</summary>
        public int starterSocial = -1;

        public string plannedMapName;

        public int startTick;

        public int durationTicks;

        public int arrivalTick = -1;

        /// <summary>Legacy (old saves only): the envoy of a pre-caravan envoy trip. Settled after loading.</summary>
        public Pawn envoy;

        public int TicksUntilArrival => arrivalTick - Find.TickManager.TicksGame;

        /// <summary>How far the drive has come, 0..1.</summary>
        public float Progress => durationTicks <= 0 ? 1f : UnityEngine.Mathf.Clamp01(1f - (float)TicksUntilArrival / durationTicks);

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
            Scribe_Values.Look(ref premiumTier, "premiumTier");
            Scribe_Values.Look(ref direct, "direct");
            Scribe_Values.Look(ref starterSocial, "starterSocial", -1);
            Scribe_Values.Look(ref plannedMapName, "plannedMapName");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref durationTicks, "durationTicks");
            Scribe_Values.Look(ref arrivalTick, "arrivalTick", -1);
            Scribe_References.Look(ref envoy, "envoy");
        }
    }
}
