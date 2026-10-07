using UnityEngine;

namespace ActionPlatformer.Cinematics
{
    [DisallowMultipleComponent]
    public sealed class CutsceneCoordinator : MonoBehaviour
    {
        [SerializeField] private CutsceneCameraRig cameraRig;
        public CutsceneCameraRig CameraRig => cameraRig;
        public CutsceneRunner Active { get; private set; }
        public bool IsBusy => Active != null;
        public bool TryAcquire(CutsceneRunner runner)
        {
            if (!isActiveAndEnabled || Active != null || runner == null || runner.gameObject.scene != gameObject.scene) return false;
            Active = runner;
            return true;
        }
        public void Release(CutsceneRunner runner) { if (Active == runner) Active = null; }
        public void CancelActive() { if (Active != null) Active.Cancel(); }
        private void OnDisable() => CancelActive();
    }
}
