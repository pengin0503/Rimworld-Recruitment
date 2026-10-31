using System;

namespace Recruitment.Logic
{
    public static class RecruitmentDuration
    {
        public const int TicksPerDay = 60000;

        /// <summary>One in-game hour; drives never finish faster than this.</summary>
        public const int MinTicks = 2500;

        /// <summary>Uniformly picks a duration between the two day counts and scales it by <paramref name="factor"/>.</summary>
        public static int RollTicks(float minDays, float maxDays, float factor, double roll01)
        {
            if (minDays > maxDays)
            {
                (minDays, maxDays) = (maxDays, minDays);
            }
            double days = minDays + (maxDays - minDays) * Math.Clamp(roll01, 0.0, 1.0);
            double ticks = Math.Round(days * factor * TicksPerDay);
            if (double.IsNaN(ticks) || ticks < MinTicks)
            {
                return MinTicks;
            }
            return ticks >= int.MaxValue ? int.MaxValue : (int)ticks;
        }
    }
}
