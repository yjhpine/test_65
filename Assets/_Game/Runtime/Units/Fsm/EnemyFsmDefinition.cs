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
        [Header("Shield (never walks; turns only when a telegraph starts)")]
        [SerializeField] private ShieldAttack shieldAttack;
        [Tooltip("Wait after the player is recognized before a telegraph may start.")]
        [SerializeField, Min(0)] private float recognitionDelay = .2f;
        [Tooltip("Shortest time from one telegraph's start to the next.")]
        [SerializeField, Min(0)] private float attackInterval = 5f;
        [SerializeField, Min(.01f)] private float detectionHeight = 2.8f;
        [Tooltip("Largest height difference at which a telegraph may start.")]
        [SerializeField, Min(.01f)] private float approachHeight = 1.4f;
        [Tooltip("Rush: speed and length of the straight path fixed at the telegraph; terrain and ledges shorten it.")]
        [SerializeField, Min(.01f)] private float dashSpeed = 18.9f;
        [SerializeField, Min(.01f)] private float dashMaxDistance = 5.5f;
        [Tooltip("Strike: the player must be this close horizontally (center to center) for the telegraph to start.")]
        [SerializeField, Min(.01f)] private float strikeTriggerDistance = 2.9f;
        [Tooltip("Strike: reach of the single hit, forward from the body's front edge.")]
        [SerializeField, Min(.01f)] private float strikeReach = 2.3f;
        [SerializeField, Min(.01f)] private float strikeDuration = .12f;
        [Tooltip("Half-width of the column over the body counted as directly above/below; attacks from there or behind pass.")]
        [SerializeField, Min(0)] private float guardDirectBand = .5f;
        [Header("Ranged")]
        [SerializeField, Min(.1f)] private float idleTurnInterval = 3f;
        [SerializeField, Min(.01f)] private float idleTurnDuration = .5f;
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
        public ShieldAttack ShieldAttack => shieldAttack;
        public float GuardDirectBand => guardDirectBand;
        public float StrikeReach => strikeReach;
        public float DashMaxDistance => dashMaxDistance;
        public bool TryValidate(out string error)
        {
            float[] positive = { detectionRange, firstPreparation, preparation, detectionHeight, approachHeight,
                dashSpeed, dashMaxDistance, strikeTriggerDistance, strikeReach, strikeDuration, scanDuration, beamLength, beamWidth, beamDisplayDuration,
                horizontalSpeed, verticalSpeed, verticalAttackDistance, verticalBeamWidth, idleTurnInterval, idleTurnDuration };
            float[] nonnegative = { recovery, deathDuration, recognitionDelay, attackInterval, guardDirectBand, lockDuration, cruiseRadius, cruiseAmplitude };
            foreach (float value in positive) if (!(value > 0f) || float.IsInfinity(value)) { error = "Enemy speeds, ranges and preparation durations must be positive and finite."; return false; }
            foreach (float value in nonnegative) if (!(value >= 0f) || float.IsInfinity(value)) { error = "Enemy durations and amplitudes must be nonnegative and finite."; return false; }
            if (!System.Enum.IsDefined(typeof(EnemyPattern), pattern) || !System.Enum.IsDefined(typeof(ShieldAttack), shieldAttack) ||
                damage < 0 || verticalDamage < 0 || approachHeight > detectionHeight || strikeTriggerDistance > detectionRange ||
                (pattern == EnemyPattern.Drone && (lockDuration > preparation || lockDuration > firstPreparation)))
            { error = "Invalid enemy pattern, damage, shield ranges or lock duration."; return false; }
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
            public readonly State Idle, Turn, Prepare, Locked, Dash, Recover, Death, Strike, React;
            public EnemyPhase Phase { get; set; }
            public float Elapsed { get; set; }
            public float Duration, Distance, TurnTo;
            // Shield: how long the player has been recognized in Idle, and the time since the last telegraph began.
            public float Recognized, SinceTelegraph = float.PositiveInfinity;
            public float Facing { get; set; }
            public Vector2 Aim { get; set; }
            public bool Vertical { get; set; }
            public float Progress => Duration <= 0 ? 1f : Mathf.Clamp01(Elapsed / Duration);
            public bool First = true;
            public uint ObservedReposition, ObservedInterrupt;
            public Vector2 PreviousCenter;
            public Context(Unit owner, EnemyCombat2D combat, UnitHealth health, GroundMovement2D ground, FlyingMovement2D flying, EnemyFsmDefinition settings)
            {
                Owner = owner; Combat = combat; Health = health; Ground = ground; Flying = flying; S = settings; Facing = combat.InitialFacing;
                ObservedInterrupt = health.InterruptVersion;
                Idle = new State(this, EnemyPhase.Idle); Turn = new State(this, EnemyPhase.Turning); Prepare = new State(this, EnemyPhase.Preparing);
                Locked = new State(this, EnemyPhase.Locked); Dash = new State(this, EnemyPhase.Dash); Recover = new State(this, EnemyPhase.Recovery); Death = new State(this, EnemyPhase.Death);
                Strike = new State(this, EnemyPhase.Strike); React = new State(this, EnemyPhase.Reacting);
                combat.Bind(settings, this); flying?.Configure(S.cruiseRadius, S.cruiseAmplitude, S.horizontalSpeed, S.verticalSpeed);
            }
            public void Stop() { Ground?.Stop(); Flying?.Cruise(false); }
            public void Cancel() { Stop(); Ground?.CancelForcedMovement(); Flying?.CancelForcedMovement(); Combat.ClearAttack(); }
            public void Track()
            {
                if (Combat.Target == null || !Combat.Target.CanReceiveDamage) return;
                Vector2 delta = Combat.TargetCenter - Combat.AimOrigin;
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
                    case EnemyPhase.Turning:
                        c.TurnTo = -c.Facing;
                        c.Duration = S.idleTurnDuration;
                        break;
                    case EnemyPhase.Preparing:
                        c.Track(); c.ObservedReposition = c.Combat.TargetReposition;
                        c.Duration = S.pattern == EnemyPattern.Surveillance ? S.scanDuration : (c.First ? S.firstPreparation : S.preparation);
                        if (S.pattern == EnemyPattern.Drone) c.Duration = Mathf.Max(0f, c.Duration - S.lockDuration);
                        c.Vertical = S.pattern == EnemyPattern.Drone && Mathf.Abs(c.Combat.TargetCenter.x - c.Combat.Center.x) < S.verticalAttackDistance;
                        if (S.pattern == EnemyPattern.Shield)
                        {
                            // The only moment a shield unit turns; direction and rush path then stay fixed to recovery.
                            float dx = c.Combat.TargetCenter.x - c.Combat.Center.x;
                            if (Mathf.Abs(dx) > .01f) c.Facing = Mathf.Sign(dx);
                            c.SinceTelegraph = 0f;
                            c.Distance = S.dashMaxDistance;
                        }
                        break;
                    case EnemyPhase.Locked: c.Duration = S.lockDuration; c.First = false; break;
                    case EnemyPhase.Dash:
                        c.First = false; c.Duration = c.Distance / S.dashSpeed; c.PreviousCenter = c.Combat.Center; c.Combat.BeginDash();
                        if (c.Ground.IsGrounded && !c.Ground.IsForcedMoving) c.Ground.MoveTo(c.Ground.Position.x + c.Facing * c.Distance, S.dashSpeed);
                        break;
                    case EnemyPhase.Strike: c.First = false; c.Duration = S.strikeDuration; c.Combat.Strike(c.Facing); break;
                    case EnemyPhase.Recovery: c.Duration = S.recovery; break;
                    case EnemyPhase.Reacting: c.Combat.ClearAttack(); break;
                    case EnemyPhase.Death: c.Cancel(); c.Duration = S.deathDuration; break;
                }
            }
            public IFsmState Tick(float deltaTime)
            {
                if (!c.Health.IsAlive && phase != EnemyPhase.Death) return c.Death;
                c.SinceTelegraph += deltaTime;
                // Ordinary hits never reset preparation; launches and slams stop an attack in progress.
                if (c.Health.InterruptVersion != c.ObservedInterrupt)
                {
                    c.ObservedInterrupt = c.Health.InterruptVersion;
                    // A shield unit is knocked out of any action, shield down, until it lands; the attack is not resumed.
                    if (S.pattern == EnemyPattern.Shield) { if (phase != EnemyPhase.Reacting) return c.React; }
                    else if (phase == EnemyPhase.Preparing || phase == EnemyPhase.Locked || phase == EnemyPhase.Dash)
                    { c.Combat.ClearAttack(); return c.Recover; }
                }
                c.Elapsed += deltaTime;
                switch (phase)
                {
                    case EnemyPhase.Idle:
                        if (S.pattern == EnemyPattern.Shield) return ShieldIdle(deltaTime);
                        if (!c.Combat.RefreshTarget(S.detectionRange, float.PositiveInfinity, S.pattern == EnemyPattern.Surveillance ? c.Facing : 0f))
                        {
                            c.Ground?.Stop();
                            return S.pattern == EnemyPattern.Surveillance && c.Elapsed >= S.idleTurnInterval ? c.Turn : null;
                        }
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
                        // A shield telegraph is cancelled when recognition is lost; the attack interval still counts from it.
                        else if (!c.Combat.ValidTarget(c.Combat.Target, S.detectionRange, S.detectionHeight) || !c.Combat.TargetSeesUs())
                            return c.Idle;
                        if (c.Elapsed < c.Duration) return null;
                        if (S.pattern != EnemyPattern.Shield) return c.Locked;
                        if (S.shieldAttack == ShieldAttack.Strike) return c.Strike;
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
                    case EnemyPhase.Strike:
                        return c.Elapsed >= c.Duration ? c.Recover : null;
                    case EnemyPhase.Reacting:
                        // Back on the ground and done being moved: the shield comes up again.
                        return !c.Ground.IsForcedMoving && c.Ground.IsGrounded ? c.Idle : null;
                    case EnemyPhase.Recovery:
                        // The turret holds its shot pose through the beam, then tracks during its existing cooldown.
                        if (S.pattern == EnemyPattern.Surveillance && c.Elapsed >= S.beamDisplayDuration &&
                            c.Combat.ValidTarget(c.Combat.Target, S.detectionRange)) c.Track();
                        return c.Elapsed >= c.Duration ? c.Idle : null;
                    case EnemyPhase.Death:
                        if (c.Elapsed >= c.Duration) c.Owner.gameObject.SetActive(false);
                        return null;
                }
                return null;
            }
            // Shield units never walk after the player. A telegraph needs recognition (range, height, sight, on screen)
            // held for the recognition delay, the attack interval since the last telegraph, and a grounded stance;
            // Strike additionally needs the player within its trigger distance.
            private IFsmState ShieldIdle(float deltaTime)
            {
                c.Ground.Stop();
                if (!c.Combat.RefreshTarget(S.detectionRange, S.detectionHeight) || !c.Combat.TargetSeesUs()) { c.Recognized = 0f; return null; }
                c.Recognized += deltaTime;
                if (c.Recognized < S.recognitionDelay || c.SinceTelegraph < S.attackInterval || c.Ground.IsForcedMoving || !c.Ground.IsGrounded)
                    return null;
                Vector2 delta = c.Combat.TargetCenter - c.Combat.Center;
                if (Mathf.Abs(delta.y) > S.approachHeight) return null;
                if (S.shieldAttack == ShieldAttack.Strike && Mathf.Abs(delta.x) > S.strikeTriggerDistance) return null;
                return c.Prepare;
            }
            public void Exit()
            {
                c.Stop();
                if (!c.Owner.isActiveAndEnabled)
                {
                    c.Cancel(); c.First = true; c.Facing = c.Combat.InitialFacing; c.Elapsed = 0; c.Phase = EnemyPhase.Idle;
                    c.Recognized = 0f; c.SinceTelegraph = float.PositiveInfinity;
                }
            }
        }
    }
}
