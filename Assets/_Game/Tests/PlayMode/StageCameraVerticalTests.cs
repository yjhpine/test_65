using System.Collections;
using ActionPlatformer.Environment;
using ActionPlatformer.Flow;
using ActionPlatformer.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    public sealed class StageCameraVerticalTests
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("CoreLoopStage");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraFollowsClimbUpToTwentyAndBackgroundCoversTheView()
        {
            var player = Object.FindFirstObjectByType<PlayerUnit>();
            var follow = Object.FindFirstObjectByType<StageCameraFollow>();
            var camera = follow.GetComponentInChildren<Camera>();
            float startRigY = follow.transform.position.y;
            float startPlayerY = player.transform.position.y;

            var body = player.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;

            // A normal jump height must not move the camera.
            for (int i = 0; i < 40; i++)
            {
                body.position = new Vector2(player.transform.position.x, startPlayerY + 4f);
                player.transform.position = body.position;
                body.linearVelocity = Vector2.zero;
                yield return null;
            }
            Assert.That(follow.transform.position.y, Is.EqualTo(startRigY).Within(0.01f));

            // Far above the limit: the climb is capped at 20 above the start height.
            float until = Time.time + 2.5f;
            while (Time.time < until)
            {
                // The motor may restore its own body mode, so pin the player every frame.
                body.position = new Vector2(player.transform.position.x, startPlayerY + 60f);
                player.transform.position = body.position;
                body.linearVelocity = Vector2.zero;
                yield return null;
            }
            float cappedRise = follow.transform.position.y - startRigY;
            Assert.That(cappedRise, Is.GreaterThan(10f).And.LessThanOrEqualTo(20.01f));

            // Every layer's stacked images must cover the visible top and bottom edges.
            float top = camera.transform.position.y + camera.orthographicSize;
            float bottom = camera.transform.position.y - camera.orthographicSize;
            foreach (var layer in Object.FindObjectsByType<ParallaxLayer2D>(FindObjectsSortMode.None))
            {
                float min = float.MaxValue, max = float.MinValue;
                foreach (var image in layer.GetComponentsInChildren<SpriteRenderer>())
                {
                    min = Mathf.Min(min, image.bounds.min.y);
                    max = Mathf.Max(max, image.bounds.max.y);
                }
                Assert.That(max, Is.GreaterThanOrEqualTo(top), layer.name);
                Assert.That(min, Is.LessThanOrEqualTo(bottom), layer.name);
            }
        }
    }
}
