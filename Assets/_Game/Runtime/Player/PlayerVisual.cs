using UnityEngine;

namespace ActionPlatformer.Player
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class PlayerVisual : MonoBehaviour
    {
        [SerializeField] private CharacterMotor2D motor;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int Rising = Animator.StringToHash("Rising");

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            if (motor == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogError("Player visual needs a motor and an Animator Controller.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            Vector2 velocity = motor.Velocity;
            bool moving = Mathf.Abs(velocity.x) > 0.1f;
            if (moving) spriteRenderer.flipX = velocity.x < 0f;
            animator.SetBool(Moving, moving);
            animator.SetBool(Grounded, motor.IsGrounded);
            animator.SetBool(Rising, velocity.y > 0.1f);
        }
    }
}
