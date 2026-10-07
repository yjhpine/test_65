using UnityEngine;

namespace ActionPlatformer.Flow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class StageGoal : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D area;
        [SerializeField] private SpriteRenderer marker;
        [SerializeField] private Color lockedColor = new Color(0.85f, 0.24f, 0.3f, 0.8f);
        [SerializeField] private Color unlockedColor = new Color(0.15f, 0.9f, 0.8f, 0.8f);
        public bool IsConfigured => area != null && area.isTrigger && marker != null;
        public bool IsUnlocked { get; private set; }

        public void SetUnlocked(bool unlocked)
        {
            IsUnlocked = unlocked;
            marker.color = unlocked ? unlockedColor : lockedColor;
        }

        public bool Contains(Collider2D body)
        {
            // Shape queries see teleports without stale trigger-enter/exit state.
            return isActiveAndEnabled && area.enabled && body != null && body.isActiveAndEnabled &&
                area.Distance(body).isOverlapped;
        }
    }
}
