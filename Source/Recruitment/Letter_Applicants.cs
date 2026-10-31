using System.Linq;
using RimWorld;
using Verse;

namespace Recruitment
{
    /// <summary>Announces arrived applicants; opening it shows <see cref="Dialog_Applicants"/>.</summary>
    public class Letter_Applicants : LetterWithTimeout
    {
        public int groupId;

        private TaggedString text;

        private ApplicantGroup Group => GameComponent_Recruitment.Instance?.GroupById(groupId);

        public override bool CanDismissWithRightClick => false;

        public override bool CanShowInLetterStack => base.CanShowInLetterStack && Group != null;

        public static void Send(ApplicantGroup group, Faction origin)
        {
            var letter = (Letter_Applicants)LetterMaker.MakeLetter(RecruitmentDefOf.Recruitment_Applicants);
            letter.groupId = group.id;
            letter.Label = "Recruitment_LetterApplicantsLabel".Translate(group.pawns.Count);
            TaggedString originText = origin != null ? "Recruitment_FromFaction".Translate(origin.NameColored) : "Recruitment_FromNowhere".Translate();
            letter.text = "Recruitment_LetterApplicants".Translate(group.method.label, group.pawns.Count, originText, (RecruitmentMod.Settings.DecisionTicks / 2500).ToString())
                + "\n\n" + group.pawns.Select(p => p.NameFullColored.Resolve()).ToLineList("  - ");
            letter.lookTargets = group.map != null ? new LookTargets(group.map.Parent) : LookTargets.Invalid;
            letter.disappearAtTick = group.deadlineTick;
            Find.LetterStack.ReceiveLetter(letter);
        }

        public override void OpenLetter()
        {
            ApplicantGroup group = Group;
            if (group == null)
            {
                Find.LetterStack.RemoveLetter(this);
                return;
            }
            RecruitmentUI.OpenApplicants(group);
        }

        protected override string GetMouseoverText()
        {
            return text.Resolve();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref groupId, "groupId");
            Scribe_Values.Look(ref text, "text");
        }
    }
}
