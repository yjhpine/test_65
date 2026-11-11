#if UNITY_EDITOR
using ActionPlatformer.Units;
using ActionPlatformer.Units.Bosses;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    // The shipped TestBoss prefab running against the rig's floor and player stand-in.
    public sealed class TestBossTests
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/Bosses/TestBoss.prefab";
        readonly BossTestRig rig = new BossTestRig();
        BossBrain Brain => rig.Brain;

        [TearDown] public void Cleanup() => rig.Cleanup();

        IEnumerator Setup(float playerDx)
        {
            rig.Floor();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            var go = Object.Instantiate(prefab, new Vector3(0, BossTestRig.FloorTop + 1.2f + .02f), Quaternion.identity);
            rig.Created.Add(go);
            rig.Bosses.Add(go.GetComponent<Unit>());
            rig.Fsm = (BossFsmDefinition)go.GetComponent<Unit>().Definition.Fsm;
            yield return null; yield return BossTestRig.Step;
            Physics2D.SyncTransforms();
            rig.SpawnPlayer(new Vector2(playerDx, 0));
        }

        // Ticks without physics, so the boss stays where it is, until a pattern is chosen.
        void TickUntilPattern(int limit = 80)
        {
            for (int i = 0; i < limit && Brain.Pattern == null; i++) rig.Tick(.05f);
            Assert.That(Brain.Pattern, Is.Not.Null, Brain.LastSelectionTrace);
        }

        [UnityTest] public IEnumerator CloseRangePicksMeleeOrBackstepAndFarGroundedPicksCharge()
        {
            yield return Setup(2);
            TickUntilPattern();
            Assert.That(Brain.Pattern, Is.TypeOf<BossMeleeAreaPattern>().Or.TypeOf<BossRepositionPattern>());
            rig.Cleanup();

            yield return Setup(8);
            TickUntilPattern();
            Assert.That(Brain.Pattern, Is.TypeOf<BossChargePattern>());
            rig.Cleanup();

            yield return Setup(8);
            rig.Probe.grounded = false;
            TickUntilPattern();
            Assert.That(Brain.Pattern, Is.TypeOf<BossLeapSlamPattern>(), "Charge needs a grounded player; the leap does not.");
        }

        [UnityTest] public IEnumerator MidRangePicksLeapSlam()
        {
            yield return Setup(4.5f);
            TickUntilPattern();
            Assert.That(Brain.Pattern, Is.TypeOf<BossLeapSlamPattern>(), Brain.LastSelectionTrace);
        }

        [UnityTest] public IEnumerator BossCanBeKilledAndPhaseShiftsAtHalfHealth()
        {
            yield return Setup(2);
            TickUntilPattern();
            rig.Health.ApplyDamage(150);
            Assert.That(rig.Health.CurrentHealth, Is.EqualTo(150));
            for (int i = 0; i < 200 && Brain.State != BossState.PhaseShift; i++) rig.Tick(.05f);
            Assert.That(Brain.State, Is.EqualTo(BossState.PhaseShift));
            Assert.That(Brain.Phase, Is.EqualTo(1));
            for (int i = 0; i < 200 && Brain.State == BossState.PhaseShift; i++) rig.Tick(.05f);
            Assert.That(Brain.State, Is.Not.EqualTo(BossState.PhaseShift));
            TickUntilPattern();
            Assert.That(Brain.Pattern, Is.TypeOf<BossSummonPattern>(), "Phase 1 summons first: " + Brain.LastSelectionTrace);
            for (int i = 0; i < 100 && Brain.State != BossState.Execute; i++) rig.Tick(.05f);
            Assert.That(Brain.State, Is.EqualTo(BossState.Execute));
            var minions = new List<UnitHealth>();
            foreach (var pig in Object.FindObjectsByType<UnitCombat2D>(FindObjectsSortMode.None)) { minions.Add(pig.GetComponent<UnitHealth>()); rig.Created.Add(pig.gameObject); }
            Assert.That(minions, Has.Count.EqualTo(2), "Two PatrolEnemy minions appear.");
            rig.Health.ApplyDamage(150);
            rig.Tick(.05f);
            Assert.That(Brain.State, Is.EqualTo(BossState.Death));
            foreach (var minion in minions) Assert.That(minion.IsAlive, Is.False, "Minions die with the boss.");
            rig.TickFor(1.1f);
            Assert.That(rig.Boss.gameObject.activeSelf, Is.True);
            rig.TickFor(.3f);
            Assert.That(rig.Boss.gameObject.activeSelf, Is.False);
        }
    }
}
#endif
