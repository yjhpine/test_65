using UnityEngine;
using UnityEngine.Rendering;

namespace ActionPlatformer.Environment
{
    [DisallowMultipleComponent]
    public sealed class ParallaxLayer2D : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [Tooltip("Screen-space scroll relative to fixed terrain: 0 follows the camera, 1 stays in the world.")]
        [SerializeField, Range(0f, 1f)] private float scrollSpeed = .2f;

        private Vector3 initialLocalPosition;
        private Vector3 initialCameraPosition;
        private bool initialized;

        public Camera ViewCamera => viewCamera;
        public float ScrollSpeed => scrollSpeed;

        private void OnEnable()
        {
            if (viewCamera == null || !viewCamera.orthographic || viewCamera.transform.IsChildOf(transform))
            {
                Debug.LogError("Parallax layer needs an external orthographic camera.", this);
                enabled = false;
                return;
            }

            if (!initialized)
            {
                initialLocalPosition = transform.localPosition;
                initialCameraPosition = viewCamera.transform.position;
                initialized = true;
            }

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            ApplyCameraPosition();
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // Sample after camera follow, Cinemachine blends and damage feedback have all finished.
            if (camera == viewCamera) ApplyCameraPosition();
        }

        private void ApplyCameraPosition()
        {
            Vector3 offset = viewCamera.transform.position - initialCameraPosition;
            offset.z = 0f;
            Vector3 origin = transform.parent != null
                ? transform.parent.TransformPoint(initialLocalPosition) : initialLocalPosition;
            // Absolute displacement avoids accumulated error on shake, teleports and return blends.
            transform.position = origin + offset * (1f - scrollSpeed);
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            if (initialized) transform.localPosition = initialLocalPosition;
        }
    }
}
