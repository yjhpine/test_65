using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    public static class UnitPhysics2D
    {
        public static ContactFilter2D Filter(int mask) => new ContactFilter2D
            { useLayerMask = true, layerMask = mask, useTriggers = false };

        public static bool IsAvailable(UnitHealth target) => target != null && target.IsAlive &&
            target.isActiveAndEnabled && target.Owner != null && target.Owner.isActiveAndEnabled;

        public static Collider2D Body(UnitHealth target)
        {
            if (target == null) return null;
            var collider = target.GetComponent<Collider2D>();
            return collider != null && collider.enabled && !collider.isTrigger ? collider : null;
        }

        public static bool HasSight(Vector2 from, Vector2 to, int mask, List<RaycastHit2D> hits) =>
            Physics2D.Linecast(from, to, Filter(mask), hits) == 0;
    }
}
