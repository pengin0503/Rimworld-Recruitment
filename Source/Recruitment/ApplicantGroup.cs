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
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawns ??= new List<Pawn>();
                pawns.RemoveAll(p => p == null);
            }
        }
    }
}
