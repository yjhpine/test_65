using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    public enum BossState { Dormant, Approach, Telegraph, Execute, Recover, Staggered, PhaseShift, Death }

    // Read-only description of the area a pattern is about to hit, for the visual layer.
    public struct BossTelegraph
    {
        public bool Visible;
        public Vector2 Center;
        public Vector2 Size;
        // false = still tracking its target, true = fixed in place (drawn in a different color).
        public bool Locked;
        // 0 when the warning appears, 1 at the moment the hit lands; drives the danger zone's opacity.
        public float Urgency;
    }
}
