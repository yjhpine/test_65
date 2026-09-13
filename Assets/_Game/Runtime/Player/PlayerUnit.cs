using ActionPlatformer.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionPlatformer.Player
{
    [RequireComponent(typeof(PlayerInput), typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class PlayerUnit : Unit
    {
        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private LayerMask groundMask = 1;
        [SerializeField] private PlayerVisual visual;
        private PlayerInputReader inputReader;
        private IPlayerInputSource input;
        private readonly InputBuffer jumpBuffer = new InputBuffer();
        private float horizontalInput;
        private bool jumpHeld;
        private double lastGroundedAt = double.NegativeInfinity;
        private bool jumpGraceAvailable;
        private bool pendingJumpRelease;

        public CharacterMotor2D Motor { get; private set; }

        protected override void OnUnitAwake()
        {
            if (tuning == null) { Debug.LogError("Player tuning is missing.", this); enabled = false; return; }
            Motor = new CharacterMotor2D(GetComponent<Rigidbody2D>(), GetComponent<CapsuleCollider2D>(), tuning, groundMask);
            inputReader = new PlayerInputReader(GetComponent<PlayerInput>());
            input = inputReader;
            if (visual != null) visual.Initialize(Motor);
        }

        public void SetInputSource(IPlayerInputSource source) => input = source ?? inputReader;

        protected override void OnUnitUpdate()
        {
            PlayerCommand command = input.Sample();
            SetMovementInput(command.Move.x, command.JumpHeld);
            if (command.JumpPressed) RequestJump();
            if (command.JumpReleased) RequestJumpRelease();
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
            Motor.RefreshContacts();
            double now = Time.timeAsDouble;
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
            jumpBuffer.Clear();
            horizontalInput = 0f;
            jumpHeld = false;
            pendingJumpRelease = false;
            jumpGraceAvailable = false;
            lastGroundedAt = double.NegativeInfinity;
        }
    }
}
