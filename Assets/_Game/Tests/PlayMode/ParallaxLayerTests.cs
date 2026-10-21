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
        }

        private ParallaxLayer2D Layer(float speed)
        {
            var go = new GameObject("Background " + speed);
            go.SetActive(false);
            go.transform.SetParent(root.transform);
            go.transform.localPosition = new Vector3(20, 5, 2);
            var layer = go.AddComponent<ParallaxLayer2D>();
            var data = new SerializedObject(layer);
            data.FindProperty("viewCamera").objectReferenceValue = camera;
            data.FindProperty("scrollSpeed").floatValue = speed;
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
    }

    public sealed class ParallaxLateCameraTestDriver : MonoBehaviour
    {
        public Vector3 Position;
        private void LateUpdate() { transform.position = Position; }
    }
}
#endif
