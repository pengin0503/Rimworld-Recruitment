using RimWorld;
using Verse;

namespace Recruitment
{
    [DefOf]
    public static class RecruitmentDefOf
    {
        public static RecruitmentMethodDef Recruitment_Caravan;

        public static RecruitmentMethodDef Recruitment_Envoy;

        public static JobDef Recruitment_DepartAsEnvoy;

        public static LetterDef Recruitment_Applicants;

        /// <summary>The goodwill reason for asking a faction for settlers over the comms console.</summary>
        public static HistoryEventDef Recruitment_RequestedSettlers;

        static RecruitmentDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RecruitmentDefOf));
        }
    }
}
