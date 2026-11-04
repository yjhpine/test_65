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
    public sealed class BossPatternTests
    {
        readonly BossTestRig rig = new BossTestRig();
        readonly List<int> hits = new List<int>();
        BossState State => rig.Brain.State;

        [TearDown] public void Cleanup() => rig.Cleanup();

        T Make<T>(System.Action<T> configure = null) where T : BossPatternDefinition
        {
            var p = rig.Track(ScriptableObject.CreateInstance<T>());
            BossTestRig.Set(p, "cooldown", 100f);
            configure?.Invoke(p);
            return p;
        }

        // One leaf behind "distance 0..20", so the boss telegraphs as soon as it recognizes the player.
        IEnumerator Setup(BossPatternDefinition pattern, Vector2 playerOffset, float bossX = 0, System.Action<BossFsmDefinition> configure = null)
        {
            rig.Floor();
            var tree = rig.Selector(new BossSequenceBranch(new BossDistanceCondition(0, 20, -3, 3), new BossPatternLeaf(pattern)));
            yield return rig.SpawnBoss(tree, configure, bossX);
            rig.SpawnPlayer(playerOffset);
            hits.Clear();
            rig.Player.DamageApplied += r => hits.Add(r.AcceptedDamage);
        }

        IEnumerator WaitFor(BossState state, float timeout = 4f)
        {
            for (float t = 0; t < timeout && State != state; t += Time.deltaTime) yield return null;
            Assert.That(State, Is.EqualTo(state));
        }

        // Waits for Execute, then until the boss leaves it; returns how long Execute lasted.
        IEnumerator RunExecute(System.Action<float> result)
        {
            yield return WaitFor(BossState.Execute);
            float start = Time.time;
            for (int i = 0; i < 600 && State == BossState.Execute; i++) yield return null;
            result(Time.time - start);
        }

        [UnityTest] public IEnumerator MeleeHitsOncePerSwingAndOnlyInsideTheBox()
        {
            yield return Setup(Make<BossMeleeAreaPattern>(), new Vector2(1.5f, 0));
            yield return RunExecute(_ => { });
            Assert.That(hits, Is.EqualTo(new[] { 10 }));
            rig.Cleanup();

            yield return Setup(Make<BossMeleeAreaPattern>(), new Vector2(5, 0));
            yield return RunExecute(_ => { });
            Assert.That(hits, Is.Empty, "dx 5 is outside the 2.5 wide box.");
            rig.Cleanup();

            yield return Setup(Make<BossMeleeAreaPattern>(), new Vector2(1.5f, 0));
            // Telegraph fixes the facing at the player's side; moving behind afterwards puts the player outside the box.
            yield return WaitFor(BossState.Telegraph);
            rig.MovePlayer((Vector2)rig.Boss.transform.position + new Vector2(-1.5f, 0));
            yield return RunExecute(_ => { });
            Assert.That(hits, Is.Empty, "The box follows the fixed facing, not the player.");
        }

        [UnityTest] public IEnumerator MeleeMultiHitStrikesEachSwingSeparately()
        {
            var melee = Make<BossMeleeAreaPattern>(p => { BossTestRig.Set(p, "hitCount", 3); BossTestRig.Set(p, "hitInterval", .2f); });
            yield return Setup(melee, new Vector2(1.5f, 0));
            uint before = rig.Combat.AttackVersion;
            yield return RunExecute(_ => { });
            Assert.That(hits, Is.EqualTo(new[] { 10, 10, 10 }));
            Assert.That(rig.Combat.AttackVersion - before, Is.EqualTo(3u));
        }

        [UnityTest] public IEnumerator ChargeFollowsFixedDirectionAndHitsOnce()
        {
            yield return Setup(Make<BossChargePattern>(), new Vector2(4, 0));
            yield return RunExecute(_ => { });
            Assert.That(hits, Is.EqualTo(new[] { 12 }), "One damage per charge although the sweep overlaps several ticks.");
            Assert.That(rig.Boss.transform.position.x, Is.GreaterThan(5f));
            rig.Cleanup();

            yield return Setup(Make<BossChargePattern>(), new Vector2(4, 0));
            yield return WaitFor(BossState.Telegraph);
            rig.MovePlayer((Vector2)rig.Boss.transform.position + new Vector2(-4, 0));
            yield return RunExecute(_ => { });
            Assert.That(hits, Is.Empty, "The charge keeps the telegraphed direction.");
            Assert.That(rig.Boss.transform.position.x, Is.GreaterThan(4f));
        }

        [UnityTest] public IEnumerator ChargeTelegraphCoversTheSweptHitArea()
        {
            var charge = Make<BossChargePattern>();
            yield return Setup(charge, new Vector2(4, 0));
            yield return WaitFor(BossState.Telegraph);
            var t = rig.Brain.Telegraph;
            Assert.That(t.Visible, Is.True);
            Vector2 body = rig.Combat.BodySize, from = rig.Combat.Center, to = from + Vector2.right * rig.Brain.Facing * charge.Distance;
            Vector2 hitSize = body + new Vector2(Mathf.Abs(to.x - from.x), 0), hitCenter = (from + to) * .5f;
            Assert.That(t.Size.x, Is.EqualTo(hitSize.x).Within(.01f));
            Assert.That(t.Size.y, Is.EqualTo(hitSize.y).Within(.01f));
            Assert.That(t.Center.x, Is.EqualTo(hitCenter.x).Within(.01f));
            Assert.That(t.Center.y, Is.EqualTo(hitCenter.y).Within(.01f));
        }

        [UnityTest] public IEnumerator ChargeStartingAgainstWallEndsWithinSafetyTime()
        {
            yield return Setup(Make<BossChargePattern>(), new Vector2(5, 0));
            // Low wall: it stops the body but leaves the line of sight at body height open.
            var wall = new GameObject("Charge wall"); rig.Created.Add(wall);
            wall.transform.position = new Vector2(1.15f, BossTestRig.FloorTop + .4f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, .8f);
            Physics2D.SyncTransforms();
            float duration = -1;
            yield return RunExecute(d => duration = d);
            Assert.That(duration, Is.GreaterThan(0).And.LessThan(7f / 16f + .5f + .1f));
            Assert.That(rig.Boss.transform.position.x, Is.LessThan(.5f));
        }

        [UnityTest] public IEnumerator ChargeStopsAtLedge()
        {
            yield return Setup(Make<BossChargePattern>(), new Vector2(5, 0), bossX: 27.2f);
            float y = rig.Boss.transform.position.y;
            yield return RunExecute(_ => { });
            Assert.That(rig.Boss.transform.position.x, Is.LessThan(30f));
            Assert.That(rig.Boss.transform.position.y, Is.EqualTo(y).Within(.1f), "No fall off the platform.");
        }

        [UnityTest] public IEnumerator RepositionBacksAwayToDesiredDistanceWithoutDamage()
        {
            yield return Setup(Make<BossRepositionPattern>(), new Vector2(2, 0));
            yield return WaitFor(BossState.Recover);
            Assert.That(Mathf.Abs(rig.Player.transform.position.x - rig.Boss.transform.position.x), Is.EqualTo(6f).Within(.3f));
            Assert.That(hits, Is.Empty);
        }

        [UnityTest] public IEnumerator RepositionOnTopOfPlayerBacksOppositeFacing()
        {
            yield return Setup(Make<BossRepositionPattern>(), new Vector2(0, 0));
            rig.Brain.Context.Facing = -1f;
            yield return WaitFor(BossState.Recover);
            Assert.That(rig.Boss.transform.position.x, Is.GreaterThan(rig.Player.transform.position.x + 2f), "Facing left, so it backs off to the right.");
        }

        [UnityTest] public IEnumerator RepositionMeasuresDistanceFromOffCenterBodyCollider()
        {
            yield return Setup(Make<BossRepositionPattern>(), new Vector2(2, 0));
            // MoveTo takes the Rigidbody x while DeltaX is measured from the collider center; an offset collider exposes any frame mix-up.
            rig.Boss.GetComponent<BoxCollider2D>().offset = new Vector2(.6f, 0);
            Physics2D.SyncTransforms();
            yield return WaitFor(BossState.Recover);
            float dx = rig.Player.GetComponent<Collider2D>().bounds.center.x - rig.Combat.Center.x;
            Assert.That(Mathf.Abs(dx), Is.EqualTo(6f).Within(.3f));
        }

        [UnityTest] public IEnumerator UndergroundPlayerTakesNoDamage()
        {
            yield return Setup(Make<BossMeleeAreaPattern>(), new Vector2(1.5f, 0));
            rig.Player.SetDamageEnabled(false);
            yield return RunExecute(_ => { });
            Assert.That(hits, Is.Empty);
            rig.Cleanup();

            yield return Setup(Make<BossChargePattern>(), new Vector2(4, 0));
            rig.Player.SetDamageEnabled(false);
            yield return RunExecute(_ => { });
            Assert.That(hits, Is.Empty);
        }

        [UnityTest] public IEnumerator PatternsFinishSafelyWhenTargetDiesMidway()
        {
            // Cleanup destroys tracked assets, so each round builds its own pattern.
            var factories = new System.Func<BossPatternDefinition>[] { () => Make<BossMeleeAreaPattern>(), () => Make<BossChargePattern>(), () => Make<BossRepositionPattern>() };
            foreach (var factory in factories)
            {
                yield return Setup(factory(), new Vector2(4, 0), configure: f => BossTestRig.Set(f, "loseTargetTime", 1f));
                yield return WaitFor(BossState.Execute);
                rig.Player.gameObject.SetActive(false);
                yield return WaitFor(BossState.Recover);
                yield return WaitFor(BossState.Dormant, 6f);
                rig.Cleanup();
            }
        }

        // ---- Leap Slam and Summon ----

        static GameObject EnemyPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/" + name + ".prefab");

        void TickUntil(System.Func<bool> done, float dt = .02f, int limit = 1000)
        {
            for (int i = 0; i < limit && !done(); i++) rig.Tick(dt);
            Assert.That(done(), Is.True, "Timed out in state " + State);
        }

        Rigidbody2D BossBody => rig.Boss.GetComponent<Rigidbody2D>();

        GameObject Wall(float x, float y, float width, float height)
        {
            var wall = new GameObject("Boss pattern wall"); rig.Created.Add(wall);
            wall.transform.position = new Vector2(x, y);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(width, height);
            Physics2D.SyncTransforms();
            return wall;
        }

        // Every live object of a minion type (found through a component only that prefab has), registered for teardown.
        List<UnitHealth> Minions<TMarker>() where TMarker : Component
        {
            var list = new List<UnitHealth>();
            foreach (var marker in Object.FindObjectsByType<TMarker>(FindObjectsSortMode.None))
            {
                var health = marker.GetComponentInParent<UnitHealth>();
                if (!rig.Created.Contains(health.gameObject)) rig.Created.Add(health.gameObject);
                list.Add(health);
            }
            list.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            return list;
        }

        static float Bottom(UnitHealth minion)
        {
            foreach (var c in minion.GetComponentsInChildren<Collider2D>()) if (!c.isTrigger) return c.bounds.min.y;
            return float.NaN;
        }

        [UnityTest] public IEnumerator LeapSlamRisesAimsAtThePlayerAndLocksTheLandingPoint()
        {
            yield return Setup(Make<BossLeapSlamPattern>(), new Vector2(6, 0));
            TickUntil(() => State == BossState.Execute);
            float startY = BossBody.position.y, y = BossTestRig.FloorTop + 1.22f;
            Assert.That(rig.Brain.Telegraph.Visible, Is.True);
            rig.TickFor(.2f);
            Assert.That(BossBody.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
            Assert.That(BossBody.position.y, Is.GreaterThan(startY + 1f).And.LessThan(startY + 5f));
            rig.TickFor(.3f);
            Assert.That(BossBody.position.y, Is.EqualTo(startY + 5f).Within(.1f), "Risen by jumpHeight.");
            Assert.That(BossBody.position.x, Is.GreaterThan(.5f), "Moves toward the landing point while aiming.");
            var t = rig.Brain.Telegraph;
            Assert.That(t.Visible && !t.Locked, Is.True);
            Assert.That(t.Center.x, Is.EqualTo(6f).Within(.02f));
            Assert.That(t.Center.y, Is.EqualTo(BossTestRig.FloorTop + .15f).Within(.02f));
            Assert.That(t.Size.x, Is.EqualTo(6f).Within(.01f)); Assert.That(t.Size.y, Is.EqualTo(.3f).Within(.01f));

            rig.MovePlayer(new Vector2(9, y)); rig.Tick(.02f);
            Assert.That(rig.Brain.Telegraph.Center.x, Is.EqualTo(9f).Within(.02f), "Follows the moving player while aiming.");
            Assert.That(rig.Brain.Telegraph.Locked, Is.False);
            rig.TickFor(.5f);
            Assert.That(rig.Brain.Telegraph.Locked, Is.True);
            rig.MovePlayer(new Vector2(-5, y)); rig.TickFor(.2f);
            Assert.That(rig.Brain.Telegraph.Center.x, Is.EqualTo(9f).Within(.02f), "Fixed after the lock.");
            Assert.That(rig.Brain.Telegraph.Locked, Is.True);
        }

        [UnityTest] public IEnumerator LeapSlamLandsExactlyLandDelayAfterLockAndHitsInsideTheRadius()
        {
            var leap = Make<BossLeapSlamPattern>();
            yield return Setup(leap, new Vector2(4, 0));
            TickUntil(() => State == BossState.Execute);
            int tick = 0, lockTick = -1, downTick = -1;
            while (tick < 400 && downTick < 0)
            {
                rig.Tick(.02f); tick++;
                if (lockTick < 0 && rig.Brain.Telegraph.Locked) lockTick = tick;
                if (BossBody.bodyType != RigidbodyType2D.Kinematic) downTick = tick;
            }
            Assert.That(lockTick, Is.GreaterThan(0)); Assert.That(downTick, Is.GreaterThan(lockTick));
            Assert.That((downTick - lockTick) * .02f, Is.EqualTo(leap.LandDelay).Within(.021f));
            Assert.That(hits, Is.EqualTo(new[] { 15 }), "Player inside the radius takes the damage once.");
            Assert.That(BossBody.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(rig.Boss.GetComponent<GroundMovement2D>().enabled, Is.True);
            for (int i = 0; i < 80 && State != BossState.Approach; i++) rig.Tick(.02f);
            Assert.That(hits, Has.Count.EqualTo(1), "No second hit from the same landing.");
            rig.Cleanup();

            yield return Setup(Make<BossLeapSlamPattern>(), new Vector2(4, 0));
            TickUntil(() => State == BossState.Execute);
            TickUntil(() => rig.Brain.Telegraph.Locked);
            rig.MovePlayer(new Vector2(-8, BossTestRig.FloorTop + 1.22f));
            TickUntil(() => BossBody.bodyType != RigidbodyType2D.Kinematic);
            Assert.That(hits, Is.Empty, "A target outside the shockwave radius is not hit.");
        }

        [UnityTest] public IEnumerator LeapSlamNeverPassesThroughWalls()
        {
            yield return Setup(Make<BossLeapSlamPattern>(), new Vector2(6, 0));
            TickUntil(() => State == BossState.Execute);
            Wall(3, 60, 1, 20); // tall: the boss cannot fly over it
            float maxX = float.MinValue, maxLanding = float.MinValue;
            for (int i = 0; i < 400 && BossBody.bodyType == RigidbodyType2D.Kinematic; i++)
            {
                rig.Tick(.02f);
                maxX = Mathf.Max(maxX, BossBody.position.x);
                if (rig.Brain.Telegraph.Visible) maxLanding = Mathf.Max(maxLanding, rig.Brain.Telegraph.Center.x);
            }
            Assert.That(BossBody.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(maxX, Is.LessThan(2.5f - .7f), "Body half width .8 stays on this side of the wall face at x 2.5.");
            Assert.That(maxLanding, Is.GreaterThan(1f).And.LessThan(2.5f), "The landing point is pulled to the wall, on the boss's side.");
            Assert.That(maxX, Is.GreaterThan(1f), "The boss did fly toward the player.");
            Assert.That(hits, Is.Empty);
        }

        [UnityTest] public IEnumerator LeapSlamCancelledMidAirRestoresPhysics()
        {
            yield return Setup(Make<BossLeapSlamPattern>(), new Vector2(6, 0));
            TickUntil(() => State == BossState.Execute);
            rig.TickFor(.6f);
            Assert.That(BossBody.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
            Assert.That(rig.Boss.GetComponent<GroundMovement2D>().enabled, Is.False);
            rig.Health.ApplyDamage(300);
            rig.Tick(.02f); rig.Tick(.02f);
            Assert.That(State, Is.EqualTo(BossState.Death));
            Assert.That(BossBody.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(rig.Boss.GetComponent<GroundMovement2D>().enabled, Is.True);
            float y = BossBody.position.y;
            for (int i = 0; i < 10; i++) yield return BossTestRig.Step;
            Assert.That(BossBody.position.y, Is.LessThan(y - .05f), "The boss falls with gravity.");
            rig.Cleanup();

            yield return Setup(Make<BossLeapSlamPattern>(), new Vector2(6, 0));
            TickUntil(() => State == BossState.Execute);
            rig.TickFor(.6f);
            var boss = rig.Boss;
            boss.gameObject.SetActive(false);
            Assert.That(boss.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(boss.GetComponent<GroundMovement2D>().enabled, Is.True);
        }

        BossSummonPattern MakeSummon(string prefab, System.Action<BossSummonPattern> configure = null) =>
            Make<BossSummonPattern>(p => { BossTestRig.Set(p, "minionPrefab", EnemyPrefab(prefab)); configure?.Invoke(p); });

        [UnityTest] public IEnumerator SummonSpawnsConfiguredMinionsOnTheGround()
        {
            yield return Setup(MakeSummon("PatrolEnemy"), new Vector2(6, 0));
            TickUntil(() => State == BossState.Execute);
            var pigs = Minions<UnitCombat2D>();
            Assert.That(pigs, Has.Count.EqualTo(2));
            Assert.That(pigs[0].transform.position.x, Is.EqualTo(-2.5f).Within(.05f));
            Assert.That(pigs[1].transform.position.x, Is.EqualTo(2.5f).Within(.05f));
            foreach (var pig in pigs) Assert.That(Bottom(pig), Is.EqualTo(BossTestRig.FloorTop).Within(.05f));
            Assert.That(pigs[0].gameObject.scene, Is.EqualTo(rig.Boss.gameObject.scene));
            Assert.That(pigs[0].transform.parent, Is.Null);
            rig.Cleanup();

            yield return Setup(MakeSummon("TrainingDummy"), new Vector2(6, 0));
            TickUntil(() => State == BossState.Execute);
            Assert.That(Minions<UnitCombat2D>(), Is.Empty, "The Inspector prefab is used, not a hard-coded one.");
            var dummies = Minions<TrainingDummyVisual>();
            Assert.That(dummies, Has.Count.EqualTo(2));
            foreach (var dummy in dummies) Assert.That(Bottom(dummy), Is.EqualTo(BossTestRig.FloorTop).Within(.05f));
        }

        [UnityTest] public IEnumerator SummonRespectsMaxAliveAndWalls()
        {
            var summon = MakeSummon("PatrolEnemy", p => BossTestRig.Set(p, "cooldown", 0f));
            yield return Setup(summon, new Vector2(-6, 0));
            Wall(1.5f, 52, .5f, 4); // right of the boss; the player stands on the left, so sight stays open
            TickUntil(() => State == BossState.Execute);
            var first = Minions<UnitCombat2D>();
            Assert.That(first, Has.Count.EqualTo(2));
            Assert.That(first[0].transform.position.x, Is.EqualTo(-2.5f).Within(.05f));
            Assert.That(first[1].transform.position.x, Is.LessThan(1.25f - .5f).And.GreaterThan(0f), "The right offset is clamped before the wall face at 1.25.");
            var selection = new BossSelection(rig.Brain.Board, rig.Brain.Context);
            Assert.That(summon.CanStart(selection), Is.False, "Two alive: maxAlive reached.");

            first[0].ApplyDamage(first[0].CurrentHealth);
            Assert.That(summon.CanStart(selection), Is.True);
            TickUntil(() => State != BossState.Execute);
            TickUntil(() => State == BossState.Execute);
            var all = Minions<UnitCombat2D>();
            Assert.That(all, Has.Count.EqualTo(3), "One new minion; the dead one is not replaced twice.");
            Assert.That(all.FindAll(m => m.IsAlive), Has.Count.EqualTo(2));
            Assert.That(summon.CanStart(selection), Is.False);
        }

        [UnityTest] public IEnumerator MinionsDieWithTheBossAndAreRemovedWhenItStops()
        {
            yield return Setup(MakeSummon("PatrolEnemy"), new Vector2(6, 0));
            TickUntil(() => State == BossState.Execute);
            var pigs = Minions<UnitCombat2D>();
            Assert.That(pigs, Has.Count.EqualTo(2));
            rig.Health.ApplyDamage(300);
            rig.Tick(.02f); rig.Tick(.02f);
            Assert.That(State, Is.EqualTo(BossState.Death));
            foreach (var pig in pigs) { Assert.That(pig.IsAlive, Is.False); Assert.That(pig.CurrentHealth, Is.Zero); }
            rig.Cleanup();

            yield return Setup(MakeSummon("PatrolEnemy"), new Vector2(6, 0));
            TickUntil(() => State == BossState.Execute);
            pigs = Minions<UnitCombat2D>();
            Assert.That(pigs, Has.Count.EqualTo(2));
            rig.Boss.gameObject.SetActive(false);
            yield return null;
            foreach (var pig in pigs) Assert.That(pig == null, Is.True, "Minions are destroyed when the fight stops.");
        }
    }
}
#endif
