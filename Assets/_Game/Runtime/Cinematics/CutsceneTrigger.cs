using UnityEngine;

namespace ActionPlatformer.Cinematics
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class CutsceneTrigger : MonoBehaviour
    {
        [SerializeField] private CutsceneRunner runner;
        [SerializeField] private BoxCollider2D area;
        [SerializeField] private bool oncePerScene = true;
        private Collider2D playerBody;
        private bool wasInside;
        public bool HasCompleted { get; private set; }
        private void OnEnable() { if (runner != null) runner.Finished += OnFinished; }
        private void OnDisable() { if (runner != null) runner.Finished -= OnFinished; }
        private void Update()
        {
            if (runner == null || runner.Player == null || area == null || !area.isActiveAndEnabled) return;
            if (playerBody == null) playerBody = runner.Player.GetComponent<Collider2D>();
            if (playerBody == null || !playerBody.isActiveAndEnabled) return;
            bool inside = area.Distance(playerBody).isOverlapped;
            if (inside && !wasInside && !runner.Player.IsUnderground && !(oncePerScene && HasCompleted))
                runner.TryPlay();
            wasInside = inside;
        }
        private void OnFinished(CutsceneEndReason reason)
        {
            if (reason == CutsceneEndReason.Completed || reason == CutsceneEndReason.Skipped) HasCompleted = true;
        }
    }
}
