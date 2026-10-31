using System;
using System.Collections.Generic;

namespace Recruitment.Logic
{
    /// <summary>
    /// Remembers computed values for a short time. UI code uses it so that checks which are cheap once
    /// but costly every frame (silver counts, reachability, faction lotteries) run a few times a second.
    /// </summary>
    public sealed class TimedCache<TKey, TValue>
    {
        private readonly float lifetime;

        private readonly Func<float> clock;

        private readonly Dictionary<TKey, (float time, TValue value)> entries = new Dictionary<TKey, (float, TValue)>();

        public TimedCache(float lifetime, Func<float> clock)
        {
            this.lifetime = lifetime;
            this.clock = clock;
        }

        public TValue Get(TKey key, Func<TValue> compute)
        {
            float now = clock();
            if (entries.TryGetValue(key, out var entry) && now >= entry.time && now - entry.time < lifetime)
            {
                return entry.value;
            }
            TValue value = compute();
            entries[key] = (now, value);
            return value;
        }

        public void Clear()
        {
            entries.Clear();
        }
    }
}
