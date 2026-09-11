using UnityEngine;

namespace ActionPlatformer.Player
{
    [RequireComponent(typeof(CharacterMotor2D), typeof(PlayerInputReader))]
    public sealed class PlayerController : MonoBehaviour
    {
        private CharacterMotor2D motor;
        private IPlayerInputSource input;
        private PlayerCommand command;
        private readonly InputBuffer jumpBuffer = new InputBuffer();
        private double lastGroundedAt = double.NegativeInfinity;
        private bool jumpGraceAvailable;
        private bool pendingJumpRelease;
        public CharacterMotor2D Motor => motor;

        private void Awake()
        {
            motor = GetComponent<CharacterMotor2D>();
            input = GetComponent<PlayerInputReader>();
        }

        public void SetInputSource(IPlayerInputSource source) => input = source ?? GetComponent<PlayerInputReader>();

        private void Update()
        {
            command = input.Sample();
            if (command.JumpPressed) jumpBuffer.Push(Time.timeAsDouble);
            pendingJumpRelease |= command.JumpReleased;
        }

        private void FixedUpdate()
        {
            if (motor.Tuning == null) return;
            motor.RefreshContacts();
            double now = Time.timeAsDouble;
            if (motor.IsGrounded) { lastGroundedAt = now; jumpGraceAvailable = true; }
            if (jumpGraceAvailable && now - lastGroundedAt <= motor.Tuning.CoyoteTime &&
                jumpBuffer.TryConsume(now, motor.Tuning.InputBufferTime))
            {
                motor.Jump();
                jumpGraceAvailable = false;
            }
            if (pendingJumpRelease) { motor.CutJump(); pendingJumpRelease = false; }
            motor.Simulate(command.Move.x, command.JumpHeld, Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            jumpBuffer.Clear();
            command = default;
            pendingJumpRelease = false;
            jumpGraceAvailable = false;
            lastGroundedAt = double.NegativeInfinity;
        }
    }
}
