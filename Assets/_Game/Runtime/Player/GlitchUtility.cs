using System.Collections.Generic;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using UnityEngine;
using static ActionPlatformer.Units.Features.UnitPhysics2D;

namespace ActionPlatformer.Player
{
    public struct GlitchRequest
    {
        public UnitHealth Target;
        public GlitchDirection Direction;
    }

    public static class GlitchUtility
    {
        public static bool ValidTarget(Unit owner, UnitHealth target, Vector2 origin, GlitchTuning tuning,
            List<RaycastHit2D> hits)
        {
            var body = Body(target);
            return IsAvailable(target) && target.Owner != owner && target.Owner.Definition.AllowGlitchTarget &&
                body != null && ((1 << body.gameObject.layer) & tuning.TargetMask.value) != 0 &&
                Vector2.Distance(origin, body.bounds.center) <= tuning.MaxDistance &&
                HasSight(origin, body.bounds.center, tuning.ObstacleMask, hits);
        }

        public static GlitchRequest Select(Unit owner, Vector2 origin, Vector2 cursor, GlitchTuning tuning,
            List<Collider2D> candidates, List<RaycastHit2D> hits)
        {
            Physics2D.OverlapCircle(origin, tuning.MaxDistance, Filter(tuning.TargetMask), candidates);
            UnitHealth best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i].GetComponentInParent<UnitHealth>();
                if (!ValidTarget(owner, candidate, origin, tuning, hits)) continue;
                var body = Body(candidate);
                float distance = (body.ClosestPoint(cursor) - cursor).sqrMagnitude;
                if (distance > tuning.AimAssistRadius * tuning.AimAssistRadius) continue;
                if (distance > bestDistance || (distance == bestDistance && best != null &&
                    candidate.GetInstanceID() >= best.GetInstanceID())) continue;
                best = candidate;
                bestDistance = distance;
            }
            return new GlitchRequest { Target = best, Direction = best == null ? GlitchDirection.Right :
                Direction((Vector2)Body(best).bounds.center, cursor, origin, tuning.CenterRadius) };
        }

        public static GlitchDirection Direction(Vector2 center, Vector2 cursor, Vector2 player, float centerRadius)
        {
            Vector2 delta = cursor - center;
            if (delta.sqrMagnitude <= centerRadius * centerRadius)
                return player.x < center.x ? GlitchDirection.Left : GlitchDirection.Right;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                return delta.x < 0f ? GlitchDirection.Left : GlitchDirection.Right;
            return delta.y < 0f ? GlitchDirection.Down : GlitchDirection.Up;
        }

        public static Vector2 Placement(Bounds player, Vector2 root, Bounds target, GlitchDirection direction, float gap)
        {
            Vector2 center = target.center;
            switch (direction)
            {
                case GlitchDirection.Left:
                    center.x -= target.extents.x + player.extents.x + gap;
                    center.y = target.min.y + player.extents.y + 0.02f;
                    break;
                case GlitchDirection.Right:
                    center.x += target.extents.x + player.extents.x + gap;
                    center.y = target.min.y + player.extents.y + 0.02f;
                    break;
                case GlitchDirection.Up: center.y += target.extents.y + player.extents.y + gap; break;
                case GlitchDirection.Down: center.y -= target.extents.y + player.extents.y + gap; break;
            }
            return center - ((Vector2)player.center - root);
        }

        public static bool SpaceFree(CapsuleCollider2D player, Vector2 root, Vector2 destination, int mask,
            List<Collider2D> overlaps)
        {
            Vector2 scale = player.transform.lossyScale;
            Vector2 size = Vector2.Scale(player.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            Vector2 center = destination + ((Vector2)player.bounds.center - root);
            Physics2D.OverlapCapsule(center, size, player.direction, player.transform.eulerAngles.z, Filter(mask), overlaps);
            for (int i = 0; i < overlaps.Count; i++)
                if (overlaps[i].attachedRigidbody != player.attachedRigidbody &&
                    !Physics2D.GetIgnoreLayerCollision(player.gameObject.layer, overlaps[i].gameObject.layer)) return false;
            return true;
        }

        // Underground v1 deliberately accepts only an unambiguous, stationary, horizontal box floor.
        public static bool TryFloor(Bounds target, int mask, List<RaycastHit2D> hits, out BoxCollider2D floor)
        {
            Vector2 from = new Vector2(target.center.x, target.min.y + 0.06f);
            int count = Physics2D.Raycast(from, Vector2.down, Filter(mask), hits, 0.25f);
            floor = count == 1 ? hits[0].collider as BoxCollider2D : null;
            if (floor == null || floor.usedByEffector || floor.attachedRigidbody != null ||
                Mathf.Abs(Mathf.DeltaAngle(floor.transform.eulerAngles.z, 0f)) > 0.01f || hits[0].normal.y < 0.999f)
                return false;
            // A second floor immediately below would make the logical underground layer ambiguous.
            int below = Physics2D.Raycast(new Vector2(from.x, floor.bounds.min.y - 0.02f), Vector2.down,
                Filter(mask), hits, 0.65f);
            return below == 0;
        }

        public static bool TryEmergence(CapsuleCollider2D player, Vector2 root, BoxCollider2D floor,
            Vector2 logical, GlitchTuning tuning, List<Collider2D> overlaps, out Vector2 destination)
        {
            destination = default;
            if (floor == null || !floor.enabled || !floor.gameObject.activeInHierarchy) return false;
            var bounds = player.bounds;
            Vector2 offset = (Vector2)bounds.center - root;
            // The marker is a fixed body-center column, even if the target moves while buried.
            Vector2 candidate = new Vector2(logical.x - offset.x,
                floor.bounds.max.y + bounds.extents.y + 0.02f - offset.y);
            if (logical.x - bounds.extents.x < floor.bounds.min.x || logical.x + bounds.extents.x > floor.bounds.max.x ||
                !SpaceFree(player, root, candidate, tuning.ObstacleMask.value | tuning.TargetMask.value, overlaps)) return false;
            destination = candidate;
            return true;
        }

        // Only used on an emergence request. The per-step marker check stays a single space query.
        // Sample equally outwards, then refine the first free boundary below 0.001 world units.
        public static bool TryNearestEmergence(CapsuleCollider2D player, Vector2 root, BoxCollider2D floor,
            Vector2 logical, float preferredDirection, GlitchTuning tuning, List<Collider2D> overlaps, out Vector2 destination)
        {
            if (TryEmergence(player, root, floor, logical, tuning, overlaps, out destination)) return true;
            if (floor == null || !floor.enabled || !floor.gameObject.activeInHierarchy) return false;
            float minX = floor.bounds.min.x + player.bounds.extents.x;
            float maxX = floor.bounds.max.x - player.bounds.extents.x;
            if (minX > maxX) return false;
            float origin = Mathf.Clamp(logical.x, minX, maxX);
            logical.x = origin;
            if (TryEmergence(player, root, floor, logical, tuning, overlaps, out destination)) return true;
            const float step = 0.05f;
            float range = Mathf.Max(origin - minX, maxX - origin);
            float previousLeft = origin, previousRight = origin;
            int count = Mathf.CeilToInt(range / step);
            for (int i = 1; i <= count; i++)
            {
                float left = Mathf.Max(minX, origin - i * step);
                float right = Mathf.Min(maxX, origin + i * step);
                Vector2 leftPoint = default, rightPoint = default;
                bool leftFree = left < previousLeft && TryEmergence(player, root, floor,
                    new Vector2(left, logical.y), tuning, overlaps, out leftPoint);
                bool rightFree = right > previousRight && TryEmergence(player, root, floor,
                    new Vector2(right, logical.y), tuning, overlaps, out rightPoint);
                if (leftFree || rightFree)
                {
                    if (leftFree) leftPoint = RefineEmergence(player, root, floor, logical.y, previousLeft, left, tuning, overlaps, leftPoint);
                    if (rightFree) rightPoint = RefineEmergence(player, root, floor, logical.y, previousRight, right, tuning, overlaps, rightPoint);
                    float offsetX = player.bounds.center.x - root.x;
                    float leftDistance = Mathf.Abs(leftPoint.x + offsetX - origin);
                    float rightDistance = Mathf.Abs(rightPoint.x + offsetX - origin);
                    bool chooseLeft = leftFree && (!rightFree ||
                        (Mathf.Abs(leftDistance - rightDistance) <= 0.001f ? preferredDirection < 0f : leftDistance < rightDistance));
                    destination = chooseLeft ? leftPoint : rightPoint;
                    return true;
                }
                previousLeft = left; previousRight = right;
            }
            return false;
        }

        private static Vector2 RefineEmergence(CapsuleCollider2D player, Vector2 root, BoxCollider2D floor,
            float y, float blocked, float free, GlitchTuning tuning, List<Collider2D> overlaps, Vector2 result)
        {
            for (int i = 0; i < 6; i++)
            {
                float middle = (blocked + free) * 0.5f;
                if (TryEmergence(player, root, floor, new Vector2(middle, y), tuning, overlaps, out var candidate))
                { free = middle; result = candidate; }
                else blocked = middle;
            }
            return result;
        }
    }
}
