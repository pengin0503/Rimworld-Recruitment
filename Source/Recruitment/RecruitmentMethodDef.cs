using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Recruitment
{
    public enum RecruitmentStartKind
    {
        /// <summary>A colonist asks a visiting trader.</summary>
        Trader,

        /// <summary>A colonist leaves the map as an envoy and comes back with the applicants.</summary>
        Envoy,

        /// <summary>A colonist uses a powered comms console.</summary>
        CommsConsole,
    }

    /// <summary>One way of recruiting colonists. Cost and duration can be overridden in the mod settings.</summary>
    public class RecruitmentMethodDef : Def
    {
        public RecruitmentStartKind startKind;

        /// <summary>The job a colonist does to start this drive (unused for envoys, who use <see cref="startJob"/> to walk out).</summary>
        public JobDef startJob;

        public List<ResearchProjectDef> researchPrerequisites = new List<ResearchProjectDef>();

        public int baseCost;

        public FloatRange baseDays = new FloatRange(1f, 2f);

        public bool allowSurface = true;

        public bool allowSpace;

        /// <summary>Tried in order; the first one usable on the destination map is used.</summary>
        public List<PawnsArrivalModeDef> arrivalModes = new List<PawnsArrivalModeDef>();

        public int uiOrder;

        public bool UsesEnvoySocial => startKind == RecruitmentStartKind.Envoy;

        public int Cost => RecruitmentMod.Settings.CostFor(this);

        public FloatRange Days => RecruitmentMod.Settings.DaysFor(this);

        public bool ResearchFinished => researchPrerequisites.All(r => r.IsFinished);

        public IEnumerable<ResearchProjectDef> MissingResearch => researchPrerequisites.Where(r => !r.IsFinished);

        public static IEnumerable<RecruitmentMethodDef> AllInOrder => DefDatabase<RecruitmentMethodDef>.AllDefs.OrderBy(d => d.uiOrder);

        public static RecruitmentMethodDef ForJob(JobDef job)
        {
            return DefDatabase<RecruitmentMethodDef>.AllDefsListForReading.FirstOrDefault(d => d.startJob == job);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (startJob == null)
            {
                yield return "startJob is not set.";
            }
            if (arrivalModes.NullOrEmpty())
            {
                yield return "arrivalModes is empty.";
            }
            if (!allowSurface && !allowSpace)
            {
                yield return "the method can be used neither on the surface nor in space.";
            }
            if (baseCost < 0)
            {
                yield return "baseCost is negative.";
            }
            if (baseDays.min < 0f || baseDays.max < baseDays.min)
            {
                yield return "baseDays is not a valid range.";
            }
        }
    }
}
