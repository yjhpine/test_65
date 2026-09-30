using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Units.Fsm
{
    [CreateAssetMenu(menuName = "Action Platformer/Units/FSM/Enemy Pattern")]
    public sealed class EnemyFsmDefinition : FsmDefinition
    {
        [SerializeField] private EnemyPattern pattern;
        [Header("Common")]
        [SerializeField, Min(.1f)] private float detectionRange = 4.4f;
        [SerializeField, Min(0)] private int damage = 14;
        [SerializeField, Min(.01f)] private float firstPreparation = 1.2f;
        [SerializeField, Min(.01f)] private float preparation = .85f;
        [SerializeField, Min(0)] private float recovery = 1.2f;
        [SerializeField, Min(0)] private float deathDuration = .6f;
        [Header("Shield")]
        [SerializeField, Min(.01f)] private float detectionHeight = 2.8f;
        [SerializeField, Min(.01f)] private float approachHeight = 1.4f;
        [SerializeField, Min(.01f)] private float approachDistance = 2.2f;
        [SerializeField, Min(.01f)] private float approachSpeed = 1.3f;
        [SerializeField, Min(0)] private float turnDuration = .55f;
        [SerializeField, Min(.01f)] private float dashSpeed = 13.6f;
        [SerializeField, Min(.01f)] private float dashMinDistance = 2f;
        [SerializeField, Min(.01f)] private float dashMaxDistance = 4.4f;
        [SerializeField, Min(0)] private float dashOvershoot = .7f;
        [SerializeField, Min(0)] private float guardFrontThreshold = .16f;
        [SerializeField, Min(0)] private float guardHeight = .68f;
        [Header("Ranged")]
        [SerializeField, Min(.01f)] private float scanDuration = 1f;
        [SerializeField, Min(0)] private float lockDuration = .4f;
        [SerializeField, Min(.01f)] private float beamLength = 22f;
        [SerializeField, Min(.01f)] private float beamWidth = .16f;
        [SerializeField, Min(.01f)] private float beamDisplayDuration = .16f;
        [Header("Drone")]
        [SerializeField, Min(0)] private float cruiseRadius = 1.4f;
        [SerializeField, Min(0)] private float cruiseAmplitude = .32f;
        [SerializeField, Min(.01f)] private float horizontalSpeed = 1.6f;
        [SerializeField, Min(.01f)] private float verticalSpeed = 2f;
        [SerializeField, Min(.01f)] private float verticalAttackDistance = 2f;
        [SerializeField, Min(0)] private int verticalDamage = 10;
        [SerializeField, Min(.01f)] private float verticalBeamWidth = .28f;
        public EnemyPattern Pattern => pattern;
        public int Damage => damage;
        public int VerticalDamage => verticalDamage;
        public float BeamWidth => beamWidth;
        public float VerticalBeamWidth => verticalBeamWidth;
        public float BeamLength => beamLength;
        public float BeamDisplayDuration => beamDisplayDuration;
        public float DeathDuration => deathDuration;
        public float GuardHeight => guardHeight;
        public float GuardFrontThreshold => guardFrontThreshold;
        public bool TryValidate(out string error)
        {
            float[] positive = { detectionRange, firstPreparation, preparation, detectionHeight, approachHeight, approachDistance,
                approachSpeed, dashSpeed, dashMinDistance, dashMaxDistance, scanDuration, beamLength, beamWidth, beamDisplayDuration,
                horizontalSpeed, verticalSpeed, verticalAttackDistance, verticalBeamWidth };
            float[] nonnegative = { recovery, deathDuration, turnDuration, dashOvershoot, guardFrontThreshold, guardHeight, lockDuration, cruiseRadius, cruiseAmplitude };
            foreach (float value in positive) if (!(value > 0f) || float.IsInfinity(value)) { error = "Enemy speeds, ranges and preparation durations must be positive and finite."; return false; }
            foreach (float value in nonnegative) if (!(value >= 0f) || float.IsInfinity(value)) { error = "Enemy durations and amplitudes must be nonnegative and finite."; return false; }
            if (!System.Enum.IsDefined(typeof(EnemyPattern), pattern) || damage < 0 || verticalDamage < 0 || dashMinDistance > dashMaxDistance ||
                approachHeight > detectionHeight || (pattern == EnemyPattern.Drone && (lockDuration > preparation || lockDuration > firstPreparation)))
            { error = "Invalid enemy pattern, damage, dash limits or lock duration."; return false; }
            error = null; return true;
        }
        protected override IFsmState CreateInitialState(Unit owner)
        {
            if (!TryValidate(out string error)) throw new System.InvalidOperationException(error);
            if (!owner.TryGetComponent<EnemyCombat2D>(out var combat) || !owner.TryGetComponent<UnitHealth>(out var health))
                throw new System.InvalidOperationException("Enemy pattern requires EnemyCombat2D and UnitHealth.");
            var ground = owner.GetComponent<GroundMovement2D>(); var flying = owner.GetComponent<FlyingMovement2D>();
            if ((pattern == EnemyPattern.Shield && ground == null) || (pattern == EnemyPattern.Drone && flying == null))
                throw new System.InvalidOperationException("Enemy pattern is missing its movement feature.");
            return new Context(owner, combat, health, ground, flying, this).Idle;
        }
        // The definition is immutable at runtime; each unit gets its own graph and action state.
        private sealed class Context : IEnemyActionState
        {
            public readonly Unit Owner; public readonly EnemyCombat2D Combat; public readonly UnitHealth Health;
            public readonly GroundMovement2D Ground; public readonly FlyingMovement2D Flying; public readonly EnemyFsmDefinition S;
            public readonly State Idle, Turn, Prepare, Locked, Dash, Recover, Death;
            public EnemyPhase Phase { get; set; }
            public float Elapsed { get; set; }
            public float Duration, Distance, TurnTo;
            public float Facing { get; set; }
            public Vector2 Aim { get; set; }
            public bool Vertical { get; set; }
            public float Progress => Duration <= 0 ? 1f : Mathf.Clamp01(Elapsed / Duration);
            public bool First = true;
            public uint ObservedReposition;
            public Vector2 PreviousCenter;
            public Context(Unit owner, EnemyCombat2D combat, UnitHealth health, GroundMovement2D ground, FlyingMovement2D flying, EnemyFsmDefinition settings)
            {
                Owner = owner; Combat = combat; Health = health; Ground = ground; Flying = flying; S = settings; Facing = combat.InitialFacing;
                Idle = new State(this, EnemyPhase.Idle); Turn = new State(this, EnemyPhase.Turning); Prepare = new State(this, EnemyPhase.Preparing);
                Locked = new State(this, EnemyPhase.Locked); Dash = new State(this, EnemyPhase.Dash); Recover = new State(this, EnemyPhase.Recovery); Death = new State(this, EnemyPhase.Death);
                combat.Bind(settings, this); flying?.Configure(S.cruiseRadius, S.cruiseAmplitude, S.horizontalSpeed, S.verticalSpeed);
            }
            public void Stop() { Ground?.Stop(); Flying?.Cruise(false); }
            public void Cancel() { Stop(); Ground?.CancelForcedMovement(); Flying?.CancelForcedMovement(); Combat.ClearAttack(); }
            public void Track()
            {
                if (Combat.Target == null || !Combat.Target.CanReceiveDamage) return;
                Vector2 delta = Combat.TargetCenter - Combat.Center;
                Aim = delta.sqrMagnitude > .00001f ? delta.normalized : Vector2.right * Facing;
                if (S.pattern != EnemyPattern.Shield && Mathf.Abs(delta.x) > .01f) Facing = Mathf.Sign(delta.x);
            }
        }
        private sealed class State : IFsmState
        {
            private readonly Context c; private readonly EnemyPhase phase;
            private EnemyFsmDefinition S => c.S;
            public State(Context context, EnemyPhase value) { c = context; phase = value; }
            public void Enter()
            {
                c.Phase = phase; c.Elapsed = 0f; c.Duration = 0f; c.Stop();
                switch (phase)
                {
                    case EnemyPhase.Idle: c.Flying?.Cruise(true); break;
                    case EnemyPhase.Turning: c.TurnTo = -c.Facing; c.Duration = S.turnDuration; break;
                    case EnemyPhase.Preparing:
                        c.Track(); c.ObservedReposition = c.Combat.TargetReposition;
                        c.Duration = S.pattern == EnemyPattern.Surveillance ? S.scanDuration : (c.First ? S.firstPreparation : S.preparation);
                        if (S.pattern == EnemyPattern.Drone) c.Duration = Mathf.Max(0f, c.Duration - S.lockDuration);
                        c.Vertical = S.pattern == EnemyPattern.Drone && Mathf.Abs(c.Combat.TargetCenter.x - c.Combat.Center.x) < S.verticalAttackDistance;
                        c.Distance = Mathf.Clamp(Mathf.Abs(c.Combat.TargetCenter.x - c.Combat.Center.x) + S.dashOvershoot, S.dashMinDistance, S.dashMaxDistance);
                        break;
                    case EnemyPhase.Locked: c.Duration = S.lockDuration; c.First = false; break;
                    case EnemyPhase.Dash:
                        c.First = false; c.Duration = c.Distance / S.dashSpeed; c.PreviousCenter = c.Combat.Center; c.Combat.BeginDash();
                        if (c.Ground.IsGrounded && !c.Ground.IsForcedMoving) c.Ground.MoveTo(c.Ground.Position.x + c.Facing * c.Distance, S.dashSpeed);
                        break;
                    case EnemyPhase.Recovery: c.Duration = S.recovery; break;
                    case EnemyPhase.Death: c.Cancel(); c.Duration = S.deathDuration; break;
                }
            }
            public IFsmState Tick(float deltaTime)
            {
                if (!c.Health.IsAlive && phase != EnemyPhase.Death) return c.Death;
                c.Elapsed += deltaTime;
                switch (phase)
                {
                    case EnemyPhase.Idle:
                        if (!c.Combat.RefreshTarget(S.detectionRange, S.pattern == EnemyPattern.Shield ? S.detectionHeight : float.PositiveInfinity)) { c.Ground?.Stop(); return null; }
                        if (S.pattern != EnemyPattern.Shield) return c.Prepare;
                        if (c.Ground.IsForcedMoving) return null;
                        Vector2 delta = c.Combat.TargetCenter - c.Combat.Center;
                        if (delta.x * c.Facing < -.05f) return c.Turn;
                        if (Mathf.Abs(delta.y) > S.approachHeight) { c.Ground.Stop(); return null; }
                        if (Mathf.Abs(delta.x) > S.approachDistance)
                        { c.Ground.MoveTo(c.Ground.Position.x + delta.x - c.Facing * S.approachDistance, S.approachSpeed); return null; }
                        return c.Prepare;
                    case EnemyPhase.Turning:
                        if (c.Elapsed < c.Duration) return null;
                        c.Facing = c.TurnTo; return c.Idle;
                    case EnemyPhase.Preparing:
                        if (S.pattern == EnemyPattern.Surveillance)
                        {
                            if (!c.Combat.ValidTarget(c.Combat.Target, S.detectionRange)) return c.Idle;
                            uint version = c.Combat.TargetReposition;
                            if (version != c.ObservedReposition) { c.ObservedReposition = version; c.Elapsed = 0f; }
                            c.Track();
                        }
                        else if (S.pattern == EnemyPattern.Drone) c.Track();
                        if (c.Elapsed < c.Duration) return null;
                        if (S.pattern != EnemyPattern.Shield) return c.Locked;
                        return c.Ground.IsGrounded && !c.Ground.IsForcedMoving ? c.Dash : c.Recover;
                    case EnemyPhase.Locked:
                        if (c.Elapsed < c.Duration) return null;
                        c.Combat.Fire(c.Aim, c.Vertical); return c.Recover;
                    case EnemyPhase.Dash:
                        // Reacting bodies cannot deal contact damage or restart their attack movement.
                        if (!c.Ground.IsForcedMoving) c.Combat.HitDashPath(c.PreviousCenter);
                        c.PreviousCenter = c.Combat.Center;
                        if (c.Ground.IsForcedMoving || !c.Ground.IsGrounded || c.Ground.IsBlocked || c.Ground.HasArrived || c.Elapsed >= c.Duration) return c.Recover;
                        return null;
                    case EnemyPhase.Recovery: return c.Elapsed >= c.Duration ? c.Idle : null;
                    case EnemyPhase.Death:
                        if (c.Elapsed >= c.Duration) c.Owner.gameObject.SetActive(false);
                        return null;
                }
                return null;
            }
            public void Exit()
            {
                c.Stop();
                if (!c.Owner.isActiveAndEnabled)
                { c.Cancel(); c.First = true; c.Facing = c.Combat.InitialFacing; c.Elapsed = 0; c.Phase = EnemyPhase.Idle; }
            }
        }
    }
}
