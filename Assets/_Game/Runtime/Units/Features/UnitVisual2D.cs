using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class UnitVisual2D : MonoBehaviour
    {
        [Tooltip("Optional velocity source. Leave empty for a stationary unit.")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private bool spriteFacesRight;
        private static readonly int Moving = Animator.StringToHash("Moving");
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private bool wasMoving;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            bool hasMovingParameter = false;
            if (animator.runtimeAnimatorController != null)
                foreach (var parameter in animator.parameters)
                    if (parameter.nameHash == Moving && parameter.type == AnimatorControllerParameterType.Bool)
                    {
                        hasMovingParameter = true;
                        break;
                    }
            if (!hasMovingParameter)
            {
                Debug.LogError("Unit visual needs an Animator Controller with a Moving bool parameter.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            wasMoving = false;
            animator.SetBool(Moving, false);
            spriteRenderer.flipX = false;
        }

        private void Update()
        {
            float horizontal = body == null ? 0f : body.linearVelocity.x;
            bool moving = Mathf.Abs(horizontal) > 0.01f;
            if (moving) spriteRenderer.flipX = (horizontal > 0f) != spriteFacesRight;
            if (moving == wasMoving) return;
            animator.SetBool(Moving, moving);
            wasMoving = moving;
        }
    }
}
