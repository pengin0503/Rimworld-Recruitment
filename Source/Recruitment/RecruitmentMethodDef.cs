using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Recruitment
{
    public enum RecruitmentStartKind
    {
        /// <summary>A colonist asks a visiting trader.</summary>
        Trader,

        /// <summary>A caravan travels to a settlement and asks it to send settlers.</summary>
        Envoy,

        /// <summary>A colonist uses a powered comms console (the "call for settlers" contact).</summary>
        CommsConsole,
    }

    /// <summary>One way of recruiting colonists. Cost and duration can be overridden in the mod settings.</summary>
    public class RecruitmentMethodDef : Def
    {
        public RecruitmentStartKind startKind;

        /// <summary>The job a colonist does to ask a trader. Only trader methods use it.</summary>
        public JobDef startJob;

        public List<ResearchProjectDef> researchPrerequisites = new List<ResearchProjectDef>();

        public int baseCost;

        public FloatRange baseDays = new FloatRange(1f, 2f);

        public bool allowSurface = true;

        public bool allowSpace;

        /// <summary>Tried in order; the first one usable on the destination map is used.</summary>
        public List<PawnsArrivalModeDef> arrivalModes = new List<PawnsArrivalModeDef>();

        public int uiOrder;

        /// <summary>How applicants arrive, for the start dialog (e.g. "walk in from the map edge").</summary>
        [MustTranslate]
        public string arrivalLabel;

        /// <summary>Texture path of the method's icon in the recruitment tab and the start dialog.</summary>
        public string iconPath;

        [Unsaved]
        private Texture2D icon;

        /// <summary>Loaded on first use; UI code runs on the main thread, where textures may be loaded.</summary>
        public Texture2D Icon => icon ??= iconPath.NullOrEmpty() ? BaseContent.BadTex : ContentFinder<Texture2D>.Get(iconPath);

        /// <summary>The method's own cost in silver, without a premium.</summary>
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
            if (startKind == RecruitmentStartKind.Trader && startJob == null)
            {
                yield return "startJob is not set (needed by trader methods).";
            }
            if (arrivalLabel.NullOrEmpty())
            {
                yield return "arrivalLabel is not set.";
            }
            if (iconPath.NullOrEmpty())
            {
                yield return "iconPath is not set.";
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
