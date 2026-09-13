using UnityEngine;

namespace ActionPlatformer.Player
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class PlayerVisual : MonoBehaviour
    {
        private ICharacterMotionState motionState;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int Rising = Animator.StringToHash("Rising");

        public void Initialize(ICharacterMotionState motionState)
        {
            this.motionState = motionState ?? throw new System.ArgumentNullException(nameof(motionState));
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogError("Player visual needs an Animator Controller.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            // PlayerUnit binds the state during Awake, regardless of component Awake order.
            if (motionState == null)
            {
                Debug.LogError("Player visual needs a movement state source.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (motionState == null) return;
            Vector2 velocity = motionState.Velocity;
            bool moving = Mathf.Abs(velocity.x) > 0.1f;
            if (moving) spriteRenderer.flipX = velocity.x < 0f;
            animator.SetBool(Moving, moving);
            animator.SetBool(Grounded, motionState.IsGrounded);
            animator.SetBool(Rising, velocity.y > 0.1f);
        }
    }
}
