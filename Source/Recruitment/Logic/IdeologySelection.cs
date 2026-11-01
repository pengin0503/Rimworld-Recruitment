using System.Collections.Generic;

namespace Recruitment.Logic
{
    /// <summary>Selects the most common non-null value, preferring the nominated value on ties.</summary>
    public static class IdeologySelection
    {
        public static T MostCommonOrDefault<T>(IEnumerable<T> values, T preferred) where T : class
        {
            var counts = new Dictionary<T, int>();
            T selected = preferred;
            int highestCount = 0;
            foreach (T value in values)
            {
                if (value == null) continue;
                counts.TryGetValue(value, out int count);
                count++;
                counts[value] = count;
                if (count > highestCount || (count == highestCount && EqualityComparer<T>.Default.Equals(value, preferred)))
                {
                    highestCount = count;
                    selected = value;
                }
            }
            return selected;
        }
    }
}
