using System.Collections.Generic;
using ActionPlatformer.Units.Fsm;
using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    // The barrage, laser and domain enemies (design: Docs/superpowers/specs/2026-11-05-ranged-enemy-family-design.md).
    public enum RangedAttack { Straight, Shotgun, Heavy, Mortar, SweepLaser, SelfDomain, FixedDomain }
    public enum RangedPhase { Idle, Preparing, Sweeping, Afterimage, Reacting, Death }
    public interface IRangedActionState
    {
        RangedPhase Phase { get; }
        float Elapsed { get; }
        float Progress { get; }
        float Facing { get; }
        // Fixed at the telegraph's start: the aim direction and, for the mortar, the ground point under it.
        Vector2 Aim { get; }
        Vector2 LandingPoint { get; }
        // The laser's current angle (degrees, world) and the sweep's start and end.
        float SweepAngle { get; }
        float SweepFrom { get; }
        float SweepTo { get; }
    }

    [DisallowMultipleComponent, RequireComponent(typeof(UnitHealth), typeof(Rigidbody2D))]
    public sealed class RangedCombat2D : MonoBehaviour
    {
        [SerializeField] private LayerMask targetMask = 4;
        [SerializeField] private LayerMask obstacleMask = 1;
        [SerializeField] private bool initiallyFacesRight;
        [Tooltip("Fixed-area domain only: world-space center of the area this unit deploys. Never re-aimed at the player.")]
        [SerializeField] private Vector2 domainCenter;
        private UnitHealth health;
        private Collider2D body;
        private readonly List<Collider2D> overlaps = new List<Collider2D>(32);
        private readonly List<RaycastHit2D> rays = new List<RaycastHit2D>(16);
        private readonly HashSet<UnitHealth> sweepStruck = new HashSet<UnitHealth>();
        private DomainArea area;
        private DomainArea domain => area ?? (area = new DomainArea(this));
        private float sweepAngle;
        public IRangedActionState State { get; private set; }
        public RangedFsmDefinition Settings { get; private set; }
        public UnitHealth Target { get; private set; }
        // Every shot, sweep start and domain opening.
        public uint ShotVersion { get; private set; }
        public float InitialFacing => initiallyFacesRight ? 1f : -1f;
        public float Facing => State == null ? InitialFacing : State.Facing;
        public Vector2 Center => body == null ? (Vector2)transform.position : (Vector2)body.bounds.center;
        public bool Alive => health != null && health.IsAlive && health.Owner != null && health.Owner.isActiveAndEnabled && isActiveAndEnabled;
        public int OwnerId => GetInstanceID();
        public int ActiveShots => EnemyProjectiles.Existing == null ? 0 : EnemyProjectiles.Existing.CountOf(OwnerId);
        public bool DomainActive => domain.Active;
        public float DomainRemaining => domain.Active ? domain.Remaining : 0f;
        public IGlitchHazard Domain => domain;
        // The next telegraph waits for this unit's own shots and field to be gone.
        public bool HasActiveWork => ActiveShots > 0 || domain.Active;
        public bool SweepActive { get; private set; }
        public EnemyBeam CurrentBeam { get; private set; }
        public Vector2 FixedDomainCenter { get => domainCenter; set => domainCenter = value; }
        public Vector2 DomainCenterNow => Settings != null && Settings.Attack == RangedAttack.FixedDomain ? domainCenter : Center;
        public Vector2 DomainSize => Settings == null ? Vector2.zero : Settings.DomainSize;

        private void Awake() { health = GetComponent<UnitHealth>(); body = GetComponent<Collider2D>(); }
        public void Bind(RangedFsmDefinition settings, IRangedActionState state) { Settings = settings; State = state; }

        // Recognition: a living player (burrowed too) within range, with terrain sight. Screen visibility is TargetSeesUs.
        public bool ValidTarget(UnitHealth candidate, float range)
        {
            if (!UnitPhysics2D.IsAvailable(candidate) || candidate.Owner.Kind != UnitKind.Player) return false;
            var collider = UnitPhysics2D.Body(candidate);
            if (collider == null) return false;
            Vector2 delta = (Vector2)collider.bounds.center - Center;
            return delta.sqrMagnitude <= range * range && HasSight(Center, collider.bounds.center);
        }
        public bool RefreshTarget(float range)
        {
            Physics2D.OverlapCircle(Center, range, UnitPhysics2D.Filter(targetMask), overlaps);
            UnitHealth best = null; float bestDistance = float.PositiveInfinity;
            foreach (var collider in overlaps)
            {
                var candidate = collider.GetComponentInParent<UnitHealth>();
                if (!ValidTarget(candidate, range)) continue;
                float distance = ((Vector2)UnitPhysics2D.Body(candidate).bounds.center - Center).sqrMagnitude;
                if (distance < bestDistance) { best = candidate; bestDistance = distance; }
            }
            Target = best; return best != null;
        }
        // The unit must be on the player's screen, when the player reports a view.
        public bool TargetSeesUs()
        {
            if (Target == null || !Target.TryGetComponent<IUnitViewSource>(out var source) || source.View == null) return true;
            Vector3 viewport = source.View.WorldToViewportPoint(Center);
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }
        public bool Recognizes() => Target != null && ValidTarget(Target, Settings.DetectionRange) && TargetSeesUs();
        public Vector2 TargetCenter
        {
            get { var targetBody = UnitPhysics2D.Body(Target); return targetBody == null ? Center : (Vector2)targetBody.bounds.center; }
        }
        public bool HasSight(Vector2 from, Vector2 to)
        {
            Physics2D.Linecast(from, to, UnitPhysics2D.Filter(obstacleMask), rays);
            foreach (var hit in rays) if (EnemyProjectiles.IsObstacle(hit.collider)) return false;
            return true;
        }

        // ---- Shots ----
        public int ShotCount => Settings.Attack == RangedAttack.Shotgun ? 3 : 1;
        public Vector2 ShotDirection(Vector2 aim, int index)
        {
            Vector2 direction = aim.sqrMagnitude > .00001f ? aim.normalized : Vector2.right * Facing;
            if (ShotCount == 1) return direction;
            float angle = (index - 1) * Settings.SpreadAngle * Mathf.Deg2Rad;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return new Vector2(direction.x * c - direction.y * s, direction.x * s + direction.y * c);
        }
        // The straight path a shot will take until the first wall, for the telegraph (capped at the detection range).
        public EnemyBeam ShotPath(Vector2 direction)
        {
            float range = Settings.DetectionRange, radius = Settings.ShotRadius;
            Physics2D.CircleCast(Center, radius, direction, UnitPhysics2D.Filter(obstacleMask), rays, range);
            float nearest = range;
            foreach (var hit in rays) if (EnemyProjectiles.IsObstacle(hit.collider)) nearest = Mathf.Min(nearest, hit.distance);
            return new EnemyBeam(Center, Center + direction * nearest, radius * 2f);
        }
        public void FireShots(Vector2 aim)
        {
            if (!Alive) return;
            unchecked { ShotVersion++; }
            var kind = Settings.Attack == RangedAttack.Shotgun ? ProjectileKind.Pellet : Settings.Attack == RangedAttack.Heavy ? ProjectileKind.Heavy : ProjectileKind.Bolt;
            for (int i = 0; i < ShotCount; i++)
                EnemyProjectiles.Instance.Spawn(OwnerId, new ProjectileSpec
                {
                    Kind = kind, Origin = Center, Velocity = ShotDirection(aim, i) * Settings.ShotSpeed, Radius = Settings.ShotRadius,
                    Lifetime = Settings.ShotLifetime, Damage = Settings.Damage, Light = Settings.LightHit,
                    TargetMask = targetMask, ObstacleMask = obstacleMask, LandingY = float.NaN
                });
        }
        // The ground straight below: the nearest terrain or platform top. NaN when there is none.
        public float FindGround()
        {
            Physics2D.Raycast(Center, Vector2.down, UnitPhysics2D.Filter(obstacleMask), rays, 60f);
            float best = float.NaN, nearest = float.PositiveInfinity;
            foreach (var hit in rays)
                if (hit.collider != null && hit.distance < nearest && !(body != null && hit.collider == body)) { nearest = hit.distance; best = hit.point.y; }
            return best;
        }
        public void Drop(float landingY)
        {
            if (!Alive) return;
            unchecked { ShotVersion++; }
            EnemyProjectiles.Instance.Spawn(OwnerId, new ProjectileSpec
            {
                Kind = ProjectileKind.Shell, Origin = Center, Velocity = Vector2.down * Settings.ShotSpeed, Radius = Settings.ShotRadius,
                Lifetime = Settings.ShotLifetime, Damage = Settings.Damage, TargetMask = targetMask, ObstacleMask = obstacleMask,
                LandingY = landingY, BlastRadius = Settings.BlastRadius, BlastDuration = Settings.BlastDuration
            });
        }

        // ---- Sweep laser ----
        public EnemyBeam BeamAt(float degrees)
        {
            Vector2 direction = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
            float range = Settings.LaserLength;
            Physics2D.Raycast(Center, direction, UnitPhysics2D.Filter(obstacleMask), rays, range);
            float nearest = range;
            foreach (var hit in rays) if (EnemyProjectiles.IsObstacle(hit.collider)) nearest = Mathf.Min(nearest, hit.distance);
            return new EnemyBeam(Center, Center + direction * nearest, Settings.LaserWidth);
        }
        public void BeginSweep(float degrees)
        {
            if (!Alive) return;
            sweepStruck.Clear(); SweepActive = true; sweepAngle = degrees;
            unchecked { ShotVersion++; }
            CurrentBeam = BeamAt(degrees); HitBeam(CurrentBeam);
        }
        // Only the live line hurts. Angles crossed since the last update are filled in at 1 degree so a fast frame skips nothing.
        public void SweepTo(float degrees)
        {
            if (!SweepActive || !Alive) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(degrees - sweepAngle)));
            for (int i = 1; i <= steps; i++) HitBeam(BeamAt(Mathf.Lerp(sweepAngle, degrees, i / (float)steps)));
            sweepAngle = degrees; CurrentBeam = BeamAt(degrees);
        }
        public void EndSweep() => SweepActive = false;
        private void HitBeam(EnemyBeam beam)
        {
            Vector2 delta = beam.End - beam.Start;
            if (delta.sqrMagnitude <= .000001f) return;
            Physics2D.OverlapBox((beam.Start + beam.End) * .5f, new Vector2(delta.magnitude, beam.Width),
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, UnitPhysics2D.Filter(targetMask), overlaps);
            foreach (var collider in overlaps)
            {
                var target = collider.GetComponentInParent<UnitHealth>();
                if (!UnitPhysics2D.IsAvailable(target) || !target.CanReceiveDamage || target.Owner.Kind != UnitKind.Player || sweepStruck.Contains(target)) continue;
                // One real hit per sweep: contact while invulnerable does not use it.
                if (target.ApplyDirectedDamage(Settings.Damage, (Vector2)collider.bounds.center - beam.Start) > 0) sweepStruck.Add(target);
            }
        }

        // ---- Domain ----
        public void OpenDomain()
        {
            if (!Alive) return;
            unchecked { ShotVersion++; }
            domain.Open(Settings.DomainDuration);
        }
        public void TickDomain(float deltaTime) => domain.Tick(deltaTime);
        public void CloseDomain() => domain.Close();

        // A launch/slam stops the telegraph and any sweep; a field already open stays for its time.
        public void CancelAction() { Target = null; SweepActive = false; sweepStruck.Clear(); }
        private void OnDisable() { CancelAction(); CloseDomain(); CurrentBeam = default; }

        private sealed class DomainArea : IGlitchHazard
        {
            private readonly RangedCombat2D owner;
            public DomainArea(RangedCombat2D value) { owner = value; }
            public bool Active { get; private set; }
            public float Remaining { get; private set; }
            public void Open(float duration) { Remaining = duration; Active = duration > 0f; if (Active) GlitchHazards.Add(this); }
            public void Tick(float deltaTime) { if (!Active) return; Remaining -= deltaTime; if (Remaining <= 0f) Close(); }
            public void Close() { Active = false; Remaining = 0f; GlitchHazards.Remove(this); }
            // The player's center counts as inside on the rectangle's edge too.
            public bool ContainsPoint(Vector2 point)
            {
                if (!Active || owner == null) return false;
                Vector2 half = owner.DomainSize * .5f, delta = point - owner.DomainCenterNow;
                return Mathf.Abs(delta.x) <= half.x && Mathf.Abs(delta.y) <= half.y;
            }
        }

        private void OnDrawGizmosSelected()
        {
            var fsm = GetComponent<Unit>() != null ? GetComponent<Unit>().Definition?.Fsm as RangedFsmDefinition : null;
            if (fsm == null || (fsm.Attack != RangedAttack.FixedDomain && fsm.Attack != RangedAttack.SelfDomain)) return;
            Gizmos.color = new Color(.65f, .3f, 1f, .8f);
            Vector3 center = fsm.Attack == RangedAttack.FixedDomain ? (Vector3)domainCenter : transform.position;
            Gizmos.DrawWireCube(center, fsm.DomainSize);
        }
    }
}
