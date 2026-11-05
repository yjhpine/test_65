using ActionPlatformer.Units;
using ActionPlatformer.Units.Bosses;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class BossDefinitionTests
    {
        private const string Root = "Assets/_Game/";
        private const string PrefabPath = Root + "Prefabs/Bosses/TestBoss.prefab";
        private const string FsmPath = Root + "Data/Bosses/TestBoss/TestBossFsm.asset";
        private const string DefinitionPath = Root + "Data/Bosses/TestBoss/TestBossDefinition.asset";
        private const string ControllerPath = Root + "Animations/Bosses/TestBoss/TestBoss.controller";
        private const string PatternFolder = Root + "Data/Bosses/TestBoss/Patterns/";

        [Test] public void TestBossPrefabIsWiredAndValid()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            Assert.AreEqual(2, prefab.layer);
            var unit = prefab.GetComponent<Unit>();
            Assert.IsNotNull(unit);
            Assert.IsTrue(unit.Definition.TryValidate(out string error), error);
            Assert.AreEqual(UnitKind.Monster, unit.Definition.Kind);
            Assert.AreEqual(300, unit.Definition.MaxHealth);
            Assert.IsFalse(unit.Definition.AllowForcedMovement);
            Assert.IsTrue(unit.Definition.AllowGlitchTarget);
            var fsm = unit.Definition.Fsm as BossFsmDefinition;
            Assert.IsNotNull(fsm);
            Assert.IsTrue(fsm.TryValidate(out error), error);
            Assert.IsTrue(fsm.Selector.TryValidate(out error), error);
            Assert.IsNotNull(prefab.GetComponent<UnitHealth>());
            Assert.IsNotNull(prefab.GetComponent<BossCombat2D>());
            Assert.IsNotNull(prefab.GetComponent<GroundMovement2D>());
            Assert.IsNotNull(prefab.GetComponent<Rigidbody2D>());
            Assert.IsNotNull(prefab.GetComponent<BoxCollider2D>());
            var visual = prefab.GetComponentInChildren<BossVisual2D>();
            Assert.IsNotNull(visual);
            Assert.IsFalse(visual.GetComponent<Animator>().applyRootMotion);
            Assert.IsNotNull(visual.GetComponent<UnitDamageFlash>());
            Assert.IsTrue(visual.GetComponent<SpriteRenderer>().sharedMaterial.HasProperty("_HitFlash"), "UnitDamageFlash needs a material with _HitFlash.");
            // The sprite is centered, so the Visual sits on the root and the drawn box covers the collider.
            Assert.AreEqual(Vector3.zero, visual.transform.localPosition);
            var drawn = visual.GetComponent<SpriteRenderer>().bounds; var body = prefab.GetComponent<BoxCollider2D>();
            Assert.AreEqual(body.size.x, drawn.size.x, .01f); Assert.AreEqual(body.size.y, drawn.size.y, .01f);
            Assert.AreEqual(prefab.transform.position.x + body.offset.x, drawn.center.x, .01f);
            Assert.AreEqual(prefab.transform.position.y + body.offset.y, drawn.center.y, .01f);
            var so = new SerializedObject(visual);
            // The telegraph is the shared danger zone drawn by VfxManager; the old child renderer is gone.
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Object>("Assets/_Game/Data/Presentation/AttackZone.asset"), so.FindProperty("attackZone").objectReferenceValue);
            Assert.IsNull(prefab.transform.Find("Telegraph"));
            Assert.AreSame(prefab.GetComponent<BossCombat2D>(), so.FindProperty("stateSource").objectReferenceValue);
        }

        [Test] public void TestBossAnimatorHasEveryReferencedState()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller, ControllerPath);
            var states = new HashSet<string>();
            foreach (var s in controller.layers[0].stateMachine.states) states.Add(s.state.name);
            var fsm = AssetDatabase.LoadAssetAtPath<BossFsmDefinition>(FsmPath);
            Assert.IsNotNull(fsm, FsmPath);
            var names = new List<string> { fsm.IdleAnimation, fsm.MoveAnimation, fsm.StaggerAnimation, fsm.PhaseShiftAnimation, fsm.DeathAnimation };
            foreach (var file in new[] { "TestBossMelee", "TestBossCharge", "TestBossBackstep", "TestBossLeapSlam", "TestBossSummon" })
            {
                var pattern = AssetDatabase.LoadAssetAtPath<BossPatternDefinition>(PatternFolder + file + ".asset");
                Assert.IsNotNull(pattern, file);
                foreach (var n in new[] { pattern.TelegraphAnimation, pattern.ExecuteAnimation, pattern.RecoverAnimation })
                    if (!string.IsNullOrEmpty(n)) names.Add(n);
            }
            foreach (var n in names) Assert.IsTrue(states.Contains(n), "Missing animator state " + n);
            Assert.IsTrue(System.Array.Exists(controller.parameters, p => p.name == "ActionTime" && p.type == AnimatorControllerParameterType.Float));
        }

        [TestCase(new[] { .3f, .6f })]
        [TestCase(new[] { 1f })]
        [TestCase(new[] { 0f })]
        public void FsmValidationRejectsBadThresholds(float[] thresholds)
        {
            var copy = Copy();
            try
            {
                var so = new SerializedObject(copy); var p = so.FindProperty("phaseThresholds");
                p.arraySize = thresholds.Length;
                for (int i = 0; i < thresholds.Length; i++) p.GetArrayElementAtIndex(i).floatValue = thresholds[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsFalse(copy.TryValidate(out _));
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test] public void FsmValidationRejectsBadScalarsAndMissingSelector()
        {
            Assert.IsTrue(AssetDatabase.LoadAssetAtPath<BossFsmDefinition>(FsmPath).TryValidate(out _));
            foreach (var (field, value) in new (string, float)[] { ("decisionInterval", 0f), ("staggerDuration", float.NaN) })
            {
                var copy = Copy();
                try
                {
                    var so = new SerializedObject(copy); so.FindProperty(field).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo();
                    Assert.IsFalse(copy.TryValidate(out _), field);
                }
                finally { Object.DestroyImmediate(copy); }
            }
            var noSelector = Copy();
            try
            {
                var so = new SerializedObject(noSelector); so.FindProperty("selector").objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsFalse(noSelector.TryValidate(out _));
            }
            finally { Object.DestroyImmediate(noSelector); }
        }

        [Test] public void TestBossAssetsHaveTheDocumentedValues()
        {
            var fsm = AssetDatabase.LoadAssetAtPath<BossFsmDefinition>(FsmPath);
            Assert.AreEqual(new[] { .5f }, new List<float>(fsm.PhaseThresholds).ToArray());
            var melee = AssetDatabase.LoadAssetAtPath<BossMeleeAreaPattern>(PatternFolder + "TestBossMelee.asset");
            var charge = AssetDatabase.LoadAssetAtPath<BossChargePattern>(PatternFolder + "TestBossCharge.asset");
            var back = AssetDatabase.LoadAssetAtPath<BossRepositionPattern>(PatternFolder + "TestBossBackstep.asset");
            Assert.AreEqual(0f, melee.Cooldown); Assert.AreEqual(3f, charge.Cooldown); Assert.AreEqual(4f, back.Cooldown);
            Assert.AreEqual("MeleeTelegraph", melee.TelegraphAnimation); Assert.AreEqual("MeleeExecute", melee.ExecuteAnimation); Assert.AreEqual("Recover", melee.RecoverAnimation);
            Assert.AreEqual("ChargeTelegraph", charge.TelegraphAnimation); Assert.AreEqual("ChargeExecute", charge.ExecuteAnimation);
            Assert.AreEqual("Backstep", back.ExecuteAnimation);
            Assert.AreSame(fsm, AssetDatabase.LoadAssetAtPath<UnitDefinition>(DefinitionPath).Fsm);

            var leap = AssetDatabase.LoadAssetAtPath<BossLeapSlamPattern>(PatternFolder + "TestBossLeapSlam.asset");
            var summon = AssetDatabase.LoadAssetAtPath<BossSummonPattern>(PatternFolder + "TestBossSummon.asset");
            Assert.AreEqual(5f, leap.Cooldown); Assert.AreEqual(.6f, leap.Recovery, 1e-5f);
            Assert.AreEqual("LeapTelegraph", leap.TelegraphAnimation); Assert.AreEqual("LeapExecute", leap.ExecuteAnimation); Assert.AreEqual("Recover", leap.RecoverAnimation);
            Assert.AreEqual(.3f, leap.TelegraphTime, 1e-5f); Assert.AreEqual(.4f, leap.RiseTime, 1e-5f); Assert.AreEqual(5f, leap.JumpHeight); Assert.AreEqual(.6f, leap.AimTime, 1e-5f);
            Assert.AreEqual(10f, leap.AirSpeed); Assert.AreEqual(1f, leap.LandDelay); Assert.AreEqual(30f, leap.DropSpeed); Assert.AreEqual(3f, leap.ShockwaveRadius);
            Assert.AreEqual(15, leap.Damage); Assert.AreEqual(.15f, leap.LandingTime, 1e-5f);
            Assert.AreEqual(12f, summon.Cooldown); Assert.AreEqual(.5f, summon.Recovery, 1e-5f);
            Assert.AreEqual("SummonTelegraph", summon.TelegraphAnimation); Assert.AreEqual("SummonExecute", summon.ExecuteAnimation); Assert.AreEqual("Recover", summon.RecoverAnimation);
            Assert.AreEqual(Root + "Prefabs/Enemies/PatrolEnemy.prefab", AssetDatabase.GetAssetPath(summon.MinionPrefab));
            Assert.AreEqual(new[] { new Vector2(-2.5f, 0), new Vector2(2.5f, 0) }, new List<Vector2>(summon.SpawnOffsets).ToArray());
            Assert.AreEqual(2, summon.MaxAlive); Assert.AreEqual(.8f, summon.TelegraphTime, 1e-5f); Assert.AreEqual(.3f, summon.ExecuteTime, 1e-5f); Assert.IsTrue(summon.DespawnWithBoss);
            Assert.IsTrue(leap.TryValidate(out string error), error); Assert.IsTrue(summon.TryValidate(out error), error);
        }

        [Test] public void TestBossSelectorHasTheDocumentedTree()
        {
            var selector = AssetDatabase.LoadAssetAtPath<BossPatternSelector>(Root + "Data/Bosses/TestBoss/TestBossSelector.asset");
            var root = (BossSelectorBranch)selector.Root;
            Assert.AreEqual(4, root.Items.Count);
            BossPatternDefinition LeafPattern(BossSelectorNode node) => ((BossPatternLeaf)((BossSequenceBranch)node).Items[^1]).Pattern;
            Assert.IsInstanceOf<BossSummonPattern>(LeafPattern(root.Items[0]));
            Assert.IsInstanceOf<BossPhaseCondition>(((BossSequenceBranch)root.Items[0]).Items[0]);
            var weighted = (BossWeightedBranch)((BossSequenceBranch)root.Items[1]).Items[^1];
            Assert.IsInstanceOf<BossMeleeAreaPattern>(((BossPatternLeaf)weighted.Items[0].node).Pattern); Assert.AreEqual(3f, weighted.Items[0].weight);
            Assert.IsInstanceOf<BossRepositionPattern>(((BossPatternLeaf)weighted.Items[1].node).Pattern); Assert.AreEqual(1f, weighted.Items[1].weight);
            Assert.IsInstanceOf<BossChargePattern>(LeafPattern(root.Items[2]));
            Assert.IsInstanceOf<BossLeapSlamPattern>(LeafPattern(root.Items[3]));
            Assert.IsTrue(selector.TryValidate(out string error), error);
        }

        [Test] public void LeapSlamValidationRejectsBadSettings()
        {
            foreach (var (field, value) in new (string, float)[] { ("riseTime", 0f), ("jumpHeight", 0f), ("airSpeed", 0f), ("dropSpeed", -1f), ("shockwaveRadius", 0f),
                ("shockwaveRadius", float.PositiveInfinity), ("aimTime", float.NaN), ("landDelay", -1f), ("landingTime", -.1f), ("telegraphTime", -1f) })
            {
                var leap = ScriptableObject.CreateInstance<BossLeapSlamPattern>();
                try
                {
                    Assert.IsTrue(leap.TryValidate(out string error), "defaults: " + error);
                    var so = new SerializedObject(leap); so.FindProperty(field).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo();
                    Assert.IsFalse(leap.TryValidate(out _), field + "=" + value);
                    so.FindProperty(field).floatValue = .5f; so.FindProperty("damage").intValue = -1; so.ApplyModifiedPropertiesWithoutUndo();
                    Assert.IsFalse(leap.TryValidate(out _), "negative damage");
                }
                finally { Object.DestroyImmediate(leap); }
            }
        }

        [Test] public void SummonValidationRejectsBadSettings()
        {
            var summon = ScriptableObject.CreateInstance<BossSummonPattern>();
            var noHealth = new GameObject("Minion without health"); noHealth.AddComponent<Unit>();
            try
            {
                var so = new SerializedObject(summon);
                Assert.IsFalse(summon.TryValidate(out _), "no prefab");
                so.FindProperty("minionPrefab").objectReferenceValue = noHealth; so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsFalse(summon.TryValidate(out _), "prefab without UnitHealth");
                so.FindProperty("minionPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Enemies/PatrolEnemy.prefab"); so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsTrue(summon.TryValidate(out string error), error);
                so.FindProperty("spawnOffsets").arraySize = 0; so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsFalse(summon.TryValidate(out _), "empty offsets");
                so.FindProperty("spawnOffsets").arraySize = 1; so.FindProperty("spawnOffsets").GetArrayElementAtIndex(0).vector2Value = new Vector2(float.NaN, 0); so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsFalse(summon.TryValidate(out _), "NaN offset");
                so.FindProperty("spawnOffsets").GetArrayElementAtIndex(0).vector2Value = Vector2.right; so.FindProperty("maxAlive").intValue = 0; so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsFalse(summon.TryValidate(out _), "maxAlive 0");
                so.FindProperty("maxAlive").intValue = 1; so.FindProperty("telegraphTime").floatValue = float.NaN; so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsFalse(summon.TryValidate(out _), "NaN telegraph");
            }
            finally { Object.DestroyImmediate(summon); Object.DestroyImmediate(noHealth); }
        }

        private static BossFsmDefinition Copy() => Object.Instantiate(AssetDatabase.LoadAssetAtPath<BossFsmDefinition>(FsmPath));
    }
}
