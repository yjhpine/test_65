using ActionPlatformer.Cinematics;
using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Flow
{
    // The boss arena of a stage: stays asleep until the player walks into the trigger area, then wakes the boss and plays
    // the introduction. The persistent GameManager completes the stage once the boss is down.
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class StageBoss : MonoBehaviour
    {
        [Tooltip("Inactive until the introduction starts, so it cannot see or attack the player before that.")]
        [SerializeField] private UnitHealth boss;
        [Tooltip("Played once when the player enters the trigger area. The boss is active before it starts.")]
        [SerializeField] private CutsceneRunner introduction;
        [SerializeField] private BoxCollider2D area;
        [SerializeField, Min(0f)] private float clearDelay = 1.5f;

        private Collider2D playerBody;
        private double defeatedAt = double.NegativeInfinity;

        public UnitHealth Boss => boss;
        public bool IsConfigured => boss != null && introduction != null && area != null && area.isTrigger;
        // The introduction (and with it the fight) has begun; a boss that never woke is not a defeated boss.
        public bool Started { get; private set; }
        public bool IsDefeated => Started && boss != null && !boss.IsAlive;
        public float ClearDelay => clearDelay;

        private void Update()
        {
            if (Started || !IsConfigured || !Application.isPlaying) return;
            var player = introduction.Player;
            if (player == null || !player.isActiveAndEnabled || player.IsUnderground) return;
            if (playerBody == null) playerBody = player.GetComponent<Collider2D>();
            if (playerBody == null || !playerBody.isActiveAndEnabled || !area.isActiveAndEnabled ||
                !area.Distance(playerBody).isOverlapped) return;
            Begin();
        }

        private void Begin()
        {
            // The speech bubble needs the boss's anchor, so it wakes first; if the scene cannot start the
            // introduction right now (e.g. the player is being hit), it goes back to sleep and the next frame retries.
            boss.gameObject.SetActive(true);
            if (introduction.TryPlay()) Started = true;
            else boss.gameObject.SetActive(false);
        }

        // True once the boss has been down for the clear delay (game time), so its death animation can finish first.
        public bool ShouldComplete(double now)
        {
            if (!IsDefeated) { defeatedAt = double.NegativeInfinity; return false; }
            if (double.IsNegativeInfinity(defeatedAt)) defeatedAt = now;
            return now - defeatedAt >= clearDelay;
        }
    }
}
