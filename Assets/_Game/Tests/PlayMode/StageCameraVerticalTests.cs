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
        private PlayerUnit player;
        private StageCameraFollow follow;
        private Rigidbody2D body;
        private float startRigY, startPlayerY;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("CoreLoopStage");
            yield return null;
            player = Object.FindFirstObjectByType<PlayerUnit>();
            follow = Object.FindFirstObjectByType<StageCameraFollow>();
            body = player.GetComponent<Rigidbody2D>();
            startRigY = follow.transform.position.y;
            startPlayerY = player.transform.position.y;
        }

        // Holds the player in the air at a height above the start, like a jump apex or a glitch chain.
        private IEnumerator HoldAirborne(float climb, float seconds)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                // The motor may restore its own body mode, so pin the player every frame.
                body.position = new Vector2(player.transform.position.x, startPlayerY + climb);
                player.transform.position = body.position;
                body.linearVelocity = Vector2.zero;
                yield return null;
            }
        }

        private IEnumerator StandOnLedge(float top)
        {
            var ledge = new GameObject("Camera test ledge");
            ledge.transform.position = new Vector3(player.transform.position.x, startPlayerY - .82f + top - .25f, 0f);
            ledge.AddComponent<BoxCollider2D>().size = new Vector2(6f, .5f);
            body.bodyType = RigidbodyType2D.Dynamic;
            player.Motor.Teleport(new Vector2(player.transform.position.x, startPlayerY + top + .3f));
            Physics2D.SyncTransforms();
            float deadline = Time.time + 2f;
            while (!player.Motor.IsGrounded && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(player.Motor.IsGrounded, Is.True, "the player lands on the test ledge");
            yield return new WaitForSeconds(1.2f);
        }

        [UnityTest]
        public IEnumerator CameraFollowsClimbUpToFortyAndBackgroundCoversTheView()
        {
            var camera = follow.GetComponentInChildren<Camera>();

            // A normal jump height must not move the camera.
            yield return HoldAirborne(4f, .7f);
            Assert.That(follow.transform.position.y, Is.EqualTo(startRigY).Within(0.01f));

            // Far above the limit: the climb is capped at 40 above the start height, framed like the start floor.
            yield return HoldAirborne(60f, 2.5f);
            Assert.That(follow.transform.position.y - startRigY, Is.EqualTo(40f).Within(.05f));

            // Every layer's stacked images must cover the visible top and bottom edges.
            // Layers follow in the camera's render callback, which batch runs never reach, so apply that step directly.
            var apply = typeof(ParallaxLayer2D).GetMethod("ApplyCameraPosition",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (var layer in Object.FindObjectsByType<ParallaxLayer2D>(FindObjectsSortMode.None)) apply.Invoke(layer, null);
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

        // Past the dead zone the camera gains on the player until the headroom matches the start floor.
        [UnityTest]
        public IEnumerator AirborneClimbPastTheDeadZoneCatchesUpToTheStandingFraming()
        {
            yield return HoldAirborne(6f, 1.5f);
            float partway = follow.transform.position.y - startRigY;
            Assert.That(partway, Is.GreaterThan(1.5f + .5f), "faster than the old dead-zone lag (1.5)");
            Assert.That(partway, Is.LessThan(6f - .5f), "not yet fully caught up");
            yield return HoldAirborne(12f, 1.5f);
            Assert.That(follow.transform.position.y - startRigY, Is.EqualTo(12f).Within(.05f));
        }

        // Ground above the dead zone is framed like the start floor; low ledges and jumps keep the view still.
        [UnityTest]
        public IEnumerator HighGroundIsFramedLikeTheStartFloorAndJumpsThereKeepTheView()
        {
            yield return StandOnLedge(3f);
            Assert.That(follow.transform.position.y, Is.EqualTo(startRigY).Within(.05f), "a low ledge keeps the start framing");

            yield return StandOnLedge(7f);
            float climb = player.transform.position.y - startPlayerY;
            Assert.That(follow.transform.position.y - startRigY, Is.EqualTo(climb).Within(.05f), "standing high is framed like the floor");

            yield return HoldAirborne(climb + 4f, .8f);
            Assert.That(follow.transform.position.y - startRigY, Is.EqualTo(climb).Within(.05f), "a jump from the ledge does not move the view");
        }
    }
}
