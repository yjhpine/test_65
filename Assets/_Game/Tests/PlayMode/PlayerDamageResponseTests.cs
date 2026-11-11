#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ActionPlatformer.Flow;
using ActionPlatformer.Player;
using ActionPlatformer.Presentation;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ActionPlatformer.Tests
{
    // What a hit does to the player: knockback away from the source, blinking invulnerability, a hurt sound, no tilt.
    public sealed class PlayerDamageResponseTests
    {
        private readonly List<Object> created = new List<Object>();
        private PlayerUnit player;
        private UnitHealth health;
        private SpriteRenderer sprite;
        private Transform visual;
        private PlayerCombatTuning tuning;
        private float timeScale, fixedStep;
        private sealed class IdleInput : IPlayerInputSource { public PlayerCommand Sample() => default; }

        [UnitySetUp] public IEnumerator Setup()
        {
            timeScale = Time.timeScale; fixedStep = Time.fixedDeltaTime;
            var floor = new GameObject("Damage response floor"); created.Add(floor);
            floor.transform.position = new Vector3(0, 80, 0);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(40, 1);
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab"),
                new Vector3(0, 81.32f, 0), Quaternion.identity); created.Add(go);
            player = go.GetComponent<PlayerUnit>(); player.SetInputSource(new IdleInput());
            health = go.GetComponent<UnitHealth>();
            visual = go.transform.Find("Visual"); sprite = visual.GetComponent<SpriteRenderer>();
            tuning = AssetDatabase.LoadAssetAtPath<PlayerCombatTuning>("Assets/_Game/Data/PlayerCombatTuning.asset");
            yield return null;
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.Motor.IsGrounded, Is.True);
        }

        [TearDown] public void Cleanup()
        {
            var manager = GameManager.Existing; if (manager != null && manager.IsPaused) manager.Resume();
            for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
            Time.timeScale = timeScale; Time.fixedDeltaTime = fixedStep;
        }

        private static IEnumerator Realtime(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        [UnityTest] public IEnumerator EnemyHitKnocksThePlayerAwayFromTheSourceWithASmallHop()
        {
            Vector2 start = player.Motor.Position;
            // The source is on the player's left, so the hit travels right.
            Assert.That(health.ApplyDirectedDamage(10, Vector2.right), Is.EqualTo(10));
            float highest = start.y, until = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < until) { highest = Mathf.Max(highest, player.Motor.Position.y); yield return null; }
            Assert.That(player.Motor.Position.x - start.x, Is.GreaterThan(.6f), "Pushed away from the source.");
            Assert.That(highest - start.y, Is.GreaterThan(.1f), "A small hop off the ground.");
            Assert.That(player.Motor.IsGrounded, Is.True, "Lands again.");
        }

        [UnityTest] public IEnumerator HitGrantsInvulnerabilityThatBlinksForAboutOnePointFiveSeconds()
        {
            Assert.That(health.ApplyDirectedDamage(10, Vector2.left), Is.EqualTo(10));
            float hitAt = Time.realtimeSinceStartup;
            Assert.That(health.ApplyDirectedDamage(10, Vector2.left), Is.Zero, "Enemy hits are ignored while invulnerable.");
            Assert.That(health.ApplyDamage(10, (Vector2)player.transform.position + Vector2.left), Is.Zero);
            Assert.That(health.CurrentHealth, Is.EqualTo(90));
            Assert.That(health.DamageVersion, Is.EqualTo(1));
            Assert.That(health.CanReceiveDamage, Is.True, "Enemies keep the player as their target.");
            yield return null;
            Assert.That(player.ReactionPresentation.Invulnerable, Is.True);
            bool dim = false, bright = false;
            float until = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                dim |= sprite.color.a < .5f; bright |= sprite.color.a > .95f;
            }
            Assert.That(dim && bright, Is.True, "The sprite blinks.");
            // The clock does not run while the hit holds time at a full stop (like a pause), so 1.5 s of moving time
            // follow that freeze.
            float frozen = tuning.DamageSlowTimeScale <= 0f ? tuning.DamageSlowDuration : 0f;
            yield return Realtime(frozen + 1.25f - (Time.realtimeSinceStartup - hitAt));
            Assert.That(player.ReactionPresentation.Invulnerable, Is.True, "Still protected at 1.25 s of moving time.");
            yield return Realtime(frozen + 1.65f - (Time.realtimeSinceStartup - hitAt));
            Assert.That(player.ReactionPresentation.Invulnerable, Is.False, "Protection ends after about 1.5 s of moving time.");
            Assert.That(sprite.color.a, Is.EqualTo(1f).Within(.001f));
            Assert.That(health.ApplyDirectedDamage(10, Vector2.left), Is.EqualTo(10));
        }

        [UnityTest] public IEnumerator InvulnerabilityClockStopsWhileThePauseHoldsTime()
        {
            Assert.That(health.ApplyDirectedDamage(10, Vector2.left), Is.EqualTo(10));
            // Let 0.3 s of the protection run first; the hit's own full stop (if any) does not count either.
            float frozen = tuning.DamageSlowTimeScale <= 0f ? tuning.DamageSlowDuration : 0f;
            yield return Realtime(frozen + .3f);
            var manager = GameManager.Instance;
            Assert.That(manager.TryPause(), Is.True);
            yield return Realtime(1.5f);
            Assert.That(player.ReactionPresentation.Invulnerable, Is.True, "Time spent paused does not count.");
            manager.Resume();
            yield return Realtime(1.45f);
            Assert.That(player.ReactionPresentation.Invulnerable, Is.False);
        }

        [UnityTest] public IEnumerator ContextFreeDamageNeitherKnocksBackNorIsBlocked()
        {
            Vector2 start = player.Motor.Position;
            Assert.That(health.ApplyDamage(10), Is.EqualTo(10));
            Assert.That(health.ApplyDamage(10), Is.EqualTo(10), "Context-free damage bypasses invulnerability, like the shield.");
            Assert.That(health.ApplyDirectedDamage(10, Vector2.right), Is.Zero, "It still grants the protection against enemies.");
            yield return Realtime(1f);
            Assert.That(player.Motor.Position.x, Is.EqualTo(start.x).Within(.02f), "No source, no knockback.");
        }

        [UnityTest] public IEnumerator HitPlaysTheHurtSoundOnceAndNeverTiltsThePlayer()
        {
            AudioSource[] Hurt() => EffectsRuntime.Sfx.GetComponentsInChildren<AudioSource>()
                .Where(s => s.clip != null && s.clip.name == "Hurt").ToArray();
            Assert.That(Hurt(), Is.Empty);
            Assert.That(health.ApplyDirectedDamage(10, Vector2.right), Is.EqualTo(10));
            health.ApplyDirectedDamage(10, Vector2.right);
            float until = Time.realtimeSinceStartup + .15f; float tilt = 0f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                tilt = Mathf.Max(tilt, Quaternion.Angle(visual.localRotation, Quaternion.identity));
            }
            Assert.That(Hurt().Length, Is.EqualTo(1), "One hurt sound for the hit that landed.");
            Assert.That(tilt, Is.LessThan(.01f), "The hit reaction does not tilt the player.");
        }
    }
}
#endif
