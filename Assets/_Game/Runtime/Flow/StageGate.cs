using UnityEngine;

namespace ActionPlatformer.Flow
{
    [DisallowMultipleComponent]
    public sealed class StageGate : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D barrier;
        [SerializeField] private SpriteRenderer marker;
        public bool IsConfigured => barrier != null && !barrier.isTrigger && marker != null;
        public bool IsLocked => barrier != null && barrier.enabled;
        public void SetLocked(bool locked)
        {
            barrier.enabled = locked;
            marker.color = locked ? new Color(.95f, .25f, .3f, .75f) : new Color(.2f, 1f, .8f, .3f);
        }
        // Collider bounds become empty when open; use authored geometry for entry detection.
        public float EntryX => barrier.transform.TransformPoint(barrier.offset + Vector2.right * barrier.size.x * .5f).x;
    }
}
