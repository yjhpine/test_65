using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionPlatformer.Player
{
    [RequireComponent(typeof(PlayerInput), typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class PlayerUnit : Unit, IPlayerActionState
    {
        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private LayerMask groundMask = 1;
        [SerializeField] private PlayerVisual visual;
        [SerializeField] private GlitchTuning glitchTuning;
        [SerializeField] private PlayerCombatTuning combatTuning;
        [SerializeField] private Camera aimCamera;
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

        public CharacterMotor2D Motor { get; private set; }
        public PlayerGlitch Glitch { get; private set; }
        public PlayerCombat Combat { get; private set; }
        public GlitchPreview GlitchPreview { get; private set; }
        public bool IsUnderground => Glitch != null && Glitch.IsUnderground;
        public Vector2 UndergroundPosition => Glitch == null ? Vector2.zero : Glitch.UndergroundPosition;
        public Vector2 GroundMarkerPosition => Glitch == null ? Vector2.zero : Glitch.GroundMarkerPosition;
        public float Facing => Combat != null && Combat.IsAttacking ? Combat.Facing : facing;
        public PlayerAttackPhase AttackPhase => Combat == null ? PlayerAttackPhase.Ready : Combat.Phase;
        public PlayerAttackPresentation AttackPresentation => Combat == null ? default : Combat.GetPresentation(Time.timeAsDouble);
        public Bounds AttackBounds => Combat == null ? default : Combat.AttackBounds;
        public PlayerShockwave Shockwave => Combat == null ? default : Combat.GetShockwave(Time.timeAsDouble);
        public void SetAimCamera(Camera camera) => aimCamera = camera;

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
            if (visual != null) visual.Initialize(Motor, this);
            return true;
        }

        public void SetInputSource(IPlayerInputSource source) => input = source ?? inputReader;

        protected override void OnUnitUpdate()
        {
            PlayerCommand command = input.Sample();
            GlitchPreview = default;
            SetMovementInput(command.Move.x, command.JumpHeld);
            if (IsUnderground)
            {
                if (command.JumpPressed) pendingCancel = true;
            }
            else
            {
                if (command.JumpPressed) RequestJump();
                if (command.JumpReleased) RequestJumpRelease();
                if (Mathf.Abs(command.Move.x) > 0.01f) facing = Mathf.Sign(command.Move.x);
                if ((command.HasAim || command.GlitchPressed) && Glitch != null && aimCamera != null)
                {
                    Ray ray = aimCamera.ScreenPointToRay(command.AimScreenPosition);
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
            if (command.AttackPressed && (Combat != null || IsUnderground)) pendingAttack = true;
        }

        private bool CanReposition(double now) => Combat == null || Combat.CanReposition(now);

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
            if (health != null && !health.IsAlive) { OnUnitDisabled(); return; }
            if (health != null && observedDamage != health.DamageVersion)
            {
                observedDamage = health.DamageVersion;
                if (Combat != null && (Combat.IsDescending || Combat.IsPreparingSlam)) Motor.StopSlam();
                Combat?.Interrupt(now);
                Glitch?.CancelUnderground();
                pendingGlitch = pendingAttack = pendingCancel = false;
            }
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
                else if (IsUnderground && pendingAttack && (Combat == null || Combat.CanAttack(now)) && Glitch.TryEmerge())
                {
                    relocated = true;
                    FaceArrival();
                    Motor.RefreshContacts();
                    Combat?.ObserveArrival(false);
                    Combat?.TryAttack(Glitch.ArrivalTarget, facing, true, now, Motor.IsGrounded);
                }
                ClearJumpState();
            }
            else
            {
                if (pendingGlitch && Glitch != null && CanReposition(now) && Glitch.TryExecute(glitchRequest, now))
                {
                    Combat?.CancelRecovery();
                    Combat?.ObserveArrival(glitchRequest.Direction == GlitchDirection.Up && !IsUnderground);
                    relocated = true;
                    ClearJumpState();
                    FaceArrival();
                }
                if (pendingAttack && !IsUnderground)
                {
                    Motor.RefreshContacts();
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
                return;
            }
            if (wasDescending) Motor.StopSlam();
            if (relocated) { Motor.RefreshContacts(); return; }
            Motor.RefreshContacts();
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

        protected override void OnUnitDisabled()
        {
            GlitchPreview = default;
            Glitch?.Reset();
            if (Combat != null && (Combat.IsDescending || Combat.IsPreparingSlam)) Motor?.StopSlam();
            Combat?.Reset();
            pendingGlitch = pendingAttack = pendingCancel = false;
            glitchRequest = default;
            ClearJumpState();
            horizontalInput = 0f;
            jumpHeld = false;
            if (visual != null) visual.ResetActions();
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
