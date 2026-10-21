#if UNITY_EDITOR
using System.Collections;
using ActionPlatformer.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    public sealed class ParallaxLayerTests
    {
        private GameObject root;
        private Camera camera;
        private RenderTexture target;
        private Texture2D beltTexture;
        private Sprite beltSprite;
        private float savedTimeScale;
        private readonly Vector3 cameraOrigin = new Vector3(4f, 3f, -10f);

        [SetUp] public void Setup()
        {
            savedTimeScale = Time.timeScale;
            root = new GameObject("Parallax test");
            var cameraObject = new GameObject("Parallax camera");
            cameraObject.transform.SetParent(root.transform);
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = cameraOrigin;
            camera.cullingMask = 0;
            target = new RenderTexture(64, 64, 24);
            target.Create();
            camera.targetTexture = target;
        }

        [TearDown] public void Cleanup()
        {
            Time.timeScale = savedTimeScale;
            Object.DestroyImmediate(root);
            target.Release();
            Object.DestroyImmediate(target);
            if (beltSprite != null) Object.DestroyImmediate(beltSprite);
            if (beltTexture != null) Object.DestroyImmediate(beltTexture);
        }

        private ParallaxLayer2D Layer(float speed, bool repeat = false)
        {
            var go = new GameObject("Background " + speed);
            go.SetActive(false);
            go.transform.SetParent(root.transform);
            go.transform.localPosition = new Vector3(20, 5, 2);
            var layer = go.AddComponent<ParallaxLayer2D>();
            var data = new SerializedObject(layer);
            data.FindProperty("viewCamera").objectReferenceValue = camera;
            data.FindProperty("scrollSpeed").floatValue = speed;
            if (repeat)
            {
                if (beltSprite == null)
                {
                    beltTexture = new Texture2D(32, 16);
                    beltSprite = Sprite.Create(beltTexture, new Rect(0, 0, 32, 16), new Vector2(.5f, .5f), 1, 0, SpriteMeshType.FullRect);
                }
                var primary = go.AddComponent<SpriteRenderer>();
                primary.sprite = beltSprite;
                var copy = new GameObject("Repeat").AddComponent<SpriteRenderer>();
                copy.transform.SetParent(go.transform, false);
                copy.transform.localPosition = Vector3.right * 32;
                copy.sprite = beltSprite;
                copy.flipX = true;
                data.FindProperty("repeatImage").objectReferenceValue = copy;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
            return layer;
        }

        private static IEnumerator RenderFrame(Camera view)
        {
            bool rendered = false;
            void OnRendered(ScriptableRenderContext context, Camera renderedCamera)
            {
                if (renderedCamera == view) rendered = true;
            }
            RenderPipelineManager.endCameraRendering += OnRendered;
            try
            {
                float deadline = Time.realtimeSinceStartup + 5f;
                while (!rendered && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(rendered, Is.True, "Expected the URP camera to render.");
            }
            finally { RenderPipelineManager.endCameraRendering -= OnRendered; }
        }

        private static void PositionIs(Transform layer, Vector3 expected)
        {
            Assert.That(Vector3.Distance(layer.position, expected), Is.LessThan(.0001f));
        }

        [UnityTest] public IEnumerator LayersScrollAtDifferentSpeedsWhileTimeIsPaused()
        {
            var locked = Layer(0);
            var far = Layer(.2f);
            var middle = Layer(.5f);
            var fixedWorld = Layer(1);
            Time.timeScale = 0f;
            camera.transform.position += new Vector3(10, 2, 7);
            yield return RenderFrame(camera);
            PositionIs(locked.transform, new Vector3(30, 7, 2));
            PositionIs(far.transform, new Vector3(28, 6.6f, 2));
            PositionIs(middle.transform, new Vector3(25, 6, 2));
            PositionIs(fixedWorld.transform, new Vector3(20, 5, 2));
        }

        [UnityTest] public IEnumerator CameraReturnZoomAndRepeatedRendersDoNotAccumulateDrift()
        {
            var layer = Layer(.2f);
            camera.transform.position = cameraOrigin + new Vector3(100, -4, 0);
            yield return RenderFrame(camera);
            PositionIs(layer.transform, new Vector3(100, 1.8f, 2));
            camera.orthographicSize = 3;
            yield return RenderFrame(camera);
            PositionIs(layer.transform, new Vector3(100, 1.8f, 2));
            camera.transform.position = cameraOrigin;
            yield return RenderFrame(camera);
            yield return RenderFrame(camera);
            PositionIs(layer.transform, new Vector3(20, 5, 2));
        }

        [UnityTest] public IEnumerator DisableRestoresAuthoringPoseAndReenableKeepsOriginalCameraAnchor()
        {
            var layer = Layer(.5f);
            camera.transform.position += Vector3.right * 10;
            yield return RenderFrame(camera);
            PositionIs(layer.transform, new Vector3(25, 5, 2));
            for (int i = 0; i < 3; i++)
            {
                layer.enabled = false;
                PositionIs(layer.transform, new Vector3(20, 5, 2));
                yield return RenderFrame(camera);
                PositionIs(layer.transform, new Vector3(20, 5, 2));
                layer.enabled = true;
                yield return RenderFrame(camera);
                PositionIs(layer.transform, new Vector3(25, 5, 2));
            }
        }

        [UnityTest] public IEnumerator OtherCamerasCannotMoveTheLayer()
        {
            var layer = Layer(.5f);
            camera.enabled = false;
            camera.transform.position += Vector3.right * 10;
            var other = new GameObject("Unrelated camera").AddComponent<Camera>();
            other.transform.SetParent(root.transform);
            other.orthographic = true;
            other.targetTexture = target;
            other.cullingMask = 0;
            other.transform.position = new Vector3(-50, 80, -10);
            yield return RenderFrame(other);
            PositionIs(layer.transform, new Vector3(20, 5, 2));
            camera.enabled = true;
            yield return RenderFrame(camera);
            PositionIs(layer.transform, new Vector3(25, 5, 2));
        }

        [UnityTest] public IEnumerator LateCameraMotionIsAppliedBeforeTheSameFrameRenders()
        {
            var layer = Layer(.5f);
            var mover = camera.gameObject.AddComponent<ParallaxLateCameraTestDriver>();
            mover.Position = cameraOrigin + Vector3.right * 12;
            yield return RenderFrame(camera);
            PositionIs(layer.transform, new Vector3(26, 5, 2));
        }

        private void BeltCoversCamera(ParallaxLayer2D layer)
        {
            var images = layer.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(images.Length, Is.EqualTo(2), "The belt must reuse exactly two images.");
            var a = images[0].bounds;
            var b = images[1].bounds;
            if (a.min.x > b.min.x) { var swap = a; a = b; b = swap; }
            float halfWidth = camera.orthographicSize * camera.aspect;
            Assert.That(a.min.x, Is.LessThanOrEqualTo(camera.transform.position.x - halfWidth + .002f));
            Assert.That(b.max.x, Is.GreaterThanOrEqualTo(camera.transform.position.x + halfWidth - .002f));
            Assert.That(a.max.x, Is.EqualTo(b.min.x).Within(.002f), "Image edges must meet without a gap or overlap.");
            Assert.That(images[0].flipX, Is.False);
            Assert.That(images[1].flipX, Is.True);
        }

        [UnityTest] public IEnumerator TwoImagesCoverBothDirectionsAndMultiWidthTeleportsWhilePaused()
        {
            Time.timeScale = 0;
            foreach (float speed in new[] { 0f, .1f, .25f, 1f })
            {
                camera.transform.position = cameraOrigin;
                var layer = Layer(speed, true);
                foreach (float x in new[] { 4f, 43f, 110f, -60f, -180f, 10004f, -10004f, 4f })
                {
                    camera.transform.position = new Vector3(x, 3, -10);
                    yield return RenderFrame(camera);
                    BeltCoversCamera(layer);
                }
                Object.DestroyImmediate(layer.gameObject);
            }
        }

        [UnityTest] public IEnumerator OnlyTheOffscreenImageRecyclesAndVisibleImageDoesNotJump()
        {
            var layer = Layer(1, true);
            var primary = layer.GetComponent<SpriteRenderer>();
            var copy = layer.transform.Find("Repeat");
            camera.transform.position = new Vector3(22, 3, -10);
            yield return RenderFrame(camera);
            Vector3 primaryPosition = primary.transform.position;
            Vector3 copyPosition = copy.position;
            camera.transform.position = new Vector3(43, 3, -10);
            yield return RenderFrame(camera);
            PositionIs(copy, copyPosition);
            PositionIs(primary.transform, primaryPosition + Vector3.right * 64);
            BeltCoversCamera(layer);
            camera.transform.position = new Vector3(22, 3, -10);
            yield return RenderFrame(camera);
            PositionIs(copy, copyPosition);
            PositionIs(primary.transform, primaryPosition);
        }

        [UnityTest] public IEnumerator BeltPreservesParallaxRateAndRestoresBothImagesOnDisable()
        {
            var layer = Layer(.25f, true);
            yield return RenderFrame(camera);
            Vector3 first = layer.transform.position;
            camera.transform.position += Vector3.right * 10;
            yield return RenderFrame(camera);
            PositionIs(layer.transform, first + Vector3.right * 7.5f);
            camera.transform.position += Vector3.right * 5000;
            yield return RenderFrame(camera);
            BeltCoversCamera(layer);
            layer.enabled = false;
            PositionIs(layer.transform, new Vector3(20, 5, 2));
            Assert.That(layer.transform.Find("Repeat").localPosition, Is.EqualTo(Vector3.right * 32));
            yield return RenderFrame(camera);
            PositionIs(layer.transform, new Vector3(20, 5, 2));
            layer.enabled = true;
            yield return RenderFrame(camera);
            BeltCoversCamera(layer);
        }

        [UnityTest] public IEnumerator TiledAndScaledImagesUseWorldWidthAfterZoomChanges()
        {
            var layer = Layer(.1f, true);
            foreach (var image in layer.GetComponentsInChildren<SpriteRenderer>())
            {
                image.drawMode = SpriteDrawMode.Tiled;
                image.size = new Vector2(48, 16);
            }
            layer.transform.localScale = new Vector3(1.25f, 1, 1);
            camera.orthographicSize = 15;
            camera.aspect = 1.5f; // View width 45, each belt image width 60.
            foreach (float x in new[] { -2000f, 2000f, 4f })
            {
                camera.transform.position = new Vector3(x, 3, -10);
                yield return RenderFrame(camera);
                BeltCoversCamera(layer);
            }
        }
    }

    public sealed class ParallaxLateCameraTestDriver : MonoBehaviour
    {
        public Vector3 Position;
        private void LateUpdate() { transform.position = Position; }
    }
}
#endif
