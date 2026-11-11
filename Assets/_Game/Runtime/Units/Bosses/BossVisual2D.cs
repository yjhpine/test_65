using System.Collections.Generic;
using UnityEngine;
using ActionPlatformer.Presentation;

namespace ActionPlatformer.Units.Bosses
{
    // Reads the boss brain's state and picks animator states, facing and the attack danger zone. It never changes gameplay.
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class BossVisual2D : MonoBehaviour
    {
        [SerializeField] private BossCombat2D stateSource;
        [SerializeField] private bool spriteFacesRight = true;
        [Tooltip("Shared red danger zone stretched over the pattern's telegraph box (Data/Presentation/AttackZone).")]
        [SerializeField] private AttackZoneProfile attackZone;
        private readonly AttackZoneOwner zone = new AttackZoneOwner();
        public Transform AttackZoneRoot => zone.SessionRoot;
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
            if (state == null || state.Settings == null) { zone.Hide(); return; }
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

        // Tracking and locked look the same; only the opacity rises with the pattern's urgency (0 -> 1 at the hit).
        private void DrawTelegraph(BossTelegraph telegraph)
        {
            zone.Hide();
            if (!telegraph.Visible) return;
            zone.Ensure(transform, attackZone, spriteRenderer);
            zone.Show(telegraph.Center, telegraph.Size, 0f, telegraph.Urgency);
        }

        private void OnDisable() => zone.Release();
    }
}
