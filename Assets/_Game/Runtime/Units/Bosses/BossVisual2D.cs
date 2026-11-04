using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    // Reads the boss brain's state and picks animator states, facing and the telegraph box. It never changes gameplay.
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class BossVisual2D : MonoBehaviour
    {
        [SerializeField] private BossCombat2D stateSource;
        [SerializeField] private bool spriteFacesRight = true;
        [SerializeField] private SpriteRenderer telegraphRenderer;
        [SerializeField] private Color telegraphColor = new Color(1f, .54f, .16f, .35f);
        [SerializeField] private Color lockedTelegraphColor = new Color(1f, .25f, .2f, .35f);
        private static readonly int ActionTime = Animator.StringToHash("ActionTime");
        private readonly HashSet<string> warned = new HashSet<string>();
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private Rigidbody2D body;
        private bool hasActionTime;
        private string animationName;
        private uint playedVersion;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>(); animator = GetComponent<Animator>();
            foreach (var parameter in animator.parameters) if (parameter.nameHash == ActionTime && parameter.type == AnimatorControllerParameterType.Float) hasActionTime = true;
        }

        private void LateUpdate()
        {
            var state = stateSource == null ? null : stateSource.State;
            if (state == null || state.Settings == null) { HideTelegraph(); return; }
            if (body == null) body = stateSource.GetComponent<Rigidbody2D>();
            spriteRenderer.flipX = (state.Facing > 0f) != spriteFacesRight;
            string desired = DesiredAnimation(state);
            if (desired != animationName || state.StateVersion != playedVersion) Play(desired, state.StateVersion);
            if (hasActionTime) animator.SetFloat(ActionTime, Mathf.Clamp01(state.Progress));
            DrawTelegraph(state.Telegraph);
        }

        private string DesiredAnimation(IBossActionState state)
        {
            var settings = state.Settings; var pattern = state.Pattern;
            string name = null;
            switch (state.State)
            {
                case BossState.Death: return settings.DeathAnimation;
                case BossState.Staggered: return settings.StaggerAnimation;
                case BossState.PhaseShift: return settings.PhaseShiftAnimation;
                case BossState.Approach:
                    return body != null && Mathf.Abs(body.linearVelocity.x) > .2f ? settings.MoveAnimation : settings.IdleAnimation;
                case BossState.Telegraph: name = pattern == null ? null : pattern.TelegraphAnimation; break;
                case BossState.Execute: name = pattern == null ? null : pattern.ExecuteAnimation; break;
                case BossState.Recover: name = pattern == null ? null : pattern.RecoverAnimation; break;
            }
            return string.IsNullOrEmpty(name) ? settings.IdleAnimation : name;
        }

        private void Play(string name, uint version)
        {
            int hash = Animator.StringToHash(name);
            if (string.IsNullOrEmpty(name) || !animator.HasState(0, hash))
            {
                // Keep whatever is playing; warn once per missing name so a misconfigured boss does not spam the console.
                if (warned.Add(name ?? "")) Debug.LogWarning($"BossVisual2D '{name}': animator state not found on '{this.name}'.", this);
                // Remember the version so the missing state is not retried (and re-checked) every frame.
                animationName = name; playedVersion = version;
                return;
            }
            animationName = name; playedVersion = version;
            animator.Play(hash, 0, 0f);
        }

        private void DrawTelegraph(BossTelegraph telegraph)
        {
            if (telegraphRenderer == null) return;
            telegraphRenderer.enabled = telegraph.Visible;
            if (!telegraph.Visible) return;
            var t = telegraphRenderer.transform;
            t.position = new Vector3(telegraph.Center.x, telegraph.Center.y, t.position.z);
            // Assumes a 1-unit sprite; compensate for the parent's scale so the box is world-sized.
            var parent = t.parent == null ? Vector3.one : t.parent.lossyScale;
            t.localScale = new Vector3(Safe(telegraph.Size.x, parent.x), Safe(telegraph.Size.y, parent.y), t.localScale.z);
            telegraphRenderer.color = telegraph.Locked ? lockedTelegraphColor : telegraphColor;
        }

        private static float Safe(float size, float scale) => Mathf.Abs(scale) < 1e-4f ? size : size / scale;

        private void HideTelegraph() { if (telegraphRenderer != null) telegraphRenderer.enabled = false; }
    }
}
