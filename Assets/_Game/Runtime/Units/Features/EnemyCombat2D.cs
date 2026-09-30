using System.Collections.Generic;
using UnityEngine;
using ActionPlatformer.Units.Fsm;

namespace ActionPlatformer.Units.Features
{
    public enum EnemyPattern { Shield, Surveillance, Drone }
    public enum EnemyPhase { Idle, Turning, Preparing, Locked, Dash, Recovery, Death }
    public interface IEnemyActionState
    {
        EnemyPhase Phase { get; }
        float Elapsed { get; }
        float Progress { get; }
        float Facing { get; }
        Vector2 Aim { get; }
        bool Vertical { get; }
    }
    public readonly struct EnemyBeam
    {
        public readonly Vector2 Start, End;
        public readonly float Width;
        public EnemyBeam(Vector2 start, Vector2 end, float width) { Start = start; End = end; Width = width; }
    }

    [DisallowMultipleComponent, RequireComponent(typeof(UnitHealth), typeof(Rigidbody2D))]
    public sealed class EnemyCombat2D : MonoBehaviour, IUnitDamageGuard
    {
        [SerializeField] private LayerMask targetMask = 4;
        [SerializeField] private LayerMask obstacleMask = 1;
        [SerializeField] private bool initiallyFacesRight;
        private UnitHealth health;
        private Collider2D body;
        private readonly List<Collider2D> overlaps = new List<Collider2D>(32);
        private readonly List<RaycastHit2D> rays = new List<RaycastHit2D>(16);
        private readonly HashSet<UnitHealth> struck = new HashSet<UnitHealth>();
        public IEnemyActionState State { get; private set; }
        public EnemyFsmDefinition Settings { get; private set; }
        public UnitHealth Target { get; private set; }
        public uint BlockVersion { get; private set; }
        public uint ShotVersion { get; private set; }
        public EnemyBeam LastShot { get; private set; }
        public float InitialFacing => initiallyFacesRight ? 1f : -1f;
        public Vector2 Center => body == null ? (Vector2)transform.position : (Vector2)body.bounds.center;
        public float Facing => State == null ? InitialFacing : State.Facing;
        public bool Alive => health != null && health.IsAlive && health.Owner.isActiveAndEnabled && isActiveAndEnabled;
        private void Awake() { health = GetComponent<UnitHealth>(); body = GetComponent<Collider2D>(); }
        public void Bind(EnemyFsmDefinition settings, IEnemyActionState state) { Settings = settings; State = state; }
        public void ClearAttack() { Target = null; struck.Clear(); LastShot = default; }
        public bool BlocksDamage(Vector2 sourcePosition)
        {
            var settings = Settings != null ? Settings : health.Owner.Definition.Fsm as EnemyFsmDefinition;
            if (settings == null || settings.Pattern != EnemyPattern.Shield || !Alive) return false;
            Vector2 delta = sourcePosition - Center;
            if (delta.x * Facing <= settings.GuardFrontThreshold || Mathf.Abs(delta.y) >= settings.GuardHeight) return false;
            unchecked { BlockVersion++; }
            return true;
        }
        public bool ValidTarget(UnitHealth candidate, float range, float height = float.PositiveInfinity)
        {
            if (!UnitPhysics2D.IsAvailable(candidate) || !candidate.CanReceiveDamage || candidate.Owner.Kind != UnitKind.Player) return false;
            var collider = UnitPhysics2D.Body(candidate);
            if (collider == null) return false;
            Vector2 delta = (Vector2)collider.bounds.center - Center;
            return delta.sqrMagnitude <= range * range && Mathf.Abs(delta.y) <= height && HasSight(Center, collider.bounds.center);
        }
        public bool RefreshTarget(float range, float height = float.PositiveInfinity)
        {
            Physics2D.OverlapCircle(Center, range, UnitPhysics2D.Filter(targetMask), overlaps);
            UnitHealth best = null; float bestDistance = float.PositiveInfinity;
            foreach (var collider in overlaps)
            {
                var candidate = collider.GetComponentInParent<UnitHealth>();
                if (!ValidTarget(candidate, range, height)) continue;
                float distance = ((Vector2)UnitPhysics2D.Body(candidate).bounds.center - Center).sqrMagnitude;
                if (distance < bestDistance || (distance == bestDistance && best != null && candidate.GetInstanceID() < best.GetInstanceID()))
                { best = candidate; bestDistance = distance; }
            }
            Target = best; return best != null;
        }
        public Vector2 TargetCenter
        {
            get { var targetBody = UnitPhysics2D.Body(Target); return targetBody == null ? Center : (Vector2)targetBody.bounds.center; }
        }
        public uint TargetReposition => Target != null && Target.TryGetComponent<IUnitRepositionState>(out var state) ? state.RepositionVersion : 0;
        private static bool IsObstacle(Collider2D collider) => collider != null &&
            !(collider.usedByEffector && collider.TryGetComponent<PlatformEffector2D>(out var effector) && effector.enabled);
        public bool HasSight(Vector2 from, Vector2 to)
        {
            Physics2D.Linecast(from, to, UnitPhysics2D.Filter(obstacleMask), rays);
            foreach (var hit in rays) if (IsObstacle(hit.collider)) return false;
            return true;
        }
        private Vector2 BeamEnd(Vector2 origin, Vector2 direction, float range)
        {
            Physics2D.Raycast(origin, direction, UnitPhysics2D.Filter(obstacleMask), rays, range);
            float nearest = range;
            foreach (var hit in rays) if (IsObstacle(hit.collider)) nearest = Mathf.Min(nearest, hit.distance);
            return origin + direction * nearest;
        }
        public EnemyBeam CalculateBeam(Vector2 aim, bool vertical)
        {
            float width = vertical ? Settings.VerticalBeamWidth : Settings.BeamWidth;
            Vector2 origin = Center;
            if (vertical) return new EnemyBeam(BeamEnd(origin, Vector2.up, Settings.BeamLength), BeamEnd(origin, Vector2.down, Settings.BeamLength), width);
            Vector2 direction = aim.sqrMagnitude > .00001f ? aim.normalized : Vector2.right * Facing;
            return new EnemyBeam(origin, BeamEnd(origin, direction, Settings.BeamLength), width);
        }
        public void Fire(Vector2 aim, bool vertical)
        {
            if (!Alive) return;
            LastShot = CalculateBeam(aim, vertical); unchecked { ShotVersion++; }
            struck.Clear();
            Vector2 delta = LastShot.End - LastShot.Start;
            Physics2D.OverlapBox((LastShot.Start + LastShot.End) * .5f,
                new Vector2(delta.magnitude, LastShot.Width), Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg,
                UnitPhysics2D.Filter(targetMask), overlaps);
            foreach (var collider in overlaps)
            {
                var target = collider.GetComponentInParent<UnitHealth>();
                Damage(target, vertical ? Settings.VerticalDamage : Settings.Damage);
            }
        }
        public void BeginDash() => struck.Clear();
        public void HitDashPath(Vector2 previousCenter)
        {
            if (!Alive) return;
            Vector2 delta = Center - previousCenter;
            Vector2 size = body.bounds.size;
            size += new Vector2(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            Physics2D.OverlapBox((Center + previousCenter) * .5f, size, 0f, UnitPhysics2D.Filter(targetMask), overlaps);
            foreach (var collider in overlaps) Damage(collider.GetComponentInParent<UnitHealth>(), Settings.Damage);
        }
        private void Damage(UnitHealth target, int amount)
        {
            if (!UnitPhysics2D.IsAvailable(target) || !target.CanReceiveDamage || target.Owner.Kind != UnitKind.Player || struck.Contains(target)) return;
            var collider = UnitPhysics2D.Body(target);
            if (collider == null || !HasSight(Center, collider.ClosestPoint(Center))) return;
            struck.Add(target); target.ApplyDamage(amount);
        }
        private void OnDisable() => ClearAttack();
    }
}
