using System;
using System.Collections.Generic;

namespace Recruitment.Logic
{
    public static class PaymentPlan
    {
        /// <summary>
        /// Splits <paramref name="cost"/> over the given stack sizes, taking from each stack in order.
        /// Returns how much to take from each stack, or null if the stacks do not cover the cost.
        /// </summary>
        public static int[] Plan(IReadOnlyList<int> stackCounts, int cost)
        {
            var take = new int[stackCounts.Count];
            int remaining = Math.Max(0, cost);
            for (int i = 0; i < stackCounts.Count && remaining > 0; i++)
            {
                int amount = Math.Min(Math.Max(0, stackCounts[i]), remaining);
                take[i] = amount;
                remaining -= amount;
            }
            return remaining > 0 ? null : take;
        }
    }
}
