using UnityEngine;

namespace ActionPlatformer.Player
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class PlayerVisual : MonoBehaviour
    {
        [Tooltip("Overlay the actual hit area while attacking (debug display).")]
        [SerializeField] private bool showAttackArea = true;
        private ICharacterMotionState motionState;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private IPlayerActionState actions;
        private Vector3 originalPosition;
        private Color originalColor;
        private SpriteRenderer attackShape, groundMarker;
        private SpriteRenderer glitchFill;
        private readonly SpriteRenderer[] glitchEdges = new SpriteRenderer[4];
        private readonly SpriteRenderer[] shockwaveEdges = new SpriteRenderer[24];
        private Sprite shapeSprite;
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int Rising = Animator.StringToHash("Rising");
        private static readonly int ActionOverride = Animator.StringToHash("ActionOverride");
        private static readonly int ActionTime = Animator.StringToHash("ActionTime");
        private static readonly int[] GroundAttacks = { Animator.StringToHash("Base Layer.Attack1"),
            Animator.StringToHash("Base Layer.Attack2"), Animator.StringToHash("Base Layer.Attack3") };
        private static readonly int[] AirAttacks = { Animator.StringToHash("Base Layer.AirAttack1"),
            Animator.StringToHash("Base Layer.AirAttack2"), Animator.StringToHash("Base Layer.AirAttack3") };
        private static readonly int Lift = Animator.StringToHash("Base Layer.Lift");
        private static readonly int Emergence = Animator.StringToHash("Base Layer.Emergence");
        private static readonly int SlamHover = Animator.StringToHash("Base Layer.SlamHover");
        private static readonly int SlamFall = Animator.StringToHash("Base Layer.SlamFall");
        private static readonly int SlamLand = Animator.StringToHash("Base Layer.SlamLand");
        private int currentAction;
        private uint previousAttackVersion;
        private bool hasActionAnimations;

        public void Initialize(ICharacterMotionState motionState, IPlayerActionState actions = null)
        {
            this.motionState = motionState ?? throw new System.ArgumentNullException(nameof(motionState));
            this.actions = actions;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            originalPosition = transform.localPosition;
            originalColor = spriteRenderer.color;
            foreach (var parameter in animator.parameters)
                hasActionAnimations |= parameter.nameHash == ActionOverride && parameter.type == AnimatorControllerParameterType.Bool;
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
            if (actions != null)
            {
                shapeSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                attackShape = CreateShape("Attack area", new Color(1f, 0.8f, 0.2f, 0.4f));
                groundMarker = CreateShape("Underground marker", new Color(0.15f, 1f, 1f, 0.9f));
                glitchFill = CreateShape("Glitch preview", Color.white);
                for (int i = 0; i < glitchEdges.Length; i++)
                    glitchEdges[i] = CreateShape("Glitch preview edge " + i, Color.white);
                for (int i = 0; i < shockwaveEdges.Length; i++)
                    shockwaveEdges[i] = CreateShape("Shockwave " + i, Color.white);
            }
        }

        private void LateUpdate()
        {
            if (motionState == null) return;
            bool underground = actions != null && actions.IsUnderground;
            spriteRenderer.enabled = !underground;
            if (underground)
                transform.position = new Vector3(actions.UndergroundPosition.x, actions.UndergroundPosition.y, transform.position.z);
            else transform.localPosition = originalPosition;
            Vector2 velocity = motionState.Velocity;
            bool moving = Mathf.Abs(velocity.x) > 0.1f;
            if (actions != null) spriteRenderer.flipX = actions.Facing < 0f;
            else if (moving) spriteRenderer.flipX = velocity.x < 0f;
            animator.SetBool(Moving, moving && !underground);
            animator.SetBool(Grounded, motionState.IsGrounded);
            animator.SetBool(Rising, velocity.y > 0.1f);
            UpdateAttackAnimation(underground, moving, velocity.y > 0.1f);
            spriteRenderer.color = originalColor;
            if (attackShape != null)
            {
                attackShape.enabled = showAttackArea && !underground &&
                    (actions.AttackPhase == PlayerAttackPhase.Active || actions.AttackPhase == PlayerAttackPhase.Descending) &&
                    actions.AttackBounds.size.x > 0f;
                if (attackShape.enabled)
                {
                    var bounds = actions.AttackBounds;
                    attackShape.transform.position = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
                    attackShape.transform.localScale = new Vector3(bounds.size.x, bounds.size.y, 1f);
                }
                groundMarker.enabled = underground;
                if (underground)
                {
                    Vector2 point = actions.GroundMarkerPosition;
                    groundMarker.transform.position = new Vector3(point.x, point.y + 0.04f, transform.position.z);
                    groundMarker.transform.localScale = new Vector3(0.8f, 0.08f, 1f);
                }
            }
            UpdateGlitchPreview(underground);
            UpdateShockwave(underground);
        }

        private void UpdateAttackAnimation(bool underground, bool moving, bool rising)
        {
            if (!hasActionAnimations) return;
            var phase = actions?.AttackPhase ?? PlayerAttackPhase.Ready;
            var attack = actions?.AttackPresentation ?? default;
            // A timed-out descent has no ground impact to display.
            bool airborneSlamRecovery = attack.Kind == PlayerAttack.Slam &&
                phase == PlayerAttackPhase.Recovery && !motionState.IsGrounded;
            bool active = !underground && phase != PlayerAttackPhase.Ready && !airborneSlamRecovery;
            animator.SetBool(ActionOverride, active);
            if (!active)
            {
                if (currentAction != 0)
                    animator.Play(motionState.IsGrounded ? (moving ? "Run" : "Idle") : (rising ? "Jump" : "Fall"), 0, 0f);
                currentAction = 0;
                return;
            }
            int strike = Mathf.Clamp(attack.Strike, 1, 3) - 1;
            int state = attack.Kind == PlayerAttack.Slam
                ? (phase == PlayerAttackPhase.Windup ? SlamHover : phase == PlayerAttackPhase.Descending ? SlamFall : SlamLand)
                : attack.Kind == PlayerAttack.Lift ? Lift : attack.Kind == PlayerAttack.Emergence ? Emergence
                : attack.Airborne ? AirAttacks[strike] : GroundAttacks[strike];
            // Clips place their impact at 0.35 and recovery at 0.6. Combat owns all real timing.
            float progress = phase == PlayerAttackPhase.Windup ? attack.PhaseProgress * 0.35f
                : phase == PlayerAttackPhase.Active ? Mathf.Lerp(0.35f, 0.6f, attack.PhaseProgress)
                : Mathf.Lerp(0.6f, 0.999f, attack.PhaseProgress);
            if (attack.Kind == PlayerAttack.Slam && phase == PlayerAttackPhase.Active)
                progress = attack.PhaseProgress * 0.6f;
            animator.SetFloat(ActionTime, progress);
            if (state != currentAction || attack.Version != previousAttackVersion)
                animator.Play(state, 0, 0f);
            currentAction = state;
            previousAttackVersion = attack.Version;
        }

        private void UpdateShockwave(bool underground)
        {
            if (actions == null) return;
            var wave = actions.Shockwave;
            float radius = wave.Radius * Mathf.Lerp(0.15f, 1f, wave.Progress);
            Color color = new Color(1f, 0.8f, 0.25f, 1f - wave.Progress);
            for (int i = 0; i < shockwaveEdges.Length; i++)
            {
                var edge = shockwaveEdges[i];
                if (edge == null) continue;
                edge.enabled = wave.Visible && !underground;
                if (!edge.enabled) continue;
                float a = Mathf.PI * i / shockwaveEdges.Length;
                float b = Mathf.PI * (i + 1) / shockwaveEdges.Length;
                Vector2 from = wave.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                Vector2 to = wave.Center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
                SetPreviewShape(edge, (from + to) * 0.5f, new Vector2(Vector2.Distance(from, to) + 0.015f, 0.065f), color);
                edge.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
            }
        }

        private void UpdateGlitchPreview(bool underground)
        {
            if (glitchFill == null) return;
            var preview = actions.GlitchPreview;
            bool visible = preview.Visible && !underground;
            glitchFill.enabled = visible;
            for (int i = 0; i < glitchEdges.Length; i++) glitchEdges[i].enabled = visible;
            if (!visible) return;

            Color color = preview.CanExecute ? new Color(0.15f, 1f, 1f, 0.95f) : new Color(1f, 0.25f, 0.2f, 0.95f);
            var bounds = preview.Bounds;
            Vector2 center = bounds.center;
            if (preview.IsUnderground) center.y += 0.04f;
            SetPreviewShape(glitchFill, center, bounds.size, new Color(color.r, color.g, color.b, 0.12f));
            const float thickness = 0.035f;
            Vector2 size = bounds.size;
            SetPreviewShape(glitchEdges[0], center + Vector2.up * size.y * 0.5f, new Vector2(size.x, thickness), color);
            SetPreviewShape(glitchEdges[1], center + Vector2.down * size.y * 0.5f, new Vector2(size.x, thickness), color);
            SetPreviewShape(glitchEdges[2], center + Vector2.left * size.x * 0.5f, new Vector2(thickness, size.y), color);
            SetPreviewShape(glitchEdges[3], center + Vector2.right * size.x * 0.5f, new Vector2(thickness, size.y), color);
        }

        private void SetPreviewShape(SpriteRenderer shape, Vector2 center, Vector2 size, Color color)
        {
            shape.color = color;
            shape.transform.SetPositionAndRotation(new Vector3(center.x, center.y, transform.position.z), Quaternion.identity);
            var scale = transform.lossyScale;
            shape.transform.localScale = new Vector3(size.x / Mathf.Abs(scale.x), size.y / Mathf.Abs(scale.y), 1f);
        }

        private SpriteRenderer CreateShape(string name, Color color)
        {
            var shape = new GameObject(name);
            shape.transform.SetParent(transform, false);
            var renderer = shape.AddComponent<SpriteRenderer>();
            renderer.sprite = shapeSprite;
            renderer.sharedMaterial = spriteRenderer.sharedMaterial;
            renderer.sortingLayerID = spriteRenderer.sortingLayerID;
            renderer.sortingOrder = spriteRenderer.sortingOrder + 2;
            renderer.color = color;
            renderer.enabled = false;
            return renderer;
        }

        public void ResetActions()
        {
            if (spriteRenderer == null) return;
            transform.localPosition = originalPosition;
            spriteRenderer.enabled = true;
            spriteRenderer.color = originalColor;
            currentAction = 0;
            previousAttackVersion = 0;
            if (hasActionAnimations)
            {
                animator.SetBool(ActionOverride, false);
                animator.SetFloat(ActionTime, 0f);
                if (animator.isActiveAndEnabled) animator.Play("Idle", 0, 0f);
            }
            if (attackShape != null) attackShape.enabled = false;
            if (groundMarker != null) groundMarker.enabled = false;
            if (glitchFill != null) glitchFill.enabled = false;
            for (int i = 0; i < glitchEdges.Length; i++)
                if (glitchEdges[i] != null) glitchEdges[i].enabled = false;
            for (int i = 0; i < shockwaveEdges.Length; i++)
                if (shockwaveEdges[i] != null) shockwaveEdges[i].enabled = false;
        }

        private void OnDisable() => ResetActions();
        private void OnDestroy() { if (shapeSprite != null) Destroy(shapeSprite); }
    }
}
