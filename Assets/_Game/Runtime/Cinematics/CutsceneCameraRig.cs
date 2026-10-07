using ActionPlatformer.Flow;
using Unity.Cinemachine;
using UnityEngine;

namespace ActionPlatformer.Cinematics
{
    [DisallowMultipleComponent]
    public sealed class CutsceneCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera output;
        [SerializeField] private CinemachineBrain brain;
        [SerializeField] private StageCameraFollow follow;
        [SerializeField] private CinemachineCamera entryCamera;
        [SerializeField] private CinemachineCamera returnCamera;
        private Vector3 localPosition;
        private Quaternion localRotation;
        private LensSettings savedLens;
        private CameraSnapshot entrySnapshot, returnSnapshot;
        private struct CameraSnapshot
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public LensSettings Lens;
            public CameraSnapshot(CinemachineCamera camera)
            { Position = camera.transform.localPosition; Rotation = camera.transform.localRotation; Lens = camera.Lens; }
            public void Restore(CinemachineCamera camera)
            {
                if (camera == null) return;
                camera.transform.localPosition = Position; camera.transform.localRotation = Rotation; camera.Lens = Lens;
                camera.PreviousStateIsValid = false;
            }
        }
        private bool followEnabled, brainEnabled, ignoreTimeScale, acquired;
        private CinemachineBrain.UpdateMethods updateMethod;
        private CinemachineBrain.BrainUpdateMethods blendUpdateMethod;
        private CinemachineBlendDefinition defaultBlend;
        private int overrideId = -1;
        public CinemachineBrain Brain => brain;
        public CinemachineCamera EntryCamera => entryCamera;
        public Camera Output => output;
        public bool IsAcquired => acquired;
        public bool IsConfigured => output != null && output.orthographic && brain != null && brain.GetComponent<Camera>() == output &&
            follow != null && output.transform.parent == follow.transform && entryCamera != null && returnCamera != null;
        public void Begin()
        {
            if (acquired) return;
            acquired = true;
            localPosition = output.transform.localPosition;
            localRotation = output.transform.localRotation;
            savedLens = LensSettings.FromCamera(output);
            followEnabled = follow.enabled; brainEnabled = brain.enabled;
            updateMethod = brain.UpdateMethod; blendUpdateMethod = brain.BlendUpdateMethod;
            ignoreTimeScale = brain.IgnoreTimeScale; defaultBlend = brain.DefaultBlend;
            entrySnapshot = new CameraSnapshot(entryCamera); returnSnapshot = new CameraSnapshot(returnCamera);
            Capture(entryCamera);
            SetReturnPose();
            follow.enabled = false;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            brain.IgnoreTimeScale = true;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
            brain.enabled = true;
        }
        private void Capture(CinemachineCamera camera)
        {
            camera.transform.SetPositionAndRotation(output.transform.position, output.transform.rotation);
            camera.Lens = LensSettings.FromCamera(output);
            camera.PreviousStateIsValid = false;
        }
        private void SetReturnPose()
        {
            Vector3 rigPosition = Application.isPlaying ? follow.CalculateRigPosition() : follow.transform.position;
            Vector3 worldOffset = follow.transform.TransformVector(localPosition);
            returnCamera.transform.SetPositionAndRotation(rigPosition + worldOffset, follow.transform.rotation * localRotation);
            returnCamera.Lens = savedLens;
            returnCamera.PreviousStateIsValid = false;
        }
        public void CaptureReturnStart() => Capture(entryCamera);
        public void Tick(float deltaTime)
        {
            if (acquired) brain.ManualUpdate(Time.frameCount, deltaTime);
        }
        public void BlendBack(float progress, float deltaTime)
        {
            SetReturnPose();
            overrideId = brain.SetCameraOverride(overrideId, int.MaxValue, entryCamera, returnCamera,
                Mathf.SmoothStep(0, 1, progress), deltaTime);
            Tick(deltaTime);
        }
        public void End()
        {
            if (!acquired) return;
            acquired = false;
            if (brain != null)
            {
                if (overrideId >= 0) brain.ReleaseCameraOverride(overrideId);
                overrideId = -1;
                brain.enabled = brainEnabled;
                brain.UpdateMethod = updateMethod; brain.BlendUpdateMethod = blendUpdateMethod;
                brain.IgnoreTimeScale = ignoreTimeScale; brain.DefaultBlend = defaultBlend;
            }
            entrySnapshot.Restore(entryCamera); returnSnapshot.Restore(returnCamera);
            if (follow != null)
            {
                if (Application.isPlaying && followEnabled) follow.SnapToTarget();
                follow.enabled = followEnabled;
            }
            if (output != null)
            {
                output.transform.localPosition = localPosition; output.transform.localRotation = localRotation;
                output.orthographicSize = savedLens.OrthographicSize; output.fieldOfView = savedLens.FieldOfView;
                output.nearClipPlane = savedLens.NearClipPlane; output.farClipPlane = savedLens.FarClipPlane;
            }
        }
        private void OnDisable() => End();
    }
}
