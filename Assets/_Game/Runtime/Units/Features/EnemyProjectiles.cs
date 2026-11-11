using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    public enum ProjectileKind { Bolt, Pellet, Heavy, Shell }

    // What a shot is launched with. A shell (mortar) falls straight down to LandingY and blasts there.
    public struct ProjectileSpec
    {
        public ProjectileKind Kind;
        public Vector2 Origin, Velocity;
        public float Radius, Lifetime;
        public int Damage;
        public bool Light;
        public LayerMask TargetMask, ObstacleMask;
        public float LandingY, BlastRadius, BlastDuration;
    }

    // The barrage family's shots in this scene. Shots outlive their shooter; a cleared encounter (or the scene ending)
    // removes them. Simulation runs on fixed steps; EnemyProjectileView draws the lists.
    [DisallowMultipleComponent]
    public sealed class EnemyProjectiles : MonoBehaviour
    {
        public sealed class Shot
        {
            public int Owner; public ProjectileKind Kind; public Vector2 Position, Velocity;
            public float Radius, Age, Lifetime, LandingY, BlastRadius, BlastDuration; public int Damage; public bool Light;
            public LayerMask TargetMask, ObstacleMask;
            public bool Falling => Kind == ProjectileKind.Shell;
        }
        public sealed class Blast
        {
            public int Owner; public Vector2 Center; public float Radius, Age, Duration; public int Damage;
            public LayerMask TargetMask; public readonly HashSet<UnitHealth> Struck = new HashSet<UnitHealth>();
        }
        public struct Pop { public Vector2 Position; public float Age; }

        private static EnemyProjectiles instance;
        public static EnemyProjectiles Existing => instance;
        public static EnemyProjectiles Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                {
                    // Scene-owned: reloading or leaving the scene drops every shot with it.
                    var go = new GameObject("Enemy projectiles");
                    instance = go.AddComponent<EnemyProjectiles>();
                    go.AddComponent<EnemyProjectileView>();
                }
                return instance;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; }
        public static void ClearExisting() { if (instance != null) instance.ClearAll(); }

        private readonly List<Shot> shots = new List<Shot>();
        private readonly List<Blast> blasts = new List<Blast>();
        private readonly List<Pop> pops = new List<Pop>();
        private readonly List<RaycastHit2D> rays = new List<RaycastHit2D>(16);
        private readonly List<Collider2D> overlaps = new List<Collider2D>(16);
        public IReadOnlyList<Shot> Shots => shots;
        public IReadOnlyList<Blast> Blasts => blasts;
        public IReadOnlyList<Pop> Pops => pops;
        public float PopDuration { get; set; } = .12f;

        private void Awake() { if (instance == null) instance = this; else if (instance != this) Destroy(this); }
        private void OnDestroy() { if (instance == this) instance = null; }

        public void Spawn(int owner, ProjectileSpec spec)
        {
            shots.Add(new Shot
            {
                Owner = owner, Kind = spec.Kind, Position = spec.Origin, Velocity = spec.Velocity, Radius = spec.Radius,
                Lifetime = spec.Lifetime, Damage = spec.Damage, Light = spec.Light, TargetMask = spec.TargetMask,
                ObstacleMask = spec.ObstacleMask, LandingY = spec.LandingY, BlastRadius = spec.BlastRadius, BlastDuration = spec.BlastDuration
            });
        }
        // A shooter waits until every shot (and blast) it made is gone before its next telegraph.
        public int CountOf(int owner)
        {
            int count = 0;
            foreach (var s in shots) if (s.Owner == owner) count++;
            foreach (var b in blasts) if (b.Owner == owner) count++;
            return count;
        }
        public void ClearAll() { shots.Clear(); blasts.Clear(); pops.Clear(); }

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        public void Step(float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime)) return;
            for (int i = shots.Count - 1; i >= 0; i--) if (Advance(shots[i], deltaTime)) shots.RemoveAt(i);
            for (int i = blasts.Count - 1; i >= 0; i--)
            {
                var blast = blasts[i];
                Physics2D.OverlapCircle(blast.Center, blast.Radius, UnitPhysics2D.Filter(blast.TargetMask), overlaps);
                foreach (var collider in overlaps)
                {
                    var target = Player(collider);
                    if (target == null || blast.Struck.Contains(target)) continue;
                    // One real hit per blast; contact while invulnerable does not use it up.
                    if (target.ApplyDirectedDamage(blast.Damage, (Vector2)collider.bounds.center - blast.Center) > 0) blast.Struck.Add(target);
                }
                blast.Age += deltaTime;
                if (blast.Age >= blast.Duration) blasts.RemoveAt(i);
            }
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var pop = pops[i]; pop.Age += deltaTime;
                if (pop.Age >= PopDuration) pops.RemoveAt(i); else pops[i] = pop;
            }
        }

        // True when the shot is gone.
        private bool Advance(Shot shot, float dt)
        {
            shot.Age += dt;
            if (shot.Age >= shot.Lifetime) return true;
            Vector2 step = shot.Velocity * dt;
            if (shot.Falling)
            {
                // A drop goes to the ground fixed at its telegraph; terrain on the way does not stop it.
                Vector2 next = shot.Position + step;
                bool landed = !float.IsNaN(shot.LandingY) && next.y - shot.Radius <= shot.LandingY;
                if (landed) next.y = shot.LandingY + shot.Radius;
                shot.Position = next;
                // A direct hit removes the shell without a ground blast.
                if (HitPlayer(shot)) return true;
                if (!landed) return false;
                blasts.Add(new Blast
                {
                    Owner = shot.Owner, Center = new Vector2(next.x, shot.LandingY), Radius = shot.BlastRadius,
                    Duration = shot.BlastDuration, Damage = shot.Damage, TargetMask = shot.TargetMask
                });
                return true;
            }
            float distance = step.magnitude;
            bool walled = false;
            if (distance > 0f)
            {
                Physics2D.CircleCast(shot.Position, shot.Radius, step / distance, UnitPhysics2D.Filter(shot.ObstacleMask), rays, distance);
                float nearest = distance;
                foreach (var hit in rays) if (IsObstacle(hit.collider) && hit.distance < nearest) { nearest = hit.distance; walled = true; }
                shot.Position += step / distance * nearest;
            }
            if (HitPlayer(shot)) return true;
            if (walled) { AddPop(shot.Position); return true; }
            return false;
        }

        private bool HitPlayer(Shot shot)
        {
            Physics2D.OverlapCircle(shot.Position, shot.Radius, UnitPhysics2D.Filter(shot.TargetMask), overlaps);
            foreach (var collider in overlaps)
            {
                var target = Player(collider);
                if (target == null) continue;
                // Invulnerable contact still removes the shot; only the pop is shown.
                Vector2 direction = shot.Velocity.sqrMagnitude > .000001f ? shot.Velocity : (Vector2)collider.bounds.center - shot.Position;
                target.ApplyDirectedDamage(shot.Damage, direction, shot.Light);
                AddPop(shot.Position);
                return true;
            }
            return false;
        }

        // Only a player who can be hit right now takes part (a burrowed player is passed over).
        private static UnitHealth Player(Collider2D collider)
        {
            var target = collider.GetComponentInParent<UnitHealth>();
            return UnitPhysics2D.IsAvailable(target) && target.CanReceiveDamage && target.Owner.Kind == UnitKind.Player &&
                UnitPhysics2D.Body(target) == collider ? target : null;
        }
        private void AddPop(Vector2 position) => pops.Add(new Pop { Position = position });
        // One-way platforms do not stop shots, like enemy beams.
        public static bool IsObstacle(Collider2D collider) => collider != null &&
            !(collider.usedByEffector && collider.TryGetComponent<PlatformEffector2D>(out var effector) && effector.enabled);
    }
}
