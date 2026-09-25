#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ActionPlatformer.Player;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    public sealed class TrainingDummyTests
    {
        private readonly List<Object> created = new List<Object>();
        private GameObject dummy;
        private PlayerUnit player;
        private UnitHealth health;
        private static readonly WaitForFixedUpdate Step = new WaitForFixedUpdate();
        private sealed class IdleInput : IPlayerInputSource { public PlayerCommand Sample() => default; }

        [UnitySetUp] public IEnumerator Setup()
        {
            var floor = new GameObject("Dummy test floor"); created.Add(floor);
            floor.transform.position = new Vector3(0, 80, 0);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(20, 1);
            dummy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/TrainingDummy.prefab"),
                new Vector3(0, 81.42f, 0), Quaternion.identity); created.Add(dummy);
            health = dummy.GetComponent<UnitHealth>();
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab"),
                new Vector3(-3, 81.32f, 0), Quaternion.identity); created.Add(go);
            player = go.GetComponent<PlayerUnit>(); player.SetInputSource(new IdleInput());
            yield return null;
            for (int i = 0; i < 5; i++) yield return Step;
        }

        [TearDown] public void Cleanup()
        {
            for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [UnityTest] public IEnumerator PassivePrefabAcceptsRepeatedLethalHitsWithoutDying()
        {
            var unit = dummy.GetComponent<Unit>();
            Assert.That(unit.Definition.UnitId, Is.EqualTo("enemy.training-dummy"));
            Assert.That(unit.Definition.AttackPower, Is.Zero);
            Assert.That(unit.Fsm, Is.Null);
            Assert.That(dummy.GetComponent<UnitCombat2D>(), Is.Null);
            Assert.That(unit.Definition.AllowGlitchTarget && unit.Definition.AllowForcedMovement, Is.True);
            foreach (var transform in dummy.GetComponentsInChildren<Transform>())
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero);
            Vector2 position = dummy.transform.position;
            for (int i = 0; i < 30; i++) yield return Step;
            Assert.That(Vector2.Distance(position, dummy.transform.position), Is.LessThan(0.05f));
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) yield return Capture();
            for (int i = 0; i < 20; i++) Assert.That(health.ApplyDamage(int.MaxValue), Is.EqualTo(health.MaxHealth));
            Assert.That(health.DamageVersion, Is.EqualTo(20));
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
            Assert.That(health.CanReceiveDamage, Is.True);
            health.SetDamageEnabled(false); Assert.That(health.ApplyDamage(10), Is.Zero);
            health.SetDamageEnabled(true);
            dummy.SetActive(false); Assert.That(health.ApplyDamage(10), Is.Zero);
            dummy.SetActive(true); Assert.That(health.ApplyDamage(10), Is.EqualTo(10));
        }

        [UnityTest] public IEnumerator GlitchAndThreeHitComboTriggerFeedbackAndKnockback()
        {
            var request = new GlitchRequest { Target = health, Direction = GlitchDirection.Left };
            Assert.That(player.Glitch.Select((Vector2)dummy.transform.position + Vector2.left * 0.3f).Target, Is.SameAs(health));
            Assert.That(player.Glitch.Preview(request, Time.timeAsDouble).CanExecute, Is.True);
            Assert.That(player.Glitch.TryExecute(request, Time.timeAsDouble), Is.True);
            var torso = dummy.transform.Find("Visual/Body").GetComponent<SpriteRenderer>();
            Color initial = torso.color;
            for (uint strike = 1; strike <= 3; strike++)
            {
                Assert.That(player.Combat.TryAttack(health, 1, false, Time.timeAsDouble), Is.True);
                float deadline = Time.realtimeSinceStartup + 2f;
                while (health.DamageVersion < strike && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(health.DamageVersion, Is.EqualTo(strike));
                yield return null;
                Assert.That(torso.color, Is.Not.EqualTo(initial));
                if (strike < 3)
                    while (player.Combat.IsAttacking && Time.realtimeSinceStartup < deadline) yield return null;
            }
            Assert.That(dummy.GetComponent<GroundMovement2D>().IsForcedMoving, Is.True);
            Assert.That(dummy.GetComponent<Rigidbody2D>().linearVelocity.x, Is.GreaterThan(0));
            Assert.That(health.CurrentHealth, Is.EqualTo(100));
            dummy.SetActive(false);
            Assert.That(torso.color, Is.EqualTo(initial));
        }

        [UnityTest] public IEnumerator UndergroundEmergenceCanLaunchTheDummy()
        {
            Assert.That(player.Glitch.TryExecute(new GlitchRequest { Target = health, Direction = GlitchDirection.Down }, Time.timeAsDouble), Is.True);
            Assert.That(player.Glitch.IsUnderground, Is.True);
            Assert.That(player.Glitch.TryEmerge(), Is.True);
            Assert.That(player.Combat.TryAttack(health, -1, true, Time.timeAsDouble), Is.True);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (health.DamageVersion == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(health.DamageVersion, Is.EqualTo(1));
            Assert.That(dummy.GetComponent<Rigidbody2D>().linearVelocity.y, Is.GreaterThan(0));
            Assert.That(player.Motor.Velocity.y, Is.GreaterThan(0));
            Assert.That(health.CurrentHealth, Is.EqualTo(100));
        }

        [UnityTest] public IEnumerator SlamHitsDummyOnceAcrossDescentAndLandingShockwave()
        {
            Assert.That(player.Glitch.TryExecute(new GlitchRequest { Target = health, Direction = GlitchDirection.Up }, Time.timeAsDouble), Is.True);
            player.Combat.ObserveArrival(true);
            Assert.That(player.Combat.TryAttack(health, 1, false, Time.timeAsDouble, false), Is.True);
            Assert.That(player.Combat.Attack, Is.EqualTo(PlayerAttack.Slam));
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!player.Shockwave.Visible && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(health.DamageVersion, Is.EqualTo(1));
            Assert.That(player.Shockwave.Visible, Is.True);
            Assert.That(player.Motor.IsGrounded, Is.True);
            yield return Step;
            TestContext.WriteLine($"grounded slam hit={health.DamageVersion}, health={health.CurrentHealth}, velocity={dummy.GetComponent<Rigidbody2D>().linearVelocity}");
        }

        private IEnumerator Capture()
        {
            var go = new GameObject("Dummy test camera"); created.Add(go);
            var camera = go.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1.5f;
            camera.transform.position = new Vector3(0, 81.3f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.07f, 0.09f, 0.14f);
            var target = new RenderTexture(600, 450, 24); var image = new Texture2D(600, 450, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            yield return null; yield return null;
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 600, 450), 0, 0); image.Apply();
                string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Library/PrototypeValidation"));
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, "TrainingDummy.png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                target.Release(); Object.Destroy(target); Object.Destroy(image);
            }
        }
    }
}
#endif
