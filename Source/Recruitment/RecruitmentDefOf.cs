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

        static RecruitmentDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RecruitmentDefOf));
        }
    }
}
