using System;

namespace Recruitment.Logic
{
    public static class CooldownRule
    {
        /// <summary>
        /// Ticks until a settlement can be asked again, given when it was last asked (null if never).
        /// Never more than <paramref name="cooldownTicks"/>.
        /// </summary>
        public static int TicksRemaining(int? lastRequestTick, int now, int cooldownTicks)
        {
            if (lastRequestTick == null || cooldownTicks <= 0)
            {
                return 0;
            }
            long remaining = (long)lastRequestTick.Value + cooldownTicks - now;
            return (int)Math.Clamp(remaining, 0, cooldownTicks);
        }
    }
}
