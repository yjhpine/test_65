using System.Collections.Generic;
using UnityEngine;
using ActionPlatformer.Units.Fsm;

namespace ActionPlatformer.Units.Features
{
    public enum EnemyPattern { Shield, Surveillance, Drone }
    // Shield units: Rush dashes along a path fixed by its telegraph; Strike hits once in front, in place.
    public enum ShieldAttack { Rush, Strike }
    // Strike and Reacting are shield-only: the in-place hit, and the launched/slammed state with the shield down.
    public enum EnemyPhase { Idle, Turning, Preparing, Locked, Dash, Recovery, Death, Strike, Reacting }
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
        [Tooltip("Turret head rotation pivot, relative to the unit root. Other patterns use the body center.")]
        [SerializeField] private Vector2 aimPivotOffset;
        [SerializeField, Min(0f)] private float muzzleDistance;
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
        public Vector2 AimOrigin => Settings != null && Settings.Pattern == EnemyPattern.Surveillance ?
            (Vector2)transform.TransformPoint(aimPivotOffset) : Center;
        public float Facing => State == null ? InitialFacing : State.Facing;
        public bool Alive => health != null && health.IsAlive && health.Owner.isActiveAndEnabled && isActiveAndEnabled;
        private void Awake() { health = GetComponent<UnitHealth>(); body = GetComponent<Collider2D>(); }
        public void Bind(EnemyFsmDefinition settings, IEnemyActionState state) { Settings = settings; State = state; }
        public void ClearAttack() { Target = null; struck.Clear(); LastShot = default; }
        // The shield is up except while launched/slammed and recovering from it, and in death.
        public bool Guarding => State == null || (State.Phase != EnemyPhase.Reacting && State.Phase != EnemyPhase.Death);
        // Attacks from in front of the shield are blocked at any height, front diagonals included; from behind
        // or directly above/below (the attacker's center over the body column) they pass, whatever the attack kind.
        public bool BlocksDamage(Vector2 sourcePosition)
        {
            var settings = Settings != null ? Settings : health.Owner.Definition.Fsm as EnemyFsmDefinition;
            if (settings == null || settings.Pattern != EnemyPattern.Shield || !Alive || !Guarding) return false;
            if ((sourcePosition.x - Center.x) * Facing <= settings.GuardDirectBand) return false;
            unchecked { BlockVersion++; }
            return true;
        }
        // Recognition also needs the unit on the player's screen, when the player reports a view.
        public bool TargetSeesUs()
        {
            if (Target == null || !Target.TryGetComponent<IUnitViewSource>(out var source) || source.View == null) return true;
            Vector3 viewport = source.View.WorldToViewportPoint(Center);
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }
        // The shield attack's reach: the strike box in front of the body, or the rush path ahead of it.
        public void ShieldAttackArea(float facing, out Vector2 center, out Vector2 size)
        {
            float half = body == null ? .5f : body.bounds.extents.x, height = body == null ? 1f : body.bounds.size.y;
            float length = Settings.ShieldAttack == ShieldAttack.Strike ? Settings.StrikeReach : Settings.DashMaxDistance;
            center = Center + Vector2.right * facing * (half + length * .5f);
            size = new Vector2(length, height);
        }
        // One check at the moment of the strike: the box in front, the target's own body and terrain sight.
        public void Strike(float facing)
        {
            if (!Alive) return;
            struck.Clear();
            ShieldAttackArea(facing, out var center, out var size);
            Physics2D.OverlapBox(center, size, 0f, UnitPhysics2D.Filter(targetMask), overlaps);
            foreach (var collider in overlaps) Damage(collider.GetComponentInParent<UnitHealth>(), Settings.Damage, Center);
        }
        public bool ValidTarget(UnitHealth candidate, float range, float height = float.PositiveInfinity)
        {
            if (!UnitPhysics2D.IsAvailable(candidate) || !candidate.CanReceiveDamage || candidate.Owner.Kind != UnitKind.Player) return false;
            var collider = UnitPhysics2D.Body(candidate);
            if (collider == null) return false;
            Vector2 delta = (Vector2)collider.bounds.center - Center;
            return delta.sqrMagnitude <= range * range && Mathf.Abs(delta.y) <= height && HasSight(Center, collider.bounds.center);
        }
        public bool RefreshTarget(float range, float height = float.PositiveInfinity, float facing = 0f)
        {
            Physics2D.OverlapCircle(Center, range, UnitPhysics2D.Filter(targetMask), overlaps);
            UnitHealth best = null; float bestDistance = float.PositiveInfinity;
            foreach (var collider in overlaps)
            {
                var candidate = collider.GetComponentInParent<UnitHealth>();
                if (!ValidTarget(candidate, range, height)) continue;
                Vector2 delta = (Vector2)UnitPhysics2D.Body(candidate).bounds.center - Center;
                // Facing is only an acquisition filter; an acquired target can still be tracked behind us.
                if (facing != 0f && delta.x * facing < 0f) continue;
                float distance = delta.sqrMagnitude;
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
            if (Settings.Pattern == EnemyPattern.Surveillance)
            {
                origin = AimOrigin + direction * muzzleDistance;
                // A muzzle protruding through nearby terrain must not shoot through that terrain.
                if (!HasSight(AimOrigin, origin)) return new EnemyBeam(origin, origin, width);
            }
            return new EnemyBeam(origin, BeamEnd(origin, direction, Settings.BeamLength), width);
        }
        public void Fire(Vector2 aim, bool vertical)
        {
            if (!Alive) return;
            LastShot = CalculateBeam(aim, vertical); unchecked { ShotVersion++; }
            struck.Clear();
            Vector2 delta = LastShot.End - LastShot.Start;
            if (delta.sqrMagnitude <= .000001f) return;
            Physics2D.OverlapBox((LastShot.Start + LastShot.End) * .5f,
                new Vector2(delta.magnitude, LastShot.Width), Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg,
                UnitPhysics2D.Filter(targetMask), overlaps);
            foreach (var collider in overlaps)
            {
                var target = collider.GetComponentInParent<UnitHealth>();
                Damage(target, vertical ? Settings.VerticalDamage : Settings.Damage,
                    Settings.Pattern == EnemyPattern.Surveillance ? LastShot.Start : Center);
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
            foreach (var collider in overlaps) Damage(collider.GetComponentInParent<UnitHealth>(), Settings.Damage, Center);
        }
        private void Damage(UnitHealth target, int amount, Vector2 origin)
        {
            if (!UnitPhysics2D.IsAvailable(target) || !target.CanReceiveDamage || target.Owner.Kind != UnitKind.Player || struck.Contains(target)) return;
            var collider = UnitPhysics2D.Body(target);
            if (collider == null || !HasSight(origin, collider.ClosestPoint(origin))) return;
            struck.Add(target); target.ApplyDirectedDamage(amount, (Vector2)collider.bounds.center - origin);
        }
        private void OnDisable() => ClearAttack();
    }
}
