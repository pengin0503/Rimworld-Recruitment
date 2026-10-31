using System;

namespace Recruitment.Logic
{
    public static class ApplicantLayout
    {
        public static float RowHeight(float descriptionHeight, float nameHeight)
        {
            return Math.Max(190f, descriptionHeight + nameHeight + 14f);
        }
    }
}
