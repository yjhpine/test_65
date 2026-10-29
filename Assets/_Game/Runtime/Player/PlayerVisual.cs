using UnityEngine;
using ActionPlatformer.Presentation;

namespace ActionPlatformer.Player
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class PlayerVisual : MonoBehaviour
    {
        [Tooltip("Overlay the actual hit area while attacking (debug display).")]
        [SerializeField] private bool showAttackArea = true;
        [Header("Movement and contact VFX")]
        private VfxManager effects;
        private EffectOwnerHandle effectOwner;
        public Transform EffectsRoot => effects?.GetSessionRoot(effectOwner);
        private PlayerMovementObserver movementObserver;
        private uint observedGlitch;
        [SerializeField] private ActionPlatformer.Presentation.PlayerVfxProfile vfxProfile;
        [Header("Sound effects")]
        [SerializeField] private PlayerSfxDefinition soundEffects;
        private PlayerSoundEffects audioEffects;

        private readonly System.Collections.Generic.Queue<PlayerHitPresentation> pendingHits = new System.Collections.Generic.Queue<PlayerHitPresentation>();
        private readonly PlayerAttackAlternation alternation = new PlayerAttackAlternation();
        private MaterialPropertyBlock reactionProperties;
        private Quaternion originalRotation;
        private bool reducedMotion;
        private uint landingVersion, alternationVersion, reactionAttackVersion;
        private uint observedReactionVersion;
        public void SetReducedMotion(bool value) { reducedMotion = value; }
        public void PresentHit(PlayerHitPresentation hit) => pendingHits.Enqueue(hit);
        public void PresentTransferSpark(Vector2 point) => effects?.PlayTransferSpark(effectOwner,point);
        public void PresentTransferSound() => audioEffects?.PlayGlitch();
        public void NotifyDamage()
        {
            observedReactionVersion = actions?.ReactionPresentation.Version ?? 0;
            alternation.Reset(); reactionAttackVersion = actions?.AttackPresentation.Version ?? 0;
        }
        private ICharacterMotionState motionState;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private IPlayerActionState actions;
        private Vector3 originalPosition;
        private Color originalColor;
        private SpriteRenderer attackShape, groundMarker;
        private SpriteRenderer glitchFill;
        private SpriteRenderer glitchRing;
        private Sprite previewDiscSprite, previewRingSprite;
        private Texture2D previewDiscTexture, previewRingTexture;
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
        private static readonly int Hit = Animator.StringToHash("Base Layer.Hit");
        private static readonly int Die = Animator.StringToHash("Base Layer.Die");
        private uint previousReactionVersion;
        private int currentAction;
        private uint previousAttackVersion;
        private bool hasActionAnimations;

        public void Initialize(ICharacterMotionState motionState, IPlayerActionState actions = null)
        {
            this.motionState = motionState ?? throw new System.ArgumentNullException(nameof(motionState));
            this.actions = actions;
            observedGlitch = actions?.GlitchPresentation.Version ?? 0;
            effects?.ResetOwner(effectOwner,actions?.GlitchPresentation.Version ?? 0);
        }

        private void Awake()
        {
            reactionProperties = new MaterialPropertyBlock();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;
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
                if (soundEffects != null) audioEffects = new PlayerSoundEffects(transform, soundEffects);
                audioEffects?.Reset(actions);
                movementObserver=new PlayerMovementObserver((kind,point,flip)=> {
                    effects?.PlayDust(effectOwner,kind,point,flip);
                    if(kind==MovementDustKind.Landing)audioEffects?.Land(point,false);
                });
                EnsureEffects();
                shapeSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                attackShape = CreateShape("Attack area", new Color(1f, 0.8f, 0.2f, 0.4f));
                groundMarker = CreateShape("Underground marker", new Color(0.15f, 1f, 1f, 0.9f));
                glitchFill = CreateShape("Glitch preview", Color.white);
                previewDiscSprite = CreatePreviewCircle(false, out previewDiscTexture);
                previewRingSprite = CreatePreviewCircle(true, out previewRingTexture);
                glitchFill.sprite = previewDiscSprite;
                glitchRing = CreateShape("Glitch preview ring", Color.white);
                glitchRing.sprite = previewRingSprite;
            }
        }

        private void LateUpdate()
        {
            if (motionState == null) return;
            EnsureEffects();
            audioEffects?.Observe(actions);
            bool underground = actions != null && actions.IsUnderground;
            var attack = actions?.AttackPresentation ?? default;
            var reaction = actions?.ReactionPresentation ?? default;
            if (reaction.Kind != PlayerReaction.None && reaction.Version != observedReactionVersion)
            {
                observedReactionVersion = reaction.Version;
                NotifyDamage();
            }
            if (actions != null && actions.AttackPhase != PlayerAttackPhase.Ready && attack.Version != alternationVersion)
            {
                alternationVersion = attack.Version;
                alternation.Begin(attack.Kind, attack.StartedAt);
            }
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
            // Apply the selected sprite in this display frame, including during a global hit stop.
            if (hasActionAnimations && animator.GetBool(ActionOverride)) animator.Update(0f);
            spriteRenderer.color = originalColor;
            bool hurt = reaction.Kind == PlayerReaction.Hit;
            bool newAttack = actions != null && actions.AttackPhase != PlayerAttackPhase.Ready && attack.Version != reactionAttackVersion;
            transform.localRotation = originalRotation;
            if (hurt && !newAttack && reaction.HasDirection && !reducedMotion)
                transform.localRotation *= Quaternion.Euler(0, 0, -reaction.Direction.x * 7f * Mathf.Sin(reaction.Progress * Mathf.PI));
            spriteRenderer.GetPropertyBlock(reactionProperties);
            reactionProperties.SetFloat("_HitFlash", hurt ? Mathf.Clamp01(1 - reaction.Progress * 2) : 0);
            reactionProperties.SetFloat("_OutlineWidth", hurt ? 1f : 0);
            spriteRenderer.SetPropertyBlock(reactionProperties);
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
                    groundMarker.color = actions.CanEmerge ? new Color(0.15f, 1f, 1f, 0.9f) : new Color(1f, 0.25f, 0.2f, 0.9f);
                    Vector2 point = actions.GroundMarkerPosition;
                    groundMarker.transform.position = new Vector3(point.x, point.y + 0.04f, transform.position.z);
                    groundMarker.transform.localScale = new Vector3(0.8f, 0.08f, 1f);
                }
            }
            UpdateGlitchPreview(underground);
            while (pendingHits.Count > 0) { var hit = pendingHits.Dequeue(); effects?.PlayContact(effectOwner,hit); audioEffects?.Hit(hit); }
            var wave = actions?.Shockwave ?? default;
            bool slamLanding = wave.Visible && wave.Version != landingVersion;
            if (slamLanding)
            {
                landingVersion = wave.Version;
                effects?.PlayDust(effectOwner,wave.Center,true);
                audioEffects?.Land(wave.Center,true);
            }
            Vector2 feet=transform.TransformPoint(vfxProfile!=null?vfxProfile.DustFootOffset:new Vector2(0,-.83f));
            movementObserver?.Tick(feet,velocity,motionState.IsGrounded,
                underground || reaction.Kind!=PlayerReaction.None,actions?.GlitchPresentation.Version??0,
                slamLanding,Time.unscaledTimeAsDouble);
            if(actions!=null && observedGlitch!=actions.GlitchPresentation.Version) {
                observedGlitch=actions.GlitchPresentation.Version;
                effects?.PlayGlitch(effectOwner,actions.GlitchPresentation);
            }
            effects?.SubmitAttack(effectOwner,actions,alternation.Variant,
                reaction.Kind==PlayerReaction.Die || (hurt && !newAttack));
        }

        private void EnsureEffects()
        {
            if(actions==null || vfxProfile==null || (effects!=null && effects.IsValid(effectOwner)))return;
            effects=EffectsRuntime.Vfx;
            if(effects!=null) {
                effectOwner=effects.RegisterOwner(transform,vfxProfile,spriteRenderer);
                effects.ResetOwner(effectOwner,actions.GlitchPresentation.Version);
                observedGlitch=actions.GlitchPresentation.Version;
                audioEffects?.Reset(actions);
            }
        }

        private void UpdateAttackAnimation(bool underground, bool moving, bool rising)
        {
            if (!hasActionAnimations) return;
            var reaction = actions?.ReactionPresentation ?? default;
            bool newAttack = actions != null && actions.AttackPhase != PlayerAttackPhase.Ready &&
                actions.AttackPresentation.Version != reactionAttackVersion;
            if (reaction.Kind == PlayerReaction.Die || (reaction.Kind == PlayerReaction.Hit && !newAttack && reaction.HasDirection))
            {
                int reactionState = reaction.Kind == PlayerReaction.Die ? Die : Hit;
                animator.SetBool(ActionOverride, true);
                animator.SetFloat(ActionTime, Mathf.Min(reaction.Progress, 0.999f));
                if (currentAction != reactionState || reaction.Version != previousReactionVersion)
                    animator.Play(reactionState, 0, 0f);
                currentAction = reactionState;
                previousReactionVersion = reaction.Version;
                return;
            }
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
            int strike = alternation.Variant;
            int state = attack.Kind == PlayerAttack.Slam
                ? (phase == PlayerAttackPhase.Windup ? SlamHover : phase == PlayerAttackPhase.Descending ? SlamFall : SlamLand)
                : attack.Kind == PlayerAttack.Lift ? Lift : attack.Kind == PlayerAttack.Emergence ? Emergence
                : attack.Kind == PlayerAttack.Air ? AirAttacks[strike] : GroundAttacks[strike];
            // Clips place their impact at 0.35 and recovery at 0.6. Combat owns all real timing.
            float progress = phase == PlayerAttackPhase.Windup ? 0.35f
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

        private void UpdateGlitchPreview(bool underground)
        {
            if (glitchFill == null) return;
            var preview = actions.GlitchPreview;
            bool visible = preview.Visible && !underground;
            glitchFill.enabled = visible;
            glitchRing.enabled = visible;
            if (!visible) return;

            Color color = preview.CanExecute ? new Color(0.15f, 1f, 1f, 0.95f) : new Color(1f, 0.25f, 0.2f, 0.95f);
            var bounds = preview.Bounds;
            Vector2 center = bounds.center;
            if (preview.IsUnderground) center.y += 0.04f;
            // A destination marker, not a depiction of the unchanged body collision bounds.
            Vector2 size = Vector2.one * .8f;
            SetPreviewShape(glitchFill, center, size, new Color(color.r, color.g, color.b, 0.12f));
            SetPreviewShape(glitchRing, center, size, color);
        }

        private static Sprite CreatePreviewCircle(bool ring, out Texture2D texture)
        {
            const int resolution = 64;
            texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.name = ring ? "Glitch preview ring" : "Glitch preview disc";
            texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[resolution * resolution];
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    float distance = new Vector2(x + .5f - resolution * .5f, y + .5f - resolution * .5f).magnitude;
                    float alpha = Mathf.Clamp01(31.5f - distance);
                    if (ring) alpha *= Mathf.Clamp01(distance - 27.5f);
                    pixels[y * resolution + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), Vector2.one * .5f, resolution,
                0, SpriteMeshType.FullRect);
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
            if (spriteRenderer == null || reactionProperties == null) return;
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
            spriteRenderer.enabled = true;
            spriteRenderer.color = originalColor;
            currentAction = 0;
            previousAttackVersion = 0;
            previousReactionVersion = 0;
            observedReactionVersion = 0;
            alternationVersion = actions?.AttackPresentation.Version ?? 0;
            landingVersion = actions?.Shockwave.Version ?? 0;
            alternation.Reset(); pendingHits.Clear(); movementObserver?.Reset(); observedGlitch=actions?.GlitchPresentation.Version??0;
            audioEffects?.Reset(actions);
            spriteRenderer.GetPropertyBlock(reactionProperties);
            reactionProperties.SetFloat("_HitFlash", 0); reactionProperties.SetFloat("_OutlineWidth", 0);
            spriteRenderer.SetPropertyBlock(reactionProperties);
            effects?.ResetOwner(effectOwner,actions?.GlitchPresentation.Version ?? 0);
            if (hasActionAnimations)
            {
                animator.SetBool(ActionOverride, false);
                animator.SetFloat(ActionTime, 0f);
                if (animator.isActiveAndEnabled) animator.Play("Idle", 0, 0f);
            }
            if (attackShape != null) attackShape.enabled = false;
            if (groundMarker != null) groundMarker.enabled = false;
            if (glitchFill != null) glitchFill.enabled = false;
            if (glitchRing != null) glitchRing.enabled = false;
        }

        private void OnDisable()
        {
            ResetActions(); effects?.ReleaseOwner(effectOwner); effectOwner=default; audioEffects?.Release();
        }
        private void OnDestroy()
        {
            if (shapeSprite != null) Destroy(shapeSprite);
            if (previewDiscSprite != null) Destroy(previewDiscSprite);
            if (previewRingSprite != null) Destroy(previewRingSprite);
            if (previewDiscTexture != null) Destroy(previewDiscTexture);
            if (previewRingTexture != null) Destroy(previewRingTexture);
        }
    }
}
