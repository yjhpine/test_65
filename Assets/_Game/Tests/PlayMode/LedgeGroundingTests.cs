using System.Collections;
using ActionPlatformer.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    /// <summary>The thin foot box is the only support: a body whose feet are not over a platform falls at once.</summary>
    public sealed class LedgeGroundingTests
    {
        private GameObject platform;
        private GameObject player;
        private PlayerTuning tuning;

        [TearDown]
        public void Cleanup()
        {
            if (platform != null) Object.Destroy(platform);
            if (player != null) Object.Destroy(player);
            if (tuning != null) Object.Destroy(tuning);
        }

        // One-way platform, top at y = 0, right edge at x = 0.
        private void BuildPlatform()
        {
            platform = new GameObject("OneWay");
            platform.transform.position = new Vector3(-10f, -0.25f, 0f);
            var slab = platform.AddComponent<BoxCollider2D>();
            slab.size = new Vector2(20f, 0.5f);
            slab.usedByEffector = true;
            platform.AddComponent<PlatformEffector2D>();
        }

        // Mirrors Player.prefab: the capsule body plus a thin flat foot box inside its bounds that meets terrain only.
        private CharacterMotor2D BuildPlayer(float x, float y, out Rigidbody2D body)
        {
            player = new GameObject("Player");
            player.transform.position = new Vector3(x, y, 0f);
            body = player.AddComponent<Rigidbody2D>();
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.None;
            var material = new PhysicsMaterial2D { friction = 0f, bounciness = 0f };
            var capsule = player.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.65f, 1.6f);
            capsule.sharedMaterial = material;
            var feet = player.AddComponent<BoxCollider2D>();
            feet.size = new Vector2(0.3f, 0.1f);
            feet.offset = new Vector2(0f, -0.75f);
            feet.sharedMaterial = material;
            feet.excludeLayers = ~1;
            Physics2D.SyncTransforms();
            tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            return new CharacterMotor2D(body, capsule, tuning, 1);
        }

        private IEnumerator Run(CharacterMotor2D motor, float seconds, float horizontal)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                motor.RefreshContacts();
                motor.Simulate(horizontal, false, Time.fixedDeltaTime);
            }
        }

        [UnityTest]
        public IEnumerator LandingWithTheFeetPastTheEdgeFallsOffImmediately()
        {
            BuildPlatform();
            // Foot box spans x 0.15..0.45, entirely past the edge; only the rounded capsule can touch the corner.
            var motor = BuildPlayer(0.3f, 2f, out var body);
            float until = Time.time + 1f;
            float lastY = body.position.y;
            float slowestFall = float.MaxValue;
            while (Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                motor.RefreshContacts();
                motor.Simulate(0f, false, Time.fixedDeltaTime);
                Assert.That(motor.IsGrounded, Is.False, "an unsupported body must not count as grounded");
                if (body.position.y < 0.9f && body.position.y > 0f)
                    slowestFall = Mathf.Min(slowestFall, -motor.Velocity.y);
                lastY = body.position.y;
            }
            Assert.That(lastY, Is.LessThan(-3f), "the body did not drop off the platform");
            Assert.That(slowestFall, Is.GreaterThan(2.5f), "it crept down the corner instead of falling");
        }

        [UnityTest]
        public IEnumerator FeetOverThePlatformStandAndWalkOffWithoutLosingSpeed()
        {
            BuildPlatform();
            var motor = BuildPlayer(-0.05f, 0.81f, out var body);
            yield return Run(motor, 0.4f, 0f);
            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(body.position.y, Is.EqualTo(0.8f).Within(0.03f));

            float previous = 0f;
            float until = Time.time + 0.5f;
            while (Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                motor.RefreshContacts();
                motor.Simulate(-1f, false, Time.fixedDeltaTime);
                float speed = Mathf.Abs(motor.Velocity.x);
                Assert.That(speed, Is.GreaterThanOrEqualTo(previous - 0.01f), "speed dropped while walking");
                previous = speed;
            }
            Assert.That(previous, Is.EqualTo(tuning.RunSpeed).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator FeetSunkIntoAOneWayPlatformAreNotGroundedAndFallThrough()
        {
            BuildPlatform();
            // Platform spans y -0.5..0; the feet start 0.3 inside it, mid-body over its interior (as after a jump from below).
            var motor = BuildPlayer(-5f, 0.5f, out var body);
            motor.RefreshContacts();
            Assert.That(motor.IsGrounded, Is.False, "feet inside the platform must not count as standing");
            float until = Time.time + 0.8f;
            float lastFootY = 0f;
            while (Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                motor.RefreshContacts();
                motor.Simulate(0f, false, Time.fixedDeltaTime);
                lastFootY = body.position.y - 0.8f;
            }
            Assert.That(lastFootY, Is.LessThan(-1.5f), "the body stayed stuck in the platform instead of falling through");
        }
    }
}
