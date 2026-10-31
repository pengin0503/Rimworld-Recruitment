using System;
using System.Collections.Generic;

namespace Recruitment.Logic
{
    public static class BoundedHistory
    {
        public static void Store<T, TKey>(List<T> entries, T value, Func<T, TKey> key, int limit)
        {
            TKey id = key(value);
            entries.RemoveAll(entry => EqualityComparer<TKey>.Default.Equals(key(entry), id));
            entries.Add(value);
            if (entries.Count > limit) entries.RemoveRange(0, entries.Count - limit);
        }
    }
}
