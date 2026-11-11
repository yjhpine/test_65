using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Units.Fsm
{
    // Barrage (straight, shotgun, heavy, mortar), sweep laser and domain enemies. Shared rule: recognized once for the
    // recognition delay, then a fixed 0.4 s telegraph, the action, and the next telegraph no sooner than the attack interval
    // after the last one began and only once this unit's shots and field are gone.
    // Lengths are in units (1 unit = 47.5 px of the design document).
    [CreateAssetMenu(menuName = "Action Platformer/Units/FSM/Ranged Enemy")]
    public sealed class RangedFsmDefinition : FsmDefinition
    {
        [SerializeField] private RangedAttack attack;
        [Header("Common")]
        [SerializeField, Min(.1f)] private float detectionRange = 13.05f;
        [Tooltip("Counted once from the first recognition; seeing the player again does not start it over.")]
        [SerializeField, Min(0)] private float recognitionDelay = .2f;
        [SerializeField, Min(.01f)] private float telegraph = .4f;
        [Tooltip("Shortest time from one telegraph's start to the next.")]
        [SerializeField, Min(0)] private float attackInterval = 5f;
        [SerializeField, Min(0)] private int damage = 10;
        [Tooltip("Straight/shotgun shots: no knockback, freeze or interrupted action; the next glitch waits a moment.")]
        [SerializeField] private bool lightHit;
        [SerializeField, Min(0)] private float deathDuration = .6f;
        [Header("Shots")]
        [SerializeField, Min(.01f)] private float shotSpeed = 4.42f;
        [SerializeField, Min(.01f)] private float shotRadius = .295f;
        [SerializeField, Min(.01f)] private float shotLifetime = 6f;
        [Tooltip("Shotgun: the outer shots' angle from the aim (degrees).")]
        [SerializeField, Range(0f, 90f)] private float spreadAngle = 12f;
        [Header("Mortar")]
        [SerializeField, Min(.01f)] private float trackSpeed = 3.79f;
        [Tooltip("Largest horizontal distance to the player at which the drop telegraph may start.")]
        [SerializeField, Min(.01f)] private float alignDistance = 1.26f;
        [SerializeField, Min(.01f)] private float returnSpeed = 4f;
        [SerializeField, Min(.01f)] private float blastRadius = 1.26f;
        [SerializeField, Min(.01f)] private float blastDuration = .12f;
        [Header("Sweep laser")]
        [SerializeField, Range(1f, 180f)] private float sweepAngle = 60f;
        [SerializeField, Min(.01f)] private float sweepDuration = .8f;
        [SerializeField, Min(0)] private float afterimage = .15f;
        [SerializeField, Min(.01f)] private float laserLength = 13.05f;
        [SerializeField, Min(.01f)] private float laserWidth = .21f;
        [Header("Domain")]
        [SerializeField] private Vector2 domainSize = new Vector2(5.05f, 4.21f);
        [SerializeField, Min(.01f)] private float domainDuration = 2f;

        public RangedAttack Attack => attack;
        public float DetectionRange => detectionRange;
        public float RecognitionDelay => recognitionDelay;
        public float Telegraph => telegraph;
        public float AttackInterval => attackInterval;
        public int Damage => damage;
        public bool LightHit => lightHit;
        public float DeathDuration => deathDuration;
        public float ShotSpeed => shotSpeed;
        public float ShotRadius => shotRadius;
        public float ShotLifetime => shotLifetime;
        public float SpreadAngle => spreadAngle;
        public float TrackSpeed => trackSpeed;
        public float AlignDistance => alignDistance;
        public float BlastRadius => blastRadius;
        public float BlastDuration => blastDuration;
        public float SweepAngleRange => sweepAngle;
        public float SweepDuration => sweepDuration;
        public float Afterimage => afterimage;
        public float LaserLength => laserLength;
        public float LaserWidth => laserWidth;
        public Vector2 DomainSize => domainSize;
        public float DomainDuration => domainDuration;
        public bool IsDomain => attack == RangedAttack.SelfDomain || attack == RangedAttack.FixedDomain;
        public bool IsShooter => attack == RangedAttack.Straight || attack == RangedAttack.Shotgun || attack == RangedAttack.Heavy;

        public bool TryValidate(out string error)
        {
            float[] positive = { detectionRange, telegraph, shotSpeed, shotRadius, shotLifetime, trackSpeed, alignDistance, returnSpeed,
                blastRadius, blastDuration, sweepAngle, sweepDuration, laserLength, laserWidth, domainSize.x, domainSize.y, domainDuration };
            float[] nonnegative = { recognitionDelay, attackInterval, deathDuration, afterimage, spreadAngle };
            foreach (float value in positive) if (!(value > 0f) || float.IsInfinity(value)) { error = "Ranged enemy speeds, sizes and durations must be positive and finite."; return false; }
            foreach (float value in nonnegative) if (!(value >= 0f) || float.IsInfinity(value)) { error = "Ranged enemy delays must be nonnegative and finite."; return false; }
            if (!System.Enum.IsDefined(typeof(RangedAttack), attack) || damage < 0) { error = "Invalid ranged attack or damage."; return false; }
            error = null; return true;
        }

        protected override IFsmState CreateInitialState(Unit owner)
        {
            if (!TryValidate(out string error)) throw new System.InvalidOperationException(error);
            if (!owner.TryGetComponent<RangedCombat2D>(out var combat) || !owner.TryGetComponent<UnitHealth>(out var health))
                throw new System.InvalidOperationException("Ranged enemy requires RangedCombat2D and UnitHealth.");
            var ground = owner.GetComponent<GroundMovement2D>(); var flying = owner.GetComponent<FlyingMovement2D>();
            if (attack == RangedAttack.Mortar ? flying == null : ground == null)
                throw new System.InvalidOperationException("Ranged enemy is missing its movement feature (mortar: FlyingMovement2D, others: GroundMovement2D).");
            return new Context(owner, combat, health, ground, flying, this).Idle;
        }

        // One graph and timer set per unit; the definition is read only.
        private sealed class Context : IRangedActionState
        {
            public readonly Unit Owner; public readonly RangedCombat2D Combat; public readonly UnitHealth Health;
            public readonly GroundMovement2D Ground; public readonly FlyingMovement2D Flying; public readonly RangedFsmDefinition S;
            public readonly State Idle, Prepare, Sweep, Afterimage, React, Death;
            public RangedPhase Phase { get; set; }
            public float Elapsed { get; set; }
            public float Duration;
            public float Progress => Duration <= 0f ? 1f : Mathf.Clamp01(Elapsed / Duration);
            public float Facing { get; set; }
            public Vector2 Aim { get; set; }
            public Vector2 LandingPoint { get; set; }
            public float SweepAngle { get; set; }
            public float SweepFrom { get; set; }
            public float SweepTo { get; set; }
            public float Recognized, SinceTelegraph = float.PositiveInfinity;
            public uint ObservedInterrupt;
            public Context(Unit owner, RangedCombat2D combat, UnitHealth health, GroundMovement2D ground, FlyingMovement2D flying, RangedFsmDefinition settings)
            {
                Owner = owner; Combat = combat; Health = health; Ground = ground; Flying = flying; S = settings; Facing = combat.InitialFacing;
                ObservedInterrupt = health.InterruptVersion;
                Idle = new State(this, RangedPhase.Idle); Prepare = new State(this, RangedPhase.Preparing); Sweep = new State(this, RangedPhase.Sweeping);
                Afterimage = new State(this, RangedPhase.Afterimage); React = new State(this, RangedPhase.Reacting); Death = new State(this, RangedPhase.Death);
                combat.Bind(settings, this);
                flying?.Configure(0f, 0f, settings.trackSpeed, settings.returnSpeed);
            }
            public bool Mortar => S.attack == RangedAttack.Mortar;
            public void Stop() { Ground?.Stop(); if (Flying != null && !Flying.IsForcedMoving) Flying.Hover(); }
            // Ground units start a telegraph only standing; the mortar only at its height, not moved.
            public bool Settled => Mortar ? !Flying.IsForcedMoving && !Flying.IsReturning : !Ground.IsForcedMoving && Ground.IsGrounded;
            public void Face(Vector2 point) { float dx = point.x - Combat.Center.x; if (Mathf.Abs(dx) > .01f) Facing = Mathf.Sign(dx); }
        }

        private sealed class State : IFsmState
        {
            private readonly Context c; private readonly RangedPhase phase;
            private RangedFsmDefinition S => c.S;
            public State(Context context, RangedPhase value) { c = context; phase = value; }
            public void Enter()
            {
                c.Phase = phase; c.Elapsed = 0f; c.Duration = 0f;
                switch (phase)
                {
                    case RangedPhase.Idle: c.Stop(); break;
                    case RangedPhase.Preparing:
                        c.Stop(); c.SinceTelegraph = 0f; c.Duration = S.telegraph;
                        // Fixed here: the aim (straight/shotgun/heavy/laser) and the drop point. Never re-aimed afterwards.
                        Vector2 target = c.Combat.TargetCenter; c.Face(target);
                        Vector2 delta = target - c.Combat.Center;
                        c.Aim = delta.sqrMagnitude > .00001f ? delta.normalized : Vector2.right * c.Facing;
                        if (c.Mortar) c.LandingPoint = new Vector2(c.Combat.Center.x, c.Combat.FindGround());
                        if (S.attack == RangedAttack.SweepLaser)
                        {
                            // Clockwise on screen: from aim + half to aim - half in world degrees.
                            float aim = Mathf.Atan2(c.Aim.y, c.Aim.x) * Mathf.Rad2Deg;
                            c.SweepFrom = c.SweepAngle = aim + S.sweepAngle * .5f; c.SweepTo = aim - S.sweepAngle * .5f;
                        }
                        break;
                    case RangedPhase.Sweeping: c.Stop(); c.Duration = S.sweepDuration; c.SweepAngle = c.SweepFrom; c.Combat.BeginSweep(c.SweepFrom); break;
                    case RangedPhase.Afterimage: c.Duration = S.afterimage; c.Combat.EndSweep(); break;
                    case RangedPhase.Reacting: c.Combat.CancelAction(); break;
                    case RangedPhase.Death:
                        // Shots already fired stay; the telegraph, the laser and this unit's field go with it.
                        c.Combat.CancelAction(); c.Combat.CloseDomain(); c.Stop(); c.Duration = S.deathDuration; break;
                }
            }
            public IFsmState Tick(float deltaTime)
            {
                if (!c.Health.IsAlive && phase != RangedPhase.Death) return c.Death;
                c.SinceTelegraph += deltaTime;
                if (phase != RangedPhase.Death) c.Combat.TickDomain(deltaTime);
                // Ordinary hits change nothing; a launch/slam cancels the telegraph or sweep (an open field stays).
                if (c.Health.InterruptVersion != c.ObservedInterrupt)
                {
                    c.ObservedInterrupt = c.Health.InterruptVersion;
                    if (phase != RangedPhase.Death && phase != RangedPhase.Reacting) return c.React;
                }
                c.Elapsed += deltaTime;
                switch (phase)
                {
                    case RangedPhase.Idle: return IdleTick(deltaTime);
                    case RangedPhase.Preparing:
                        if (!c.Combat.Recognizes()) return c.Idle; // cancelled; the attack interval still counts from it
                        if (c.Elapsed < c.Duration) return null;
                        switch (S.attack)
                        {
                            case RangedAttack.Mortar: c.Combat.Drop(c.LandingPoint.y); return c.Idle;
                            case RangedAttack.SweepLaser: return c.Sweep;
                            case RangedAttack.SelfDomain: case RangedAttack.FixedDomain: c.Combat.OpenDomain(); return c.Idle;
                            default: c.Combat.FireShots(c.Aim); return c.Idle;
                        }
                    case RangedPhase.Sweeping:
                        // Started sweeps finish even if the player leaves the fan or hides.
                        c.SweepAngle = Mathf.Lerp(c.SweepFrom, c.SweepTo, c.Progress);
                        c.Combat.SweepTo(c.SweepAngle);
                        return c.Elapsed >= c.Duration ? c.Afterimage : null;
                    case RangedPhase.Afterimage:
                        return c.Elapsed >= c.Duration ? c.Idle : null;
                    case RangedPhase.Reacting:
                        // A slammed mortar falls, then flies straight back up to its height without firing.
                        if (c.Flying != null && !c.Flying.IsForcedMoving) c.Flying.Hover();
                        return c.Settled ? c.Idle : null;
                    case RangedPhase.Death:
                        if (c.Elapsed >= c.Duration) c.Owner.gameObject.SetActive(false);
                        return null;
                }
                return null;
            }
            private IFsmState IdleTick(float deltaTime)
            {
                c.Ground?.Stop();
                if (!c.Combat.RefreshTarget(S.detectionRange) || !c.Combat.TargetSeesUs())
                {
                    if (c.Flying != null && !c.Flying.IsForcedMoving) c.Flying.Hover();
                    return null;
                }
                // The first recognition delay is counted once and never given again.
                if (c.Recognized < S.recognitionDelay) c.Recognized += deltaTime;
                Vector2 target = c.Combat.TargetCenter;
                c.Face(target);
                if (c.Mortar && !c.Flying.IsForcedMoving) c.Flying.Follow(target.x, S.trackSpeed);
                if (c.Recognized < S.recognitionDelay || c.SinceTelegraph < S.attackInterval || c.Combat.HasActiveWork || !c.Settled) return null;
                if (c.Mortar && Mathf.Abs(target.x - c.Combat.Center.x) > S.alignDistance) return null;
                return c.Prepare;
            }
            public void Exit()
            {
                if (phase == RangedPhase.Sweeping) c.Combat.EndSweep();
                if (!c.Owner.isActiveAndEnabled)
                {
                    c.Combat.CancelAction(); c.Combat.CloseDomain(); c.Stop();
                    c.Facing = c.Combat.InitialFacing; c.Elapsed = 0f; c.Phase = RangedPhase.Idle;
                    c.Recognized = 0f; c.SinceTelegraph = float.PositiveInfinity;
                }
            }
        }
    }
}
