using System;

namespace Recruitment.Logic
{
    /// <summary>
    /// Ages pawns that are away in the same chunks the game uses for idle world pawns
    /// (<c>Pawn.TickMothballed</c> every 15000 ticks).
    /// </summary>
    public static class MothballSchedule
    {
        public const int Interval = 15000;

        /// <summary>Whole intervals that have passed since <paramref name="lastTick"/>, in ticks.</summary>
        public static int DueTicks(int lastTick, int now, int interval)
        {
            if (interval <= 0 || now <= lastTick)
            {
                return 0;
            }
            return (now - lastTick) / interval * interval;
        }

        /// <summary>Ticks not yet applied when the pawn comes back.</summary>
        public static int RemainderTicks(int lastTick, int now)
        {
            return Math.Max(0, now - lastTick);
        }
    }
}
