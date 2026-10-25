#if UNITY_EDITOR
using System.Collections.Generic;
using ActionPlatformer.Player;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class CombatPresentationTests
    {
        private readonly List<Object> created = new List<Object>();
        private UnitHealth MakeUnit(Vector2 point, UnitKind kind, int hp = 50, bool preserve = false)
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>(); created.Add(definition);
            var data = new SerializedObject(definition);
            data.FindProperty("unitId").stringValue = "presentation.test";
            data.FindProperty("displayName").stringValue = "Presentation test";
            data.FindProperty("kind").enumValueIndex = (int)kind;
            data.FindProperty("maxHealth").intValue = hp; data.FindProperty("attackPower").intValue = 10;
            data.ApplyModifiedPropertiesWithoutUndo();
            var go = new GameObject("Presentation test unit"); go.SetActive(false); created.Add(go);
            go.layer = 2; go.transform.position = point;
            var unit = go.AddComponent<Unit>();
            data = new SerializedObject(unit); data.FindProperty("definition").objectReferenceValue = definition; data.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<BoxCollider2D>(); var health = go.AddComponent<UnitHealth>();
            data = new SerializedObject(health); data.FindProperty("preserveHealthOnDamage").boolValue = preserve; data.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true); return health;
        }
        [TearDown] public void Cleanup()
        {
            for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }
        [Test] public void DamageFactsDistinguishTrainingDamageAndPublishBeforeLethalDeactivation()
        {
            var target = MakeUnit(Vector2.zero, UnitKind.Monster, 10);
            int notifications = 0; bool activeAtNotification = false; UnitDamageResult result = default;
            target.DamageApplied += value => { notifications++; result = value; activeAtNotification = target.gameObject.activeSelf; };
            Assert.That(target.ApplyDamage(0), Is.Zero); Assert.That(notifications, Is.Zero);
            Assert.That(target.ApplyDirectedDamage(4, Vector2.left), Is.EqualTo(4));
            Assert.That(result.HealthLost, Is.EqualTo(4)); Assert.That(result.Direction, Is.EqualTo(Vector2.left));
            Assert.That(target.ApplyDamage(10), Is.EqualTo(6)); Assert.That(result.HasDirection, Is.False);
            Assert.That(result.Killed && activeAtNotification, Is.True); Assert.That(target.gameObject.activeSelf, Is.False);
            Assert.That(target.ApplyDamage(10), Is.Zero); Assert.That(notifications, Is.EqualTo(2));
            var dummy = MakeUnit(Vector2.right * 4, UnitKind.Monster, 10, true);
            Assert.That(dummy.ApplyDamage(5), Is.EqualTo(5)); Assert.That(dummy.CurrentHealth, Is.EqualTo(10));
            Assert.That(dummy.LastDamage.HealthLost, Is.Zero); Assert.That(dummy.LastDamage.Killed, Is.False);
            dummy.SetDamageEnabled(false); uint version = dummy.DamageVersion;
            Assert.That(dummy.ApplyDamage(5), Is.Zero); Assert.That(dummy.DamageVersion, Is.EqualTo(version));
        }
        [Test] public void EachVictimHasOneContactAndLethalOutcomeWithoutChangingDamageOrStartTime()
        {
            var owner = MakeUnit(new Vector2(100, 100), UnitKind.Player);
            var first = MakeUnit(new Vector2(100.8f, 100), UnitKind.Monster, 5);
            var second = MakeUnit(new Vector2(101.1f, 100), UnitKind.Monster, 50);
            var tuning = AssetDatabase.LoadAssetAtPath<PlayerCombatTuning>("Assets/_Game/Data/PlayerCombatTuning.asset");
            var combat = new PlayerCombat(owner.Owner, owner.GetComponent<Collider2D>(), tuning, new SequentialComboRule(3, true));
            Physics2D.SyncTransforms();
            Assert.That(combat.TryAttack(first, 1, false, 10), Is.True);
            Assert.That(combat.GetPresentation(10).StartedAt, Is.EqualTo(10));
            Assert.That(combat.ConsumeHit(out _), Is.False, "Windup must not emit contact effects.");
            combat.Tick(10 + tuning.Windup + .00001);
            var hits = new List<PlayerHitPresentation>(); while (combat.ConsumeHit(out var hit)) hits.Add(hit);
            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(hits.Find(h => h.Target == first).Killed, Is.True);
            Assert.That(hits.Find(h => h.Target == first).Damage, Is.EqualTo(5));
            Assert.That(hits.Find(h => h.Target == second).Damage, Is.EqualTo(10));
            Assert.That(second.CurrentHealth, Is.EqualTo(40));
            combat.Tick(10 + tuning.Windup + .01); Assert.That(combat.ConsumeHit(out _), Is.False);
            combat.Reset(); Assert.That(combat.ConsumeHit(out _), Is.False);
        }
        [Test] public void CameraPrioritizesDamageThenKillCapsPixelsAndKeepsTimeFeedbackInReducedMotion()
        {
            var target = MakeUnit(Vector2.zero, UnitKind.Monster);
            var go = new GameObject("Presentation camera"); created.Add(go);
            var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 6.5f;
            var tuning = AssetDatabase.LoadAssetAtPath<PlayerCombatTuning>("Assets/_Game/Data/PlayerCombatTuning.asset");
            float time = Time.timeScale, step = Time.fixedDeltaTime;
            var feedback = new PlayerImpactFeedback(tuning, camera);
            try
            {
                Time.timeScale = 1;
                feedback.PlayDamage(Vector2.left, 10);
                feedback.PlayHit(new PlayerHitPresentation(1, PlayerAttack.Side, target, Vector2.zero, Vector2.right, 10, 10, true), 10);
                feedback.LateTick(10);
                Assert.That(camera.transform.position.x, Is.EqualTo(-4f * 13f / 660f).Within(.00001));
                Assert.That(camera.orthographicSize, Is.EqualTo(6.5f));
                feedback.Reset(); feedback.PlayHit(new PlayerHitPresentation(1, PlayerAttack.Side, target, Vector2.zero, Vector2.up, 10, 10, true), 11);
                feedback.Play(new PlayerImpact(Vector2.right, PlayerImpactStrength.Normal), 11);
                feedback.LateTick(11); Assert.That(camera.transform.position.y, Is.EqualTo(3f * 13f / 660f).Within(.00001));
                feedback.Reset(); feedback.PlayDamage(Vector2.zero, 12); feedback.LateTick(12);
                Assert.That(camera.transform.position, Is.EqualTo(Vector3.zero));
                feedback.Reset(); feedback.ReducedMotion = true;
                feedback.Play(new PlayerImpact(Vector2.right, PlayerImpactStrength.Normal), 13); feedback.LateTick(13);
                Assert.That(Time.timeScale, Is.Zero); Assert.That(camera.transform.position, Is.EqualTo(Vector3.zero));
                feedback.Tick(14); feedback.PlayDamage(Vector2.right, 15); feedback.LateTick(15);
                Assert.That(Time.timeScale, Is.EqualTo(tuning.DamageSlowTimeScale));
                Assert.That(camera.transform.position, Is.EqualTo(Vector3.zero));
            }
            finally { feedback.Dispose(); Time.timeScale = time; Time.fixedDeltaTime = step; }
        }
    }
}
#endif
