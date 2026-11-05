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

        [Tooltip("Optional copies stacked one image height below the belt (same order), for stages that descend.")]
        [SerializeField] private SpriteRenderer[] lowerImages;

        private Vector3[] initialUpperLocalPositions;
        private Vector3[] initialLowerLocalPositions;
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

            if (!ValidStack(upperImages) || !ValidStack(lowerImages))
            {
                Debug.LogError("Parallax upper/lower images need a belt and at most two direct child SpriteRenderers each.", this);
                enabled = false;
                return;
            }

            if (!initialized)
            {
                initialUpperLocalPositions = SaveLocalPositions(upperImages);
                initialLowerLocalPositions = SaveLocalPositions(lowerImages);
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
            PlaceStack(upperImages, 1f);
            PlaceStack(lowerImages, -1f);
        }

        // Copies of the belt one image height above (+1) or below (-1); [0] follows the primary image, [1] the repeat image.
        private bool ValidStack(SpriteRenderer[] images)
        {
            if (images == null || images.Length == 0) return true;
            bool valid = repeatImage != null && images.Length <= 2;
            foreach (var image in images) valid &= image != null && image.transform.parent == transform;
            return valid;
        }

        private static Vector3[] SaveLocalPositions(SpriteRenderer[] images)
        {
            if (images == null) return null;
            var saved = new Vector3[images.Length];
            for (int i = 0; i < images.Length; i++) saved[i] = images[i].transform.localPosition;
            return saved;
        }

        private void PlaceStack(SpriteRenderer[] images, float direction)
        {
            if (images == null || images.Length == 0) return;
            float height = primaryImage.bounds.size.y;
            for (int i = 0; i < images.Length; i++)
            {
                Transform source = i == 0 ? transform : repeatImage.transform;
                images[i].transform.position = source.position + Vector3.up * (height * direction);
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
            if (initialized) { RestoreStack(upperImages, initialUpperLocalPositions); RestoreStack(lowerImages, initialLowerLocalPositions); }
        }

        private static void RestoreStack(SpriteRenderer[] images, Vector3[] saved)
        {
            if (images == null || saved == null) return;
            for (int i = 0; i < images.Length && i < saved.Length; i++) images[i].transform.localPosition = saved[i];
        }
    }
}
