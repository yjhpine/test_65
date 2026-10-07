using UnityEngine;

namespace ActionPlatformer.Cinematics
{
    [DisallowMultipleComponent]
    public sealed class DialogueSpeaker : MonoBehaviour
    {
        [SerializeField] private string displayName = "화자";
        [SerializeField] private Sprite portrait;
        [SerializeField] private Transform anchor;
        public string DisplayName => displayName;
        public Sprite Portrait => portrait;
        public Transform Anchor => anchor;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(displayName) && anchor != null &&
            (anchor == transform || anchor.IsChildOf(transform));
        public bool IsAvailable => isActiveAndEnabled && IsConfigured && anchor.gameObject.activeInHierarchy;
    }
}
