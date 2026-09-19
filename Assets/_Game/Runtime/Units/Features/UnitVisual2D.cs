using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class UnitVisual2D : MonoBehaviour
    {
        [Tooltip("Optional velocity source. Leave empty for a stationary unit.")]
        [SerializeField] private Rigidbody2D body;
        [Tooltip("Optional FSM animation source. Leave empty for velocity-only Idle/Run.")]
        [SerializeField] private Unit stateSource;
        [Tooltip("Normalized point in the Attack clip where the weapon connects.")]
        [SerializeField, Range(0.01f, 0.99f)] private float attackImpactNormalizedTime = 0.4f;
        [Tooltip("Seconds from impact to the end of the Attack clip.")]
        [SerializeField, Min(0.01f)] private float attackRecoveryDuration = 0.3f;
        [SerializeField] private bool spriteFacesRight;
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int ActionTime = Animator.StringToHash("ActionTime");
        private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
        private static readonly int Run = Animator.StringToHash("Base Layer.Run");
        private static readonly int Attack = Animator.StringToHash("Base Layer.Attack");
        private static readonly int Hit = Animator.StringToHash("Base Layer.Hit");
        private static readonly int Death = Animator.StringToHash("Base Layer.Death");
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private bool wasMoving;
        private bool ready;
        private IUnitAnimationState previousState;
        private uint previousVersion;
        private UnitAnimation currentAnimation;

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
                return;
            }
            if (stateSource != null)
            {
                bool hasActionTime = false;
                foreach (var parameter in animator.parameters)
                    hasActionTime |= parameter.nameHash == ActionTime && parameter.type == AnimatorControllerParameterType.Float;
                if (!hasActionTime || !animator.HasState(0, Attack) || !animator.HasState(0, Hit) || !animator.HasState(0, Death))
                {
                    Debug.LogError("FSM visual needs ActionTime float and Attack/Hit/Death states in Base Layer.", this);
                    enabled = false;
                    return;
                }
            }
            ready = true;
        }

        private void OnEnable()
        {
            if (!ready) return;
            wasMoving = false;
            previousState = null;
            previousVersion = 0;
            currentAnimation = UnitAnimation.Locomotion;
            animator.SetBool(Moving, false);
            animator.Play(Idle, 0, 0f);
            spriteRenderer.flipX = false;
        }

        private void LateUpdate()
        {
            float horizontal = body == null ? 0f : body.linearVelocity.x;
            bool moving = Mathf.Abs(horizontal) > 0.01f;
            if (moving) spriteRenderer.flipX = (horizontal > 0f) != spriteFacesRight;
            if (moving != wasMoving)
            {
                animator.SetBool(Moving, moving);
                wasMoving = moving;
            }

            var state = stateSource != null && stateSource.isActiveAndEnabled
                ? stateSource.Fsm?.CurrentState as IUnitAnimationState : null;
            UnitAnimation animation = state?.Animation ?? UnitAnimation.Locomotion;
            float progress = 0f;
            if (animation != UnitAnimation.Locomotion)
            {
                float elapsed = state.AnimationElapsed;
                float duration = state.AnimationDuration;
                if (animation == UnitAnimation.Attack)
                {
                    progress = duration > 0f && elapsed < duration
                        ? elapsed / duration * attackImpactNormalizedTime
                        : attackImpactNormalizedTime + (elapsed - duration) / Mathf.Max(0.01f, attackRecoveryDuration) * (1f - attackImpactNormalizedTime);
                    if (progress >= 1f) animation = UnitAnimation.Locomotion;
                    if (Mathf.Abs(state.FacingDirection) > 0.01f)
                        spriteRenderer.flipX = (state.FacingDirection > 0f) != spriteFacesRight;
                }
                else progress = duration > 0f ? elapsed / duration : 1f;
            }

            bool restart = !ReferenceEquals(state, previousState) || (state != null && state.AnimationVersion != previousVersion);
            if (animation != currentAnimation || (animation != UnitAnimation.Locomotion && restart))
            {
                int stateHash = animation == UnitAnimation.Attack ? Attack : animation == UnitAnimation.Hit ? Hit :
                    animation == UnitAnimation.Death ? Death : moving ? Run : Idle;
                animator.Play(stateHash, 0, 0f);
                currentAnimation = animation;
            }
            // Only presentation is sampled here; damage and state transitions remain in the FSM.
            if (animation != UnitAnimation.Locomotion) animator.SetFloat(ActionTime, Mathf.Clamp01(progress));
            previousState = state;
            previousVersion = state?.AnimationVersion ?? 0;
        }
    }
}
