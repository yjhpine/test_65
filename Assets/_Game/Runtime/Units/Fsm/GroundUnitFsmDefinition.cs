using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Units.Fsm
{
    [CreateAssetMenu(menuName = "Action Platformer/Units/FSM/Ground Unit")]
    public sealed class GroundUnitFsmDefinition : FsmDefinition
    {
        [Header("Patrol")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 2f;
        [SerializeField, Min(0.1f)] private float patrolDistance = 3f;
        [SerializeField, Min(0.05f)] private float waitDuration = 0.6f;

        [Header("Chase entry and exit")]
        [SerializeField] private UnitKind targetKind = UnitKind.Player;
        [SerializeField, Min(0.1f)] private float detectionRange = 6f;
        [SerializeField, Min(0.1f)] private float loseTargetRange = 8f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 3f;
        [SerializeField, Min(0.02f)] private float targetRefreshInterval = 0.15f;

        [Header("Attack")]
        [SerializeField, Min(0.1f)] private float attackRange = 1.2f;
        [SerializeField, Min(0f)] private float attackWindup = 0.2f;
        [Tooltip("Minimum time between attack starts, including time spent in other states.")]
        [SerializeField, Min(0.05f)] private float attackInterval = 1f;

        [Header("Hit and death")]
        [SerializeField, Min(0f)] private float hitStunDuration = 0.25f;
        [Tooltip("Time spent in Death before deactivating the GameObject. Zero deactivates immediately.")]
        [SerializeField, Min(0f)] private float deathDelay = 0.8f;

        public float MoveSpeed => moveSpeed;
        public float PatrolDistance => patrolDistance;
        public float WaitDuration => waitDuration;

        public bool TryValidate(out string error)
        {
            if (!Positive(moveSpeed) || !Positive(patrolDistance) || !Positive(waitDuration) ||
                !Positive(detectionRange) || !Positive(loseTargetRange) || !Positive(chaseSpeed) ||
                !Positive(targetRefreshInterval) || !Positive(attackRange) || !Positive(attackInterval) ||
                !NonNegative(attackWindup) || !NonNegative(hitStunDuration) || !NonNegative(deathDelay))
            {
                error = "FSM distances, speeds and intervals must be positive and finite; windup, hit stun and death delay cannot be negative.";
                return false;
            }
            if (attackRange > detectionRange || detectionRange > loseTargetRange || attackWindup > attackInterval)
            {
                error = "Use Attack Range <= Detection Range <= Lose Target Range and Attack Windup <= Attack Interval.";
                return false;
            }
            if (targetKind == UnitKind.Unspecified || !System.Enum.IsDefined(typeof(UnitKind), targetKind))
            {
                error = "Select a supported target kind.";
                return false;
            }
            error = null;
            return true;
        }

        protected override IFsmState CreateInitialState(Unit owner)
        {
            if (!TryValidate(out string error)) throw new System.InvalidOperationException(error);
            if (!owner.TryGetComponent<GroundMovement2D>(out var movement))
                throw new System.InvalidOperationException("Ground unit FSM requires GroundMovement2D on its unit.");
            owner.TryGetComponent<UnitHealth>(out var health);
            owner.TryGetComponent<UnitCombat2D>(out var combat);
            var context = new Context(owner, movement, health, combat, this);
            return context.WaitRight;
        }

        private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
        private static bool NonNegative(float value) => value >= 0f && !float.IsInfinity(value);

        // Per-unit graph and timers. The shared definition only supplies configuration.
        private sealed class Context
        {
            public readonly Unit Owner;
            public readonly GroundMovement2D Movement;
            public readonly UnitHealth Health;
            public readonly UnitCombat2D Combat;
            public readonly GroundUnitFsmDefinition Settings;
            public readonly WaitState WaitRight;
            public readonly ChaseState Chase;
            public readonly AttackState Attack;
            public readonly HitState Hit;
            public readonly DeathState Death;
            public float Clock;
            public float NextAttackAt;
            public uint ObservedDamage;
            private float nextScanAt;

            public Context(Unit owner, GroundMovement2D movement, UnitHealth health, UnitCombat2D combat, GroundUnitFsmDefinition settings)
            {
                Owner = owner; Movement = movement; Health = health; Combat = combat; Settings = settings;
                WaitRight = new WaitState(this);
                var walkRight = new WalkState(this, 1f);
                var waitLeft = new WaitState(this);
                var walkLeft = new WalkState(this, -1f);
                WaitRight.Next = walkRight; walkRight.Next = waitLeft;
                waitLeft.Next = walkLeft; walkLeft.Next = WaitRight;
                Chase = new ChaseState(this); Attack = new AttackState(this);
                Hit = new HitState(this); Death = new DeathState(this);
            }

            public void RefreshTarget()
            {
                if (Combat == null || Clock < nextScanAt) return;
                nextScanAt = Clock + Settings.targetRefreshInterval;
                Combat.RefreshTarget(Settings.targetKind, Settings.detectionRange, Settings.loseTargetRange);
            }

            public IFsmState CombatState()
            {
                if (Movement.IsForcedMoving) return null;
                if (Combat == null || !Combat.isActiveAndEnabled || Combat.Target == null || !Combat.Target.IsAlive ||
                    !Combat.Target.CanReceiveDamage) return null;
                return Combat.IsTargetInRange(Settings.attackRange) ? (IFsmState)Attack : Chase;
            }
        }

        private abstract class State : IFsmState, IUnitAnimationState
        {
            protected readonly Context context;
            public virtual UnitAnimation Animation => UnitAnimation.Locomotion;
            public virtual uint AnimationVersion => 0;
            public virtual float AnimationElapsed => 0f;
            public virtual float AnimationDuration => 0f;
            public virtual float FacingDirection => 0f;
            protected State(Context context) { this.context = context; }
            public virtual void Enter() { }
            public virtual void Exit() { context.Movement.Stop(); }

            public IFsmState Tick(float deltaTime)
            {
                context.Clock += deltaTime;
                if (context.Health != null)
                {
                    if (!context.Health.IsAlive)
                        return this == context.Death ? Step(deltaTime) : context.Death;
                    if (context.Health.DamageVersion != context.ObservedDamage)
                    {
                        context.ObservedDamage = context.Health.DamageVersion;
                        if (this != context.Hit) return context.Hit;
                        context.Hit.RestartStun();
                    }
                }
                if (context.Movement.IsForcedMoving && this != context.Hit && this != context.Death)
                    return this == context.WaitRight ? null : context.WaitRight;
                context.RefreshTarget();
                return Step(deltaTime);
            }
            protected abstract IFsmState Step(float deltaTime);
        }

        private sealed class WaitState : State
        {
            private float elapsed;
            public IFsmState Next;
            public WaitState(Context context) : base(context) { }
            public override void Enter() { elapsed = 0f; context.Movement.Stop(); }
            protected override IFsmState Step(float deltaTime)
            {
                IFsmState combat = context.CombatState();
                if (combat != null) return combat;
                if (!context.Movement.isActiveAndEnabled) return null;
                elapsed += deltaTime;
                return elapsed >= context.Settings.waitDuration ? Next : null;
            }
        }

        private sealed class WalkState : State
        {
            private readonly float direction;
            public IFsmState Next;
            public WalkState(Context context, float direction) : base(context) { this.direction = direction; }
            public override void Enter() => context.Movement.MoveTo(
                context.Movement.Position.x + direction * context.Settings.patrolDistance, context.Settings.moveSpeed);
            protected override IFsmState Step(float deltaTime) => context.CombatState() ??
                (!context.Movement.isActiveAndEnabled || context.Movement.HasArrived || context.Movement.IsBlocked ? Next : null);
        }

        private sealed class ChaseState : State
        {
            public ChaseState(Context context) : base(context) { }
            protected override IFsmState Step(float deltaTime)
            {
                IFsmState next = context.CombatState();
                if (next == null) return context.WaitRight;
                if (next != this) return next;
                context.Movement.MoveTo(context.Combat.Target.transform.position.x, context.Settings.chaseSpeed);
                return null;
            }
        }

        private sealed class AttackState : State
        {
            private bool windingUp;
            private float elapsed;
            private UnitHealth victim;
            private bool hasAttacked;
            private float animationStartedAt;
            private float facing;
            private uint animationVersion;
            public override UnitAnimation Animation => hasAttacked ? UnitAnimation.Attack : UnitAnimation.Locomotion;
            public override uint AnimationVersion => animationVersion;
            public override float AnimationElapsed => context.Clock - animationStartedAt;
            public override float AnimationDuration => context.Settings.attackWindup;
            public override float FacingDirection => facing;
            public AttackState(Context context) : base(context) { }
            public override void Enter() { context.Movement.Stop(); windingUp = false; hasAttacked = false; elapsed = 0f; }
            public override void Exit() { windingUp = false; victim = null; base.Exit(); }
            protected override IFsmState Step(float deltaTime)
            {
                if (windingUp)
                {
                    elapsed += deltaTime;
                    if (elapsed >= context.Settings.attackWindup)
                    {
                        context.Combat.TryHit(victim, context.Settings.attackRange);
                        windingUp = false;
                        victim = null;
                    }
                    return null;
                }
                IFsmState next = context.CombatState();
                if (next == null) return context.WaitRight;
                if (next != this) return next;
                if (context.Clock < context.NextAttackAt) return null;
                context.NextAttackAt = context.Clock + context.Settings.attackInterval;
                victim = context.Combat.Target;
                facing = victim.transform.position.x - context.Owner.transform.position.x;
                hasAttacked = true;
                animationStartedAt = context.Clock;
                unchecked { animationVersion++; }
                elapsed = 0f;
                windingUp = true;
                if (context.Settings.attackWindup == 0f)
                {
                    context.Combat.TryHit(victim, context.Settings.attackRange);
                    windingUp = false;
                    victim = null;
                }
                return null;
            }
        }

        private sealed class HitState : State
        {
            private float elapsed;
            private uint animationVersion;
            public override UnitAnimation Animation => UnitAnimation.Hit;
            public override uint AnimationVersion => animationVersion;
            public override float AnimationElapsed => elapsed;
            public override float AnimationDuration => context.Settings.hitStunDuration;
            public HitState(Context context) : base(context) { }
            public override void Enter() { RestartStun(); context.Movement.Stop(); }
            public void RestartStun() { elapsed = 0f; unchecked { animationVersion++; } }
            protected override IFsmState Step(float deltaTime)
            {
                elapsed += deltaTime;
                return elapsed >= context.Settings.hitStunDuration ? context.CombatState() ?? context.WaitRight : null;
            }
        }

        private sealed class DeathState : State
        {
            private float elapsed;
            public override UnitAnimation Animation => UnitAnimation.Death;
            public override float AnimationElapsed => elapsed;
            public override float AnimationDuration => context.Settings.deathDelay;
            public DeathState(Context context) : base(context) { }
            public override void Enter()
            {
                elapsed = 0f;
                context.Movement.CancelForcedMovement();
                context.Movement.Stop();
                if (context.Combat != null) context.Combat.ClearTarget();
                if (context.Settings.deathDelay == 0f) context.Owner.gameObject.SetActive(false);
            }
            protected override IFsmState Step(float deltaTime)
            {
                elapsed += deltaTime;
                if (elapsed >= context.Settings.deathDelay) context.Owner.gameObject.SetActive(false);
                return null;
            }
        }
    }
}
