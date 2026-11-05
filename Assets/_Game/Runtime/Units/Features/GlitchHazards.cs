using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    // An area that kills a glitch starting or ending inside it (a domain enemy's active field). Walking, jumping and
    // fighting inside are harmless; only a teleport's start and arrival points are checked, never its path.
    public interface IGlitchHazard
    {
        bool ContainsPoint(Vector2 point);
    }

    // The hazards active right now. Owners add an area when it turns active and remove it when it ends or they die,
    // so the player can ask without knowing about enemies.
    public static class GlitchHazards
    {
        private static readonly List<IGlitchHazard> active = new List<IGlitchHazard>();
        public static int Count => active.Count;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();
        public static void Add(IGlitchHazard hazard) { if (hazard != null && !active.Contains(hazard)) active.Add(hazard); }
        public static void Remove(IGlitchHazard hazard) => active.Remove(hazard);
        // The edge counts as inside.
        public static bool Contains(Vector2 point)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i] == null) { active.RemoveAt(i); continue; }
                if (active[i].ContainsPoint(point)) return true;
            }
            return false;
        }
    }
}
