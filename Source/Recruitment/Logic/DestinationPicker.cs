using System.Collections.Generic;

namespace Recruitment.Logic
{
    public readonly struct MapCandidate
    {
        public readonly int Id;

        public readonly bool IsPlayerHome;

        public readonly bool IsSpace;

        public readonly int FreeColonists;

        public MapCandidate(int id, bool isPlayerHome, bool isSpace, int freeColonists)
        {
            Id = id;
            IsPlayerHome = isPlayerHome;
            IsSpace = isSpace;
            FreeColonists = freeColonists;
        }
    }

    /// <summary>Chooses which map arriving applicants (or a returning envoy) go to.</summary>
    public static class DestinationPicker
    {
        /// <summary>
        /// Keeps the original map while it is still a suitable player home; otherwise picks the suitable home
        /// with the most free colonists (lowest id on ties). Returns null when no map is suitable.
        /// </summary>
        public static int? Pick(IEnumerable<MapCandidate> maps, int originalId, bool allowSurface, bool allowSpace)
        {
            MapCandidate? best = null;
            foreach (MapCandidate map in maps)
            {
                if (!Suitable(map, allowSurface, allowSpace))
                {
                    continue;
                }
                if (map.Id == originalId)
                {
                    return map.Id;
                }
                if (best == null || map.FreeColonists > best.Value.FreeColonists || (map.FreeColonists == best.Value.FreeColonists && map.Id < best.Value.Id))
                {
                    best = map;
                }
            }
            return best?.Id;
        }

        public static bool Suitable(MapCandidate map, bool allowSurface, bool allowSpace)
        {
            return map.IsPlayerHome && (map.IsSpace ? allowSpace : allowSurface);
        }
    }
}
