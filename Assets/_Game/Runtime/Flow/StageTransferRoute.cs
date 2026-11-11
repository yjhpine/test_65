using System.Collections.Generic;
using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Flow
{
    // One distance clock for the whole route: no restart/easing at individual defeat positions.
    public sealed class StageTransferRoute
    {
        private readonly Vector2[] points;
        private readonly float[] distances;
        public float Length { get; }
        public float Duration { get; }
        public int Count => points.Length;
        public Vector2 this[int index] => points[index];
        public StageTransferRoute(IReadOnlyList<Vector2> route)
        {
            if (route == null || route.Count < 2) throw new System.ArgumentException("A route needs an origin and destination.");
            points = new Vector2[route.Count]; distances = new float[route.Count];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = route[i];
                if (i > 0) distances[i] = distances[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            }
            Length = distances[distances.Length - 1];
            Duration = Mathf.Clamp(Length / 60f, .4f, 1.1f);
        }
        public static float Progress(float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            const float tail = .22f, start = 1f - tail, speed = 1f / (1f - tail / 2f);
            float s = Mathf.Max(0f, (t - start) / tail);
            return t >= 1f ? 1f : speed * (t <= start ? t : start + tail * (s - s * s / 2f));
        }
        public Vector2 Evaluate(float elapsed, out int passed)
        {
            float distance = Progress(elapsed / Duration) * Length;
            passed = 0;
            while (passed + 1 < points.Length && distances[passed + 1] <= distance) passed++;
            if (passed == points.Length - 1) return points[passed];
            float segment = distances[passed + 1] - distances[passed];
            return Vector2.Lerp(points[passed], points[passed + 1], segment > 0 ? (distance - distances[passed]) / segment : 1);
        }
        public static bool SpaceFree(CapsuleCollider2D body, Vector2 position, int mask, List<Collider2D> overlaps)
        {
            Vector2 size = Vector2.Scale(body.size, new Vector2(Mathf.Abs(body.transform.lossyScale.x), Mathf.Abs(body.transform.lossyScale.y)));
            Vector2 center = position + (Vector2)body.transform.TransformVector(body.offset);
            Physics2D.OverlapCapsule(center, size, body.direction, body.transform.eulerAngles.z, UnitPhysics2D.Filter(mask), overlaps);
            foreach (var other in overlaps)
                if (other.attachedRigidbody != body.attachedRigidbody &&
                    !Physics2D.GetIgnoreLayerCollision(body.gameObject.layer, other.gameObject.layer)) return false;
            return true;
        }
    }
}
