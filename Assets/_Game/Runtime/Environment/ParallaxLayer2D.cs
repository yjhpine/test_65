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

        [Tooltip("Optional matching child image for a two-image horizontal belt. Each image must cover the camera width.")]
        [SerializeField] private SpriteRenderer repeatImage;

        [Tooltip("Optional copies stacked one image height above the belt: [0] matches the primary image, [1] the repeat image.")]
        [SerializeField] private SpriteRenderer[] upperImages;

        private Vector3[] initialUpperLocalPositions;
        private Vector3 initialLocalPosition;
        private Vector3 initialCameraPosition;
        private Vector3 initialRepeatLocalPosition;
        private SpriteRenderer primaryImage;
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

            if (repeatImage != null && (!TryGetComponent(out primaryImage) ||
                repeatImage.transform.parent != transform || primaryImage.sprite == null ||
                repeatImage.sprite != primaryImage.sprite || primaryImage.bounds.size.x <= 0f))
            {
                Debug.LogError("Parallax belt needs a matching child SpriteRenderer and a visible primary image.", this);
                enabled = false;
                return;
            }

            if (upperImages != null && upperImages.Length > 0)
            {
                bool valid = repeatImage != null && upperImages.Length <= 2;
                foreach (var image in upperImages) valid &= image != null && image.transform.parent == transform;
                if (!valid)
                {
                    Debug.LogError("Parallax upper images need a belt and at most two direct child SpriteRenderers.", this);
                    enabled = false;
                    return;
                }
            }

            if (!initialized)
            {
                if (upperImages != null)
                {
                    initialUpperLocalPositions = new Vector3[upperImages.Length];
                    for (int i = 0; i < upperImages.Length; i++) initialUpperLocalPositions[i] = upperImages[i].transform.localPosition;
                }
                initialLocalPosition = transform.localPosition;
                initialCameraPosition = viewCamera.transform.position;
                if (repeatImage != null) initialRepeatLocalPosition = repeatImage.transform.localPosition;
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
            if (repeatImage != null) WrapImages();
            PlaceUpperImages();
        }

        private void PlaceUpperImages()
        {
            if (upperImages == null || upperImages.Length == 0) return;
            float height = primaryImage.bounds.size.y;
            for (int i = 0; i < upperImages.Length; i++)
            {
                Transform source = i == 0 ? transform : repeatImage.transform;
                upperImages[i].transform.position = source.position + Vector3.up * height;
            }
        }

        private void WrapImages()
        {
            var bounds = primaryImage.bounds;
            double width = bounds.size.x;
            double cameraLeft = (double)viewCamera.transform.position.x - viewCamera.orthographicSize * viewCamera.aspect;
            // Cover the left edge and its next tile directly, even after a multi-tile camera teleport.
            double firstIndex = System.Math.Floor((cameraLeft - bounds.min.x) / width);
            bool primaryIsFirst = firstIndex % 2d == 0d;
            double primaryIndex = primaryIsFirst ? firstIndex : firstIndex + 1d;
            double repeatIndex = primaryIsFirst ? firstIndex + 1d : firstIndex;
            // Fixed even/odd identities preserve the alternating mirrored pattern in both directions.
            transform.position += Vector3.right * (float)(primaryIndex * width);
            repeatImage.transform.position = transform.position + Vector3.right * (float)((repeatIndex - primaryIndex) * width);
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            if (initialized) transform.localPosition = initialLocalPosition;
            if (initialized && repeatImage != null) repeatImage.transform.localPosition = initialRepeatLocalPosition;
            if (initialized && upperImages != null && initialUpperLocalPositions != null)
                for (int i = 0; i < upperImages.Length; i++) upperImages[i].transform.localPosition = initialUpperLocalPositions[i];
        }
    }
}
