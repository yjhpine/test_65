using ActionPlatformer.Units.Features;
using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    // Target finding, sight and damage-guard delegation for a boss. Holds no fight state of its own.
    [DisallowMultipleComponent, RequireComponent(typeof(UnitHealth), typeof(Rigidbody2D))]
    public sealed class BossCombat2D : MonoBehaviour, IUnitDamageGuard
    {
        [SerializeField] private LayerMask targetMask = 4;
        [SerializeField] private LayerMask obstacleMask = 1;
        [SerializeField] private bool initiallyFacesRight;
        private readonly List<Collider2D> overlaps = new List<Collider2D>(32);
        private readonly List<RaycastHit2D> rays = new List<RaycastHit2D>(16);
        private Collider2D body;
        private IUnitDamageGuard guard;

        private readonly HashSet<UnitHealth> hitBuffer = new HashSet<UnitHealth>();

        public IBossActionState State { get; private set; }
        // Advances on every swing/sweep that damaged someone, so visuals and sounds can react once per hit.
        public uint AttackVersion { get; private set; }
        public Vector2 BodySize => body == null ? Vector2.one : (Vector2)body.bounds.size;
        public float InitialFacing => initiallyFacesRight ? 1f : -1f;
        public Vector2 Center => body == null ? (Vector2)transform.position : (Vector2)body.bounds.center;

        private void Awake() { body = GetComponent<Collider2D>(); }

        public void Bind(IBossActionState state, IUnitDamageGuard damageGuard) { State = state; guard = damageGuard; }

        // Deliberately ignores CanReceiveDamage: a player who is briefly invulnerable is still the boss's opponent.
        public UnitHealth FindPlayer(float range, float height)
        {
            Physics2D.OverlapCircle(Center, range, UnitPhysics2D.Filter(targetMask), overlaps);
            UnitHealth best = null; float bestDistance = float.PositiveInfinity;
            foreach (var collider in overlaps)
            {
                var candidate = collider.GetComponentInParent<UnitHealth>();
                if (!UnitPhysics2D.IsAvailable(candidate) || candidate.Owner.Kind != UnitKind.Player) continue;
                var targetBody = UnitPhysics2D.Body(candidate);
                if (targetBody == null) continue;
                Vector2 delta = (Vector2)targetBody.bounds.center - Center;
                if (delta.sqrMagnitude > range * range || Mathf.Abs(delta.y) > height || !HasSight(Center, targetBody.bounds.center)) continue;
                float distance = delta.sqrMagnitude;
                if (distance < bestDistance || (distance == bestDistance && best != null && candidate.GetInstanceID() < best.GetInstanceID()))
                { best = candidate; bestDistance = distance; }
            }
            return best;
        }

        // Damages every available Player inside the box that the boss can see and that is not yet in struck. Returns the number hit.
        public int HitBox(Vector2 center, Vector2 size, int damage, ISet<UnitHealth> struck)
        {
            Physics2D.OverlapBox(center, size, 0f, UnitPhysics2D.Filter(targetMask), overlaps);
            return Strike(damage, struck, Center);
        }

        // Same rules as HitBox over a circle (shockwaves).
        public int HitCircle(Vector2 center, float radius, int damage, ISet<UnitHealth> struck)
        {
            Physics2D.OverlapCircle(center, radius, UnitPhysics2D.Filter(targetMask), overlaps);
            return Strike(damage, struck, center);
        }

        // Same as HitBox over the box that covers the body at both ends of a move (like EnemyCombat2D.HitDashPath).
        public int HitSweep(Vector2 from, Vector2 to, int damage, ISet<UnitHealth> struck)
        {
            Vector2 delta = to - from;
            Physics2D.OverlapBox((from + to) * .5f, BodySize + new Vector2(Mathf.Abs(delta.x), Mathf.Abs(delta.y)), 0f, UnitPhysics2D.Filter(targetMask), overlaps);
            return Strike(damage, struck, to);
        }

        private int Strike(int damage, ISet<UnitHealth> struck, Vector2 origin)
        {
            int count = 0;
            hitBuffer.Clear();
            foreach (var collider in overlaps)
            {
                var target = collider.GetComponentInParent<UnitHealth>();
                if (!UnitPhysics2D.IsAvailable(target) || !target.CanReceiveDamage || target.Owner.Kind != UnitKind.Player ||
                    (struck != null && struck.Contains(target)) || !hitBuffer.Add(target)) continue;
                var targetBody = UnitPhysics2D.Body(target);
                if (targetBody == null || !HasSight(origin, targetBody.ClosestPoint(origin))) continue;
                struck?.Add(target);
                target.ApplyDirectedDamage(damage, (Vector2)targetBody.bounds.center - origin);
                count++;
            }
            if (count > 0) AttackVersion++;
            return count;
        }

        // First terrain surface below (x, fromY) within maxDistance. One-way platforms are not ground for the boss, like they are not sight blockers.
        public bool TryFindGround(float x, float fromY, float maxDistance, out float surfaceY)
        {
            Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, UnitPhysics2D.Filter(obstacleMask), rays, maxDistance);
            foreach (var hit in rays)
                if (IsSolid(hit.collider)) { surfaceY = hit.point.y; return true; }
            surfaceY = 0f;
            return false;
        }

        // How far the body can travel horizontally toward targetX before terrain stops it. Returns targetX or the last free x.
        public float ClampHorizontal(Vector2 bodyCenter, float targetX)
        {
            float delta = targetX - bodyCenter.x;
            if (Mathf.Abs(delta) < 1e-4f) return targetX;
            Physics2D.BoxCast(bodyCenter, BodySize * .95f, 0f, Vector2.right * Mathf.Sign(delta), UnitPhysics2D.Filter(obstacleMask), rays, Mathf.Abs(delta));
            float nearest = Mathf.Abs(delta);
            foreach (var hit in rays) if (IsSolid(hit.collider)) nearest = Mathf.Min(nearest, hit.distance);
            return nearest >= Mathf.Abs(delta) ? targetX : bodyCenter.x + Mathf.Sign(delta) * nearest;
        }

        // Vertical counterpart of ClampHorizontal (a ceiling above a jump, the floor below a drop).
        public float ClampVertical(Vector2 bodyCenter, float targetY)
        {
            float delta = targetY - bodyCenter.y;
            if (Mathf.Abs(delta) < 1e-4f) return targetY;
            Physics2D.BoxCast(bodyCenter, BodySize * .95f, 0f, Vector2.up * Mathf.Sign(delta), UnitPhysics2D.Filter(obstacleMask), rays, Mathf.Abs(delta));
            float nearest = Mathf.Abs(delta);
            foreach (var hit in rays) if (IsSolid(hit.collider)) nearest = Mathf.Min(nearest, hit.distance);
            return nearest >= Mathf.Abs(delta) ? targetY : bodyCenter.y + Mathf.Sign(delta) * nearest;
        }

        // A horizontal ray (a point, not a body) from origin toward targetX; returns targetX, or stopBefore short of the first wall.
        public float RayClampX(Vector2 origin, float targetX, float stopBefore)
        {
            float delta = targetX - origin.x;
            if (Mathf.Abs(delta) < 1e-4f) return targetX;
            Physics2D.Raycast(origin, Vector2.right * Mathf.Sign(delta), UnitPhysics2D.Filter(obstacleMask), rays, Mathf.Abs(delta));
            float nearest = Mathf.Abs(delta);
            foreach (var hit in rays) if (IsSolid(hit.collider)) nearest = Mathf.Min(nearest, hit.distance);
            return nearest >= Mathf.Abs(delta) ? targetX : origin.x + Mathf.Sign(delta) * Mathf.Max(0f, nearest - stopBefore);
        }

        private static bool IsSolid(Collider2D collider) => collider != null &&
            !(collider.usedByEffector && collider.TryGetComponent<PlatformEffector2D>(out var effector) && effector.enabled);

        public bool HasSight(Vector2 from, Vector2 to) => UnitPhysics2D.HasSight(from, to, obstacleMask, rays);

        public bool BlocksDamage(Vector2 sourcePosition) => guard != null && guard.BlocksDamage(sourcePosition);
    }
}
