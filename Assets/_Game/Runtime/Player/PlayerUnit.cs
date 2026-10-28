using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionPlatformer.Player
{
    [RequireComponent(typeof(PlayerInput), typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class PlayerUnit : Unit, IPlayerActionState, IUnitRepositionState
    {
        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private LayerMask groundMask = 1;
        [SerializeField] private PlayerVisual visual;
        [SerializeField] private GlitchTuning glitchTuning;
        [SerializeField] private PlayerCombatTuning combatTuning;
        [SerializeField] private Camera aimCamera;
        [Tooltip("Optional camera overlay material for player damage flashes and vignette.")]
        [SerializeField] private Material damageScreenMaterial;
        [SerializeField] private bool reducedMotion;
        public bool ReducedMotion => reducedMotion;
        private readonly System.Collections.Generic.Queue<UnitDamageResult> pendingDamageFeedback =
            new System.Collections.Generic.Queue<UnitDamageResult>();
        private double hitVisualStartedAt = double.NegativeInfinity;
        private UnitDamageResult visualDamage;
        [Tooltip("Time to display Die before deactivating the player. UnitHealth Deactivate On Death must be off.")]
        [SerializeField, Min(0f)] private float deathDuration = 0.7f;
        private double deathStartedAt = double.NegativeInfinity;
        private PlayerInputReader inputReader;
        private IPlayerInputSource input;
        private readonly InputBuffer jumpBuffer = new InputBuffer();
        private float horizontalInput;
        private bool jumpHeld;
        private double lastGroundedAt = double.NegativeInfinity;
        private bool jumpGraceAvailable;
        private bool pendingJumpRelease;
        private bool pendingGlitch, pendingAttack, pendingCancel;
        private GlitchRequest glitchRequest;
        private UnitHealth health;
        private uint observedDamage;
        private float facing = 1f;
        private bool hasAim;
        private Vector2 aimScreenPosition;
        private const float AimFacingDeadZone = 0.1f;
        private int controlLocks;
        private System.IDisposable transferLock;
        private Vector2 transferOrigin;
        public bool IsScriptedTransferring => transferLock != null;
        public bool IsControlLocked => controlLocks > 0;
        // Relinquish external camera control before applying a new damage presentation.
        public event System.Action ControlInterrupted;
        // Local observers may react to the same input; normal combat glitch selection stays here.
        public event System.Action GlitchPressed;
        public System.IDisposable AcquireControlLock() => AcquireControlLock(false);
        private System.IDisposable AcquireControlLock(bool preserveGlitchCooldown)
        {
            if (controlLocks++ == 0) { ClearActions(preserveGlitchCooldown); Motor?.StopHorizontal(); }
            return new ControlLock(this);
        }

        public bool BeginScriptedTransfer()
        {
            if (!isActiveAndEnabled || Motor == null || health == null || !health.IsAlive || IsControlLocked) return false;
            transferLock = AcquireControlLock(true);
            visual?.ResetActions();
            transferOrigin = Motor.Position;
            Motor.BeginTransfer();
            unchecked { RepositionVersion++; }
            return true;
        }

        public void SetScriptedTransferPosition(Vector2 position)
        {
            if (IsScriptedTransferring && float.IsFinite(position.x) && float.IsFinite(position.y))
                Motor.Relocate(position, Vector2.zero);
        }

        public void EndScriptedTransfer(bool completed = false)
        {
            if (transferLock == null) return;
            if (!completed) Motor?.Relocate(transferOrigin, Vector2.zero);
            Motor?.EndTransfer();
            transferLock.Dispose();
            transferLock = null;
        }

        public void PresentTransferSpark(Vector2 point) => visual?.PresentTransferSpark(point);
        private sealed class ControlLock : System.IDisposable
        {
            private PlayerUnit owner;
            public ControlLock(PlayerUnit owner) { this.owner = owner; }
            public void Dispose()
            {
                if (owner == null) return;
                owner.controlLocks--;
                owner.ClearJumpState();
                owner.pendingGlitch = owner.pendingAttack = owner.pendingCancel = false;
                owner = null;
            }
        }

        public uint RepositionVersion { get; private set; }
        public CharacterMotor2D Motor { get; private set; }
        public PlayerGlitch Glitch { get; private set; }
        public PlayerCombat Combat { get; private set; }
        public PlayerImpactFeedback Feedback { get; private set; }
        public GlitchPreview GlitchPreview { get; private set; }
        public bool IsUnderground => Glitch != null && Glitch.IsUnderground;
        public bool CanEmerge => Glitch != null && Glitch.CanEmerge;
        public Vector2 UndergroundPosition => Glitch == null ? Vector2.zero : Glitch.UndergroundPosition;
        public Vector2 GroundMarkerPosition => Glitch == null ? Vector2.zero : Glitch.GroundMarkerPosition;
        public float Facing => Combat != null && Combat.IsAttacking ? Combat.Facing : facing;
        public PlayerAttackPhase AttackPhase => Combat == null ? PlayerAttackPhase.Ready : Combat.Phase;
        public PlayerAttackPresentation AttackPresentation => Combat == null ? default : Combat.GetPresentation(Time.timeAsDouble);
        public PlayerGlitchPresentation GlitchPresentation => Glitch == null ? default : Glitch.Presentation;
        public Bounds AttackBounds => Combat == null ? default : Combat.AttackBounds;
        public PlayerShockwave Shockwave => Combat == null ? default : Combat.GetShockwave(Time.timeAsDouble);
        public void SetAimCamera(Camera camera)
        {
            aimCamera = camera;
            Feedback?.SetCamera(camera);
        }
        public PlayerReactionPresentation ReactionPresentation
        {
            get
            {
                if (!isActiveAndEnabled || health == null) return default;
                bool dying = !health.IsAlive;
                float duration = dying ? Mathf.Max(0f, deathDuration) : .12f;
                double started = dying ? deathStartedAt : hitVisualStartedAt;
                double elapsed = (dying ? Time.timeAsDouble : Time.unscaledTimeAsDouble) - started;
                if (!dying && (duration <= 0f || elapsed >= duration)) return default;
                return new PlayerReactionPresentation { Kind = dying ? PlayerReaction.Die : PlayerReaction.Hit,
                    Version = health.DamageVersion, HasDirection = visualDamage.HasDirection, Direction = visualDamage.Direction,
                    Progress = double.IsNegativeInfinity(started) ? 0f :
                        duration > 0f ? Mathf.Clamp01((float)(elapsed / duration)) : 1f };
            }
        }

        protected override bool TryInitializeUnit()
        {
            if (tuning == null) { Debug.LogError("Player tuning is missing.", this); return false; }
            Motor = new CharacterMotor2D(GetComponent<Rigidbody2D>(), GetComponent<CapsuleCollider2D>(), tuning, groundMask);
            inputReader = new PlayerInputReader(GetComponent<PlayerInput>());
            input = inputReader;
            TryGetComponent(out health);
            // Movement, combat and repositioning can be configured independently.
            if (glitchTuning != null || combatTuning != null)
            {
                if (health == null || (glitchTuning != null && !glitchTuning.TryValidate()) ||
                    (combatTuning != null && !combatTuning.TryValidate()))
                { Debug.LogError("Configured player actions need valid tuning and UnitHealth.", this); return false; }
                var body = GetComponent<CapsuleCollider2D>();
                if (glitchTuning != null) Glitch = new PlayerGlitch(this, health, Motor, body, glitchTuning);
                if (combatTuning != null)
                    Combat = new PlayerCombat(this, body, combatTuning,
                        combatTuning.EnableCombo ? new SequentialComboRule(combatTuning.ComboLength, combatTuning.RetainComboOnTargetChange) : null,
                        combatTuning.EnablePositionAttacks ? new PositionAttackSelector(combatTuning) : null,
                        combatTuning.EnableHitReactions ? new GroundHitReaction(combatTuning) : null);
            }
            if (combatTuning != null) Feedback = new PlayerImpactFeedback(combatTuning, aimCamera, damageScreenMaterial);
            if (health != null) health.DamageApplied += OnDamageApplied;
            SetReducedMotion(reducedMotion);
            if (visual != null) visual.Initialize(Motor, this);
            return true;
        }

        public void SetInputSource(IPlayerInputSource source) => input = source ?? inputReader;

        public void SetReducedMotion(bool value)
        {
            reducedMotion = value;
            if (Feedback != null) Feedback.ReducedMotion = value;
            if (visual != null) visual.SetReducedMotion(value);
        }

        private void OnDamageApplied(UnitDamageResult result)
        {
            if (isActiveAndEnabled && result.HealthLost > 0) pendingDamageFeedback.Enqueue(result);
        }

        private void PresentDamage()
        {
            while (pendingDamageFeedback.Count > 0)
            {
                visualDamage = pendingDamageFeedback.Dequeue();
                hitVisualStartedAt = Time.unscaledTimeAsDouble;
                Feedback?.PlayDamage(visualDamage.Direction, Time.unscaledTimeAsDouble);
                visual?.NotifyDamage();
            }
        }

        protected override void OnUnitUpdate()
        {
            if (Feedback != null && Feedback.ReducedMotion != reducedMotion) SetReducedMotion(reducedMotion);
            Feedback?.Tick(Time.unscaledTimeAsDouble);
            if (!UpdateReactions(Time.timeAsDouble)) return;
            if (IsScriptedTransferring) return;
            PlayerCommand command = IsControlLocked ? default : input.Sample();
            if (command.GlitchPressed) GlitchPressed?.Invoke();
            GlitchPreview = default;
            SetMovementInput(command.Move.x, command.JumpHeld);
            hasAim = command.HasAim;
            aimScreenPosition = command.AimScreenPosition;
            // Locomotion owns idle facing; the cursor is consulted only when an attack can start.
            if ((Combat == null || !Combat.IsAttacking) && Mathf.Abs(command.Move.x) > 0.01f)
                facing = Mathf.Sign(command.Move.x);
            if (IsUnderground)
            {
                if (command.JumpPressed) pendingCancel = true;
            }
            else
            {
                if (Combat == null || !Combat.BlocksMovement(Motor.IsGrounded))
                {
                    if (command.JumpPressed) RequestJump();
                }
                // Releasing an existing jump still cuts its height during an air swing.
                if (command.JumpReleased && (Combat == null || !Combat.BlocksMovement(Motor.IsGrounded)))
                    RequestJumpRelease();
                if ((command.HasAim || command.GlitchPressed) && Glitch != null && aimCamera != null)
                {
                    Ray ray = Feedback != null ? Feedback.ScreenPointToRay(command.AimScreenPosition) :
                        aimCamera.ScreenPointToRay(command.AimScreenPosition);
                    var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));
                    if (plane.Raycast(ray, out float distance))
                    {
                        var selection = pendingGlitch ? glitchRequest : Glitch.Select(ray.GetPoint(distance));
                        var preview = Glitch.Preview(selection, Time.timeAsDouble);
                        preview.CanExecute &= CanReposition(Time.timeAsDouble) && health.DamageVersion == observedDamage;
                        GlitchPreview = preview;
                        if (command.GlitchPressed && !pendingGlitch && CanReposition(Time.timeAsDouble))
                        {
                            glitchRequest = selection;
                            pendingGlitch = true;
                        }
                    }
                }
            }
            // Glitch may cancel Recovery in this physics step. Validate attack eligibility after it executes.
            if (command.AttackPressed && !pendingAttack && (Combat != null || IsUnderground))
            {
                pendingAttack = true;
                if (!IsUnderground) Combat?.BufferAttack(Time.timeAsDouble);
            }
        }

        private bool CanReposition(double now) => Combat == null || Combat.CanReposition(now);

        private bool RefreshAimFacing()
        {
            if (!hasAim || aimCamera == null || !float.IsFinite(aimScreenPosition.x) || !float.IsFinite(aimScreenPosition.y))
                return false;
            Ray ray = Feedback != null ? Feedback.ScreenPointToRay(aimScreenPosition) : aimCamera.ScreenPointToRay(aimScreenPosition);
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));
            if (!plane.Raycast(ray, out float distance)) return false;
            float delta = ray.GetPoint(distance).x - Motor.Position.x;
            if (Mathf.Abs(delta) > AimFacingDeadZone) facing = Mathf.Sign(delta);
            return true;
        }

        private void SetMovementInput(float horizontal, bool holdingJump)
        {
            horizontalInput = horizontal;
            jumpHeld = holdingJump;
        }

        private void RequestJump() => jumpBuffer.Push(Time.timeAsDouble);
        private void RequestJumpRelease() => pendingJumpRelease = true;

        private void FixedUpdate()
        {
            if (Motor == null || tuning == null) return;
            double now = Time.timeAsDouble;
            if (!UpdateReactions(now))
            {
                if (isActiveAndEnabled)
                {
                    Motor.RefreshContacts();
                    Motor.StopHorizontal();
                    Motor.Simulate(0f, false, Time.fixedDeltaTime);
                }
                return;
            }
            if (IsScriptedTransferring) return;
            if (!IsUnderground)
            {
                Motor.RefreshContacts();
                Combat?.ObserveGrounded(Motor.IsGrounded);
            }
            bool wasUnderground = IsUnderground;
            Glitch?.Tick(now);
            bool relocated = false;
            if (wasUnderground)
            {
                if (pendingCancel) Glitch.CancelUnderground();
                else if (IsUnderground)
                {
                    Glitch.MoveUnderground(horizontalInput, Time.fixedDeltaTime);
                    if (pendingAttack && (Combat == null || Combat.CanAttack(now)) && Glitch.TryEmerge())
                    {
                        unchecked { RepositionVersion++; }
                        relocated = true;
                        FaceArrival();
                        Motor.RefreshContacts();
                        Combat?.ObserveArrival(false);
                        if (Combat != null) RefreshAimFacing();
                        Combat?.TryAttack(Glitch.ArrivalTarget, facing, true, now, Motor.IsGrounded);
                    }
                }
                ClearJumpState();
            }
            else
            {
                if (pendingGlitch && Glitch != null && CanReposition(now) && Glitch.TryExecute(glitchRequest, now))
                {
                    unchecked { RepositionVersion++; }
                    Combat?.CancelRecovery();
                    Combat?.ObserveArrival(glitchRequest.Direction == GlitchDirection.Up && !IsUnderground);
                    relocated = true;
                    ClearJumpState();
                    FaceArrival();
                }
                if (pendingAttack && !IsUnderground && Combat != null && Combat.CanAttack(now))
                {
                    Motor.RefreshContacts();
                    RefreshAimFacing();
                    Combat?.TryAttack(Glitch?.ArrivalTarget, facing, false, now, Motor.IsGrounded);
                }
            }
            pendingGlitch = pendingAttack = pendingCancel = false;
            glitchRequest = default;
            if (IsUnderground) return;
            bool wasDescending = Combat != null && Combat.IsDescending;
            if (Combat != null && Combat.Tick(now))
            {
                Motor.SetVerticalVelocity(Combat.LaunchSpeed);
                relocated = true;
            }
            if (Combat != null && Combat.ConsumeBufferedAttack(now))
            {
                Motor.RefreshContacts();
                RefreshAimFacing();
                Combat.TryAttack(Glitch?.ArrivalTarget, facing, false, now, Motor.IsGrounded);
            }
            ApplyImpactFeedback();
            bool movementLocked = Combat != null && Combat.BlocksMovement(Motor.IsGrounded);
            if (movementLocked)
            {
                // Attack-time jump presses must not escape into the first free physics step.
                jumpBuffer.Clear();
                jumpGraceAvailable = false;
                lastGroundedAt = double.NegativeInfinity;
            }
            if (movementLocked)
            {
                pendingJumpRelease = false;
                Motor.StopHorizontal();
            }
            if (Combat != null && Combat.IsPreparingSlam)
            {
                ClearJumpState();
                Motor.Hover();
                return;
            }
            if (Combat != null && Combat.IsDescending)
            {
                ClearJumpState();
                if (Motor.SimulateSlam(Combat.SlamSpeed, Time.fixedDeltaTime, out var impact))
                    Combat.LandSlam(impact, now);
                ApplyImpactFeedback();
                return;
            }
            if (wasDescending) Motor.StopSlam();
            if (relocated) { Motor.RefreshContacts(); return; }
            Motor.RefreshContacts();
            if (movementLocked)
            {
                // Keep gravity and attack-owned vertical motion, but suppress locomotion throughout Recovery.
                Motor.Simulate(0f, jumpHeld, Time.fixedDeltaTime);
                return;
            }
            if (Motor.IsGrounded) { lastGroundedAt = now; jumpGraceAvailable = true; }
            if (jumpGraceAvailable && now - lastGroundedAt <= tuning.CoyoteTime &&
                jumpBuffer.TryConsume(now, tuning.InputBufferTime))
            {
                Motor.Jump();
                jumpGraceAvailable = false;
            }
            if (pendingJumpRelease) { Motor.CutJump(); pendingJumpRelease = false; }
            Motor.Simulate(horizontalInput, jumpHeld, Time.fixedDeltaTime);
        }

        private void LateUpdate() => Feedback?.LateTick(Time.unscaledTimeAsDouble);

        private void ApplyImpactFeedback()
        {
            if (Combat != null && Combat.ConsumeImpact(out var impact))
                Feedback?.Play(impact, Time.unscaledTimeAsDouble, false);
            while (Combat != null && Combat.ConsumeHit(out var hit))
            {
                visual?.PresentHit(hit);
                Feedback?.PlayHit(hit, Time.unscaledTimeAsDouble);
            }
        }

        private bool UpdateReactions(double now)
        {
            if (health == null) return true;
            if (observedDamage != health.DamageVersion || (!health.IsAlive && double.IsNegativeInfinity(deathStartedAt)))
            {
                EndScriptedTransfer();
                ControlInterrupted?.Invoke();
            }
            if (!health.IsAlive)
            {
                if (double.IsNegativeInfinity(deathStartedAt))
                {
                    ClearActions();
                    Motor.StopSlam();
                    deathStartedAt = now;
                    if (observedDamage != health.DamageVersion)
                    {
                        observedDamage = health.DamageVersion;
                        PresentDamage();
                    }
                }
                if (now >= deathStartedAt + Mathf.Max(0f, deathDuration)) gameObject.SetActive(false);
                return false;
            }
            if (observedDamage != health.DamageVersion)
            {
                observedDamage = health.DamageVersion;
                if (Combat != null && (Combat.IsDescending || Combat.IsPreparingSlam)) Motor.StopSlam();
                Combat?.Interrupt(now);
                Feedback?.Reset();
                PresentDamage();
                Glitch?.CancelUnderground();
                pendingGlitch = pendingAttack = pendingCancel = false;
            }
            return true;
        }

        protected override void OnUnitDisabled()
        {
            EndScriptedTransfer();
            ControlInterrupted?.Invoke();
            ClearActions();
            Feedback?.Dispose();
            hitVisualStartedAt = double.NegativeInfinity;
            pendingDamageFeedback.Clear();
            if (health != null) observedDamage = health.DamageVersion;
            if (visual != null) visual.ResetActions();
        }

        private void ClearActions(bool preserveGlitchCooldown = false)
        {
            Feedback?.Reset();
            GlitchPreview = default;
            if (preserveGlitchCooldown) Glitch?.CancelUnderground();
            else Glitch?.Reset();
            if (Combat != null && (Combat.IsDescending || Combat.IsPreparingSlam)) Motor?.StopSlam();
            Combat?.Reset();
            pendingGlitch = pendingAttack = pendingCancel = false;
            glitchRequest = default;
            ClearJumpState();
            horizontalInput = 0f;
            jumpHeld = false;
            hasAim = false;
            aimScreenPosition = Vector2.zero;
        }

        private void OnDestroy()
        {
            if (health != null) health.DamageApplied -= OnDamageApplied;
            Feedback?.Dispose();
        }

        private void FaceArrival()
        {
            if (Glitch != null && Glitch.ArrivalTarget != null)
            {
                float delta = Glitch.ArrivalTarget.transform.position.x - Motor.Position.x;
                if (Mathf.Abs(delta) > 0.01f) facing = Mathf.Sign(delta);
            }
        }

        private void ClearJumpState()
        {
            jumpBuffer.Clear();
            pendingJumpRelease = false;
            jumpGraceAvailable = false;
            lastGroundedAt = double.NegativeInfinity;
        }
    }
}
