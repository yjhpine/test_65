using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    // Samples presentation only; guarding, damage and dash timing remain in the combat/FSM.
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class ShieldSoldierVisual : MonoBehaviour
    {
        private const float ReactionDuration = .32f;
        private const float BlockDuration = .24f;
        private static readonly int ActionTime = Animator.StringToHash("ActionTime");
        private Animator animator;
        private string animationName;
        private uint damageVersion, blockVersion;
        private float hurtAt = float.NegativeInfinity, blockedAt = float.NegativeInfinity;

        public void Initialize(Animator controller, uint damage, uint block)
        {
            animator = controller;
            ResetPresentation(damage, block);
        }

        public void Render(IEnemyActionState state, bool dead, float deathProgress,
            bool moving, uint damage, uint block)
        {
            bool newHit = damage != damageVersion;
            bool newBlock = block != blockVersion;
            if (newHit) { damageVersion = damage; hurtAt = Time.time; }
            if (newBlock) { blockVersion = block; blockedAt = Time.time; }

            string desired;
            float progress = 0f;
            if (dead) { desired = "Death"; progress = deathProgress; }
            else if (Time.time - hurtAt < ReactionDuration)
            { desired = "Hit"; progress = (Time.time - hurtAt) / ReactionDuration; }
            else if (Time.time - blockedAt < BlockDuration)
            { desired = "Block"; progress = (Time.time - blockedAt) / BlockDuration; }
            else if (state.Phase == EnemyPhase.Dash)
            { desired = "Attack"; progress = state.Progress; }
            else if (state.Phase == EnemyPhase.Preparing)
            {
                // Fit raise/hold/lower into the existing preparation; never delay the dash.
                if (state.Progress < .2f)
                { desired = "GuardEnter"; progress = state.Progress / .2f; }
                else if (state.Progress >= .8f)
                { desired = "GuardExit"; progress = (state.Progress - .8f) / .2f; }
                else desired = "GuardHold";
            }
            else desired = state.Phase == EnemyPhase.Idle && moving ? "Run" : "Idle";

            if (desired != animationName || (desired == "Hit" && newHit) || (desired == "Block" && newBlock))
            {
                animationName = desired;
                animator.Play(desired, 0, 0f);
            }
            animator.SetFloat(ActionTime, Mathf.Clamp01(progress));
        }

        public void ResetPresentation(uint damage, uint block)
        {
            damageVersion = damage;
            blockVersion = block;
            hurtAt = blockedAt = float.NegativeInfinity;
            animationName = null;
        }
    }
}
