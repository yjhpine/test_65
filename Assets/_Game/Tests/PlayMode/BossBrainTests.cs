#if UNITY_EDITOR
using ActionPlatformer.Player;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Bosses;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    public sealed class BossBrainTests
    {
        readonly BossTestRig rig = new BossTestRig();
        BossBrain Brain => rig.Brain;
        BossState State => rig.Brain.State;

        [TearDown] public void Cleanup() => rig.Cleanup();

        // Spawns a floor, a boss with a one-leaf tree (optionally behind a distance gate) and a player stand-in.
        IEnumerator Setup(ScriptedBossPattern pattern, Vector2 playerOffset, BossSelectorNode gate = null, System.Action<BossFsmDefinition> configure = null)
        {
            rig.Floor();
            var leaf = new BossPatternLeaf(pattern);
            var selector = rig.Selector(gate == null ? (BossSelectorNode)leaf : new BossSequenceBranch(gate, leaf));
            yield return rig.SpawnBoss(selector, configure);
            rig.SpawnPlayer(playerOffset);
        }

        // Ticks until the boss reaches a state, failing the test instead of looping forever.
        void TickUntil(BossState state, float dt = .05f, int limit = 200)
        {
            for (int i = 0; i < limit && State != state; i++) rig.Tick(dt);
            Assert.That(State, Is.EqualTo(state));
        }

        [UnityTest] public IEnumerator DormantUntilPlayerRecognizedThenApproaches()
        {
            yield return Setup(rig.Pattern(), new Vector2(20, 0), new BossDistanceCondition(0, 1, -3, 3));
            for (int i = 0; i < 10; i++) { rig.Tick(.05f); Assert.That(State, Is.EqualTo(BossState.Dormant)); }
            rig.MovePlayer((Vector2)rig.Boss.transform.position + new Vector2(8, 0));
            rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Approach));
        }

        [UnityTest] public IEnumerator ApproachStopsAtPreferredDistanceWhenNoPatternFits()
        {
            yield return Setup(rig.Pattern(), new Vector2(6, 0), new BossDistanceCondition(0, 1, -3, 3));
            yield return new WaitForSeconds(2f);
            Assert.That(State, Is.EqualTo(BossState.Approach));
            Assert.That(Mathf.Abs(Brain.Board.DeltaX), Is.EqualTo(2.5f).Within(.2f));
            Assert.That(Brain.LastSelectionTrace, Is.Not.Empty);
        }

        [UnityTest] public IEnumerator PatternRunsTelegraphExecuteRecoverThenDecidesAgain()
        {
            var pattern = rig.Pattern(.3f, .3f, .2f);
            yield return Setup(pattern, new Vector2(5, 0), new BossDistanceCondition(0, 10, -3, 3));
            var order = new List<BossState>();
            for (int i = 0; i < 300 && order.Count < 8; i++)
            {
                rig.Tick(.1f);
                var s = State;
                if (s != BossState.Dormant && (order.Count == 0 || order[order.Count - 1] != s)) order.Add(s);
            }
            Assert.That(order, Is.EqualTo(new[] { BossState.Approach, BossState.Telegraph, BossState.Execute, BossState.Recover,
                BossState.Approach, BossState.Telegraph, BossState.Execute, BossState.Recover }), "Recover returns through Approach before the next Telegraph.");
            Assert.That(pattern.Runs, Has.Count.EqualTo(1), "The run is created once per boss and reused.");
            var run = pattern.Runs[0];
            Assert.That(run.BeginTelegraphCalls, Is.EqualTo(2));
            Assert.That(run.BeginExecuteCalls, Is.EqualTo(2));
            Assert.That(run.EndCalls, Is.EqualTo(2));
            Assert.That(run.CancelCalls, Is.Zero);
        }

        [UnityTest] public IEnumerator TelegraphFixesFacingUntilRecover()
        {
            yield return Setup(rig.Pattern(.3f, .3f, .2f), new Vector2(5, 0));
            TickUntil(BossState.Telegraph);
            Assert.That(Brain.Facing, Is.EqualTo(1));
            Assert.That(Brain.Telegraph.Visible, Is.True);
            rig.MovePlayer((Vector2)rig.Boss.transform.position + new Vector2(-5, 0));
            for (int i = 0; i < 100 && State != BossState.Recover; i++)
            {
                rig.Tick(.05f);
                Assert.That(Brain.Facing, Is.EqualTo(1), "Facing stays fixed through the telegraph and execute.");
                Assert.That(Brain.Board.BossFacing, Is.EqualTo(1));
            }
            Assert.That(State, Is.EqualTo(BossState.Recover));
            Assert.That(Brain.Facing, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator GuardDelegatesToTheRunningPatternOnly()
        {
            yield return Setup(rig.Pattern(blockAll: true), new Vector2(5, 0));
            Vector2 source = (Vector2)rig.Boss.transform.position + new Vector2(3, 0);
            rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Approach));
            Assert.That(rig.Health.ApplyDamage(10, source), Is.EqualTo(10), "Not blocked outside a pattern.");
            TickUntil(BossState.Telegraph);
            Assert.That(rig.Health.ApplyDamage(10, source), Is.Zero, "The running pattern decides.");
            Assert.That(rig.Health.GuardBlockVersion, Is.EqualTo(1));
            Assert.That(rig.Health.CurrentHealth, Is.EqualTo(290));
            TickUntil(BossState.Execute);
            Assert.That(rig.Health.ApplyDamage(10, source), Is.Zero, "Execute blocks too.");
            Assert.That(rig.Health.GuardBlockVersion, Is.EqualTo(2));
            TickUntil(BossState.Recover);
            Assert.That(rig.Health.ApplyDamage(10, source), Is.EqualTo(10), "Recover does not block.");
            Assert.That(rig.Health.GuardBlockVersion, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator TwoBossesSharingAssetsKeepIndependentCooldowns()
        {
            rig.Floor();
            var pattern = rig.Pattern(.3f, .3f, .2f, cooldown: 10);
            var selector = rig.Selector(new BossPatternLeaf(pattern));
            yield return rig.SpawnBoss(selector, null, -4);
            var second = rig.Fsm;
            yield return rig.SpawnBoss(null, null, 4, second);
            Assert.That(rig.Bosses, Has.Count.EqualTo(2));
            rig.SpawnPlayer(new Vector2(3, 0));
            var a = rig.Bosses[0]; var b = rig.Bosses[1];
            for (int i = 0; i < 100 && BossTestRig.BrainOf(a).State != BossState.Telegraph; i++) rig.Tick(.05f, a);
            Assert.That(BossTestRig.BrainOf(a).Board.IsCoolingDown(pattern), Is.True);
            Assert.That(BossTestRig.BrainOf(b).State, Is.EqualTo(BossState.Dormant), "B has not been ticked.");
            for (int i = 0; i < 10 && BossTestRig.BrainOf(b).State != BossState.Telegraph; i++) rig.Tick(.05f, b);
            Assert.That(BossTestRig.BrainOf(b).State, Is.EqualTo(BossState.Telegraph));
            Assert.That(BossTestRig.BrainOf(b).Pattern, Is.SameAs(pattern));
            Assert.That(pattern.Runs, Has.Count.EqualTo(2), "Each boss has its own run object.");
        }

        [UnityTest] public IEnumerator DisableAndEnableRestartsFromDormantAndClearsMemory()
        {
            var pattern = rig.Pattern(.3f, .3f, .2f, cooldown: 10);
            yield return Setup(pattern, new Vector2(5, 0));
            TickUntil(BossState.Execute);
            Assert.That(Brain.Board.IsCoolingDown(pattern), Is.True);
            rig.Health.ApplyDamage(160); // below the .5 threshold: a phase shift is due after the restart
            rig.Boss.gameObject.SetActive(false);
            Assert.That(pattern.Runs[0].CancelCalls, Is.EqualTo(1));
            Assert.That(pattern.Runs[0].EndCalls, Is.Zero);
            rig.Boss.gameObject.SetActive(true);
            Assert.That(State, Is.EqualTo(BossState.Dormant));
            Assert.That(Brain.Board.IsCoolingDown(pattern), Is.False);
            Assert.That(Brain.Board.LastPattern, Is.Null);
            TickUntil(BossState.PhaseShift);
            Assert.That(rig.Health.DamageEnabled, Is.False, "Invulnerable during the shift.");
            rig.Boss.gameObject.SetActive(false);
            rig.Boss.gameObject.SetActive(true);
            Assert.That(State, Is.EqualTo(BossState.Dormant));
            Assert.That(rig.Health.DamageEnabled, Is.True, "Disabling mid-shift must not leave the boss invulnerable.");
        }

        // Two patterns with long cooldowns: after the first finishes the second runs, so the first stays cached but inactive.
        IEnumerator SetupTwoCachedRuns(System.Action<ScriptedBossPattern, ScriptedBossPattern> result)
        {
            var a = rig.Pattern(.1f, .1f, .1f, cooldown: 100); var b = rig.Pattern(.1f, .1f, .1f, cooldown: 100);
            rig.Floor();
            yield return rig.SpawnBoss(rig.Selector(new BossPatternLeaf(a), new BossPatternLeaf(b)));
            rig.SpawnPlayer(new Vector2(5, 0));
            for (int i = 0; i < 200 && b.Runs.Count == 0; i++) rig.Tick(.05f);
            Assert.That(a.Runs, Has.Count.EqualTo(1)); Assert.That(b.Runs, Has.Count.EqualTo(1));
            Assert.That(Brain.Pattern, Is.SameAs(b));
            result(a, b);
        }

        [UnityTest] public IEnumerator DeathNotifiesEveryCachedRunOnce()
        {
            ScriptedBossPattern a = null, b = null;
            yield return SetupTwoCachedRuns((x, y) => { a = x; b = y; });
            Assert.That(a.Runs[0].DiedCalls + b.Runs[0].DiedCalls, Is.Zero);
            rig.Health.ApplyDamage(300);
            rig.Tick(.05f); rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Death));
            Assert.That(a.Runs[0].DiedCalls, Is.EqualTo(1), "inactive cached run");
            Assert.That(b.Runs[0].DiedCalls, Is.EqualTo(1), "active run");
            rig.TickFor(1.5f, .05f);
            Assert.That(a.Runs[0].DiedCalls, Is.EqualTo(1));
            Assert.That(a.Runs[0].StoppedCalls, Is.EqualTo(1), "the boss object is then disabled, which stops the fight");
        }

        [UnityTest] public IEnumerator StopNotifiesEveryCachedRunAfterCancellingTheActiveOne()
        {
            ScriptedBossPattern a = null, b = null;
            yield return SetupTwoCachedRuns((x, y) => { a = x; b = y; });
            rig.Boss.gameObject.SetActive(false);
            Assert.That(a.Runs[0].StoppedCalls, Is.EqualTo(1), "inactive cached run");
            Assert.That(b.Runs[0].StoppedCalls, Is.EqualTo(1), "active run");
            Assert.That(b.Runs[0].CancelCalls, Is.EqualTo(1));
            Assert.That(a.Runs[0].DiedCalls + b.Runs[0].DiedCalls, Is.Zero);
        }

        [UnityTest] public IEnumerator StoppedBrainReportsDormantAndRestoresFacing()
        {
            yield return Setup(rig.Pattern(.3f, .3f, .2f), new Vector2(5, 0));
            Assert.That(Brain.Facing, Is.EqualTo(-1), "The rig boss starts facing left.");
            Assert.That(Brain.Board.BossFacing, Is.EqualTo(-1), "Set at construction, not on the first tick.");
            TickUntil(BossState.Telegraph);
            Assert.That(Brain.Facing, Is.EqualTo(1));
            rig.Boss.gameObject.SetActive(false);
            Assert.That(State, Is.EqualTo(BossState.Dormant));
            Assert.That(Brain.Pattern, Is.Null);
            Assert.That(Brain.Facing, Is.EqualTo(-1), "Facing returns to the original on restart.");
            Assert.That(Brain.Board.BossFacing, Is.EqualTo(-1));
        }

        [UnityTest] public IEnumerator PausedFsmFreezesClockAndPattern()
        {
            yield return Setup(rig.Pattern(.3f, .3f, .2f), new Vector2(5, 0));
            TickUntil(BossState.Telegraph);
            rig.Tick(.1f);
            float elapsed = Brain.StateElapsed, clock = Brain.Board.Clock;
            var pause = rig.Boss.Fsm.Pause();
            yield return new WaitForSeconds(1f);
            rig.Tick(.5f);
            Assert.That(Brain.StateElapsed, Is.EqualTo(elapsed));
            Assert.That(Brain.Board.Clock, Is.EqualTo(clock));
            Assert.That(State, Is.EqualTo(BossState.Telegraph));
            pause.Dispose();
            for (int i = 0; i < 3 && State == BossState.Telegraph; i++) rig.Tick(.1f);
            Assert.That(State, Is.EqualTo(BossState.Execute));
        }

        [UnityTest] public IEnumerator InvalidSettingsThrowOnCreate()
        {
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException.*패턴 선택 트리가 지정되지 않았습니다"));
            rig.Floor();
            yield return rig.SpawnBoss(null);
            Assert.That(rig.Boss.Fsm, Is.Null, "The FSM is never assigned when its creation throws.");
        }

        // ---- Interruptions: stagger, phase shift, death ----

        Vector2 PlayerPos => rig.Probe.transform.position;
        int Interrupt() => rig.Health.ApplyDamage(1, PlayerPos, false, true);

        [UnityTest] public IEnumerator InterruptingHitStaggersDuringTelegraphButNotExecute()
        {
            var pattern = rig.Pattern(.3f, .3f, .2f);
            yield return Setup(pattern, new Vector2(5, 0));
            TickUntil(BossState.Telegraph);
            Assert.That(Interrupt(), Is.EqualTo(1));
            rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Staggered));
            Assert.That(pattern.Runs[0].CancelCalls, Is.EqualTo(1));
            TickUntil(BossState.Approach);
            TickUntil(BossState.Execute);
            Interrupt();
            rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Execute));
            for (int i = 0; i < 60 && State != BossState.Recover; i++) { rig.Tick(.05f); Assert.That(State, Is.Not.EqualTo(BossState.Staggered)); }
            Assert.That(State, Is.EqualTo(BossState.Recover));
            for (int i = 0; i < 12; i++) { rig.Tick(.05f); Assert.That(State, Is.Not.EqualTo(BossState.Staggered), "The ignored hit is not applied later."); }
            Assert.That(pattern.Runs[0].CancelCalls, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator OrdinaryHitNeverStaggers()
        {
            yield return Setup(rig.Pattern(.3f, .3f, .2f), new Vector2(20, 0), new BossDistanceCondition(0, 10, -3, 3));
            void Check()
            {
                var before = State; var version = Brain.StateVersion;
                rig.Health.ApplyDamage(1); rig.Health.ApplyDamage(1, PlayerPos, false, false);
                rig.Tick(.001f);
                Assert.That(State, Is.EqualTo(before)); Assert.That(Brain.StateVersion, Is.EqualTo(version));
            }
            Check();                                                   // Dormant
            rig.MovePlayer((Vector2)rig.Boss.transform.position + new Vector2(12, 0));
            rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Approach));        // out of the gate's range, so no pattern fits
            Check();
            rig.MovePlayer((Vector2)rig.Boss.transform.position + new Vector2(5, 0));
            TickUntil(BossState.Telegraph); Check();
            TickUntil(BossState.Execute); Check();
            TickUntil(BossState.Recover); Check();
        }

        [UnityTest] public IEnumerator CooldownStartsAtTelegraphEvenWhenCancelled()
        {
            var pattern = rig.Pattern(.3f, .3f, .2f, cooldown: 5);
            yield return Setup(pattern, new Vector2(5, 0));
            TickUntil(BossState.Telegraph);
            float start = Brain.Board.Clock;
            Interrupt();
            while (Brain.Board.Clock - start < 4.9f)
            {
                rig.Tick(.05f);
                Assert.That(State, Is.Not.EqualTo(BossState.Telegraph), $"Picked again at {Brain.Board.Clock - start:F2}s.");
            }
            TickUntil(BossState.Telegraph);
            Assert.That(Brain.Board.Clock - start, Is.GreaterThanOrEqualTo(5f - .001f));
            Assert.That(Brain.Board.Clock - start, Is.LessThan(5.4f));
        }

        [UnityTest] public IEnumerator StaggerImmunityPreventsChainStagger()
        {
            yield return Setup(rig.Pattern(.3f, .3f, .2f, cooldown: 100), new Vector2(5, 0));
            TickUntil(BossState.Telegraph);
            Interrupt(); rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Staggered));
            TickUntil(BossState.Approach);
            rig.TickFor(1.5f, .05f);
            Interrupt(); rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Approach), "1.5s after the stagger ended: still immune (it is 2.1s after it began).");
            rig.TickFor(.5f, .05f);
            Interrupt(); rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Staggered), "2.1s after it ended: staggers again.");
        }

        [UnityTest] public IEnumerator ProgressRisesThroughStaggerPhaseShiftAndDeath()
        {
            yield return Setup(rig.Pattern(.3f, .3f, .2f, cooldown: 100), new Vector2(5, 0), null, f => BossTestRig.SetThresholds(f, .5f));
            TickUntil(BossState.Telegraph);
            Interrupt(); rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Staggered));
            Assert.That(Brain.Progress, Is.LessThan(.2f));
            rig.TickFor(.3f, .05f);
            Assert.That(Brain.Progress, Is.InRange(.3f, .9f), "Hit animation is driven by Progress.");
            TickUntil(BossState.Approach);
            rig.Health.ApplyDamage(160);
            TickUntil(BossState.PhaseShift);
            rig.TickFor(.6f, .05f);
            Assert.That(Brain.Progress, Is.InRange(.2f, .9f), "PhaseShift progress.");
            rig.Health.SetDamageEnabled(true);
            rig.Health.ApplyDamage(300);
            rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Death));
            rig.TickFor(.6f, .05f);
            Assert.That(Brain.Progress, Is.InRange(.3f, .8f), "Death progress.");
        }

        [UnityTest] public IEnumerator PhaseShiftWaitsForRunningPatternAndSkipsToDeepestPhase()
        {
            yield return Setup(rig.Pattern(.3f, .3f, .2f), new Vector2(5, 0), null, f => BossTestRig.SetThresholds(f, .66f, .33f));
            TickUntil(BossState.Execute);
            Assert.That(Brain.Phase, Is.Zero);
            rig.Health.ApplyDamage(240);
            var seen = new List<BossState>(); var phases = new List<int>();
            for (int i = 0; i < 100 && State != BossState.PhaseShift; i++)
            {
                rig.Tick(.05f);
                if (seen.Count == 0 || seen[seen.Count - 1] != State) { seen.Add(State); phases.Add(Brain.Phase); }
            }
            Assert.That(seen, Is.EqualTo(new[] { BossState.Execute, BossState.Recover, BossState.PhaseShift }), "Waits for Execute and Recover, then shifts.");
            Assert.That(phases, Is.EqualTo(new[] { 0, 0, 2 }), "One shift straight to the deepest phase.");
            Assert.That(rig.Health.ApplyDamage(10), Is.Zero, "Invulnerable during the shift.");
            Interrupt(); rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.PhaseShift), "Interrupting hits are ignored.");
            TickUntil(BossState.Approach);
            Assert.That(Brain.Phase, Is.EqualTo(2));
            Assert.That(rig.Health.DamageEnabled, Is.True);
            Assert.That(rig.Health.ApplyDamage(10), Is.EqualTo(10));
        }

        [UnityTest] public IEnumerator LethalHitDiesFromAnyStateAndDisablesAfterDelay()
        {
            var pattern = rig.Pattern(.3f, .3f, .2f);
            yield return Setup(pattern, new Vector2(5, 0));
            TickUntil(BossState.Telegraph);
            rig.Health.ApplyDamage(300);
            rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Death));
            Assert.That(pattern.Runs[0].CancelCalls, Is.EqualTo(1));
            rig.TickFor(1.1f, .05f);
            Assert.That(rig.Boss.gameObject.activeSelf, Is.True);
            rig.TickFor(.2f, .05f);
            Assert.That(rig.Boss.gameObject.activeSelf, Is.False);
            Assert.That(pattern.Runs[0].CancelCalls, Is.EqualTo(1));

            rig.Cleanup();
            var second = rig.Pattern(.3f, .3f, .2f);
            yield return Setup(second, new Vector2(5, 0), null, f => BossTestRig.Set(f, "invulnerableDuringPhaseShift", false));
            TickUntil(BossState.Execute);
            rig.Health.ApplyDamage(160);
            TickUntil(BossState.PhaseShift);
            Assert.That(rig.Health.DamageEnabled, Is.True, "Vulnerable when invulnerability is off.");
            rig.Health.ApplyDamage(140);
            rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Death));
            rig.TickFor(1.3f, .05f);
            Assert.That(rig.Boss.gameObject.activeSelf, Is.False);
        }

        // ---- Senses ----

        [UnityTest] public IEnumerator UndergroundPlayerStaysTargetedAtGroundMarker()
        {
            var pattern = rig.Pattern(.3f, .3f, .2f, cooldown: 100);
            yield return Setup(pattern, new Vector2(5, 0), new BossPlayerStateCondition(BossPlayerState.Underground));
            rig.Probe.underground = true;
            rig.Probe.marker = (Vector2)rig.Probe.transform.position + new Vector2(4, 0);
            rig.Player.SetDamageEnabled(false);
            bool telegraphed = false;
            for (int i = 0; i < 60; i++)
            {
                rig.Tick(.05f);
                Assert.That(State, Is.Not.EqualTo(BossState.Dormant), $"Dormant at tick {i}.");
                Assert.That(Brain.Board.Target, Is.SameAs(rig.Player));
                Assert.That(Brain.Board.PlayerPosition.x, Is.EqualTo(rig.Probe.marker.Value.x).Within(1e-4f));
                Assert.That(Brain.Board.PlayerUnderground, Is.True);
                telegraphed |= State == BossState.Telegraph;
            }
            Assert.That(telegraphed, Is.True, "The Underground condition tree picks a pattern.");
        }

        [UnityTest] public IEnumerator RecentlyGlitchedTracksRepositionVersion()
        {
            yield return Setup(rig.Pattern(.3f, .3f, .2f, cooldown: 100), new Vector2(5, 0));
            var recently = new BossPlayerStateCondition(BossPlayerState.RecentlyGlitched, .5f);
            bool Check() => recently.Evaluate(new BossSelection(Brain.Board, Brain.Context), 0);
            rig.Probe.repositionVersion = 7;
            rig.Tick(.05f);
            Assert.That(float.IsPositiveInfinity(Brain.Board.SinceGlitch), Is.True, "The first observed version is only recorded.");
            Assert.That(Check(), Is.False);
            rig.Probe.repositionVersion = 8;
            rig.Tick(.05f);
            Assert.That(Check(), Is.True);
            rig.Tick(.6f);
            Assert.That(Check(), Is.False);
        }

        [UnityTest] public IEnumerator UnknownPlayerStatesFailConditionsWithoutPlayerContracts()
        {
            yield return Setup(rig.Pattern(.3f, .3f, .2f, cooldown: 100), new Vector2(5, 0));
            Object.DestroyImmediate(rig.Probe);
            rig.Tick(.05f);
            Assert.That(Brain.Board.HasTarget, Is.True);
            var s = new BossSelection(Brain.Board, Brain.Context);
            foreach (var state in new[] { BossPlayerState.Grounded, BossPlayerState.Airborne, BossPlayerState.Underground, BossPlayerState.Attacking, BossPlayerState.WatchingBoss })
                Assert.That(new BossPlayerStateCondition(state).Evaluate(s, 0), Is.False, state.ToString());
            Assert.That(new BossDistanceCondition(0, 10, -3, 3).Evaluate(s, 0), Is.True);
        }

        [UnityTest] public IEnumerator OffScreenBossIsNotRecognized()
        {
            yield return Setup(rig.Pattern(.3f, .3f, .2f, cooldown: 100), new Vector2(5, 0));
            var cam = rig.Track(new GameObject("Boss test camera")).AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 4;
            Vector3 boss = rig.Boss.transform.position;
            cam.transform.position = new Vector3(boss.x + 60, boss.y, -10);
            rig.Probe.view = cam;
            for (int i = 0; i < 10; i++) { rig.Tick(.05f); Assert.That(State, Is.EqualTo(BossState.Dormant)); }
            cam.transform.position = new Vector3(boss.x + 3, boss.y, -10);
            rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Approach));
        }

        [UnityTest] public IEnumerator LostTargetStopsSelectingAndReturnsToDormantAfterLoseTime()
        {
            yield return Setup(rig.Pattern(.3f, .3f, .2f), new Vector2(5, 0));
            rig.Tick(.05f);
            Assert.That(State, Is.EqualTo(BossState.Approach));
            rig.Player.gameObject.SetActive(false);
            float start = Brain.Board.Clock;
            for (int i = 0; i < 100 && State != BossState.Dormant; i++)
            {
                rig.Tick(.05f);
                Assert.That(State, Is.Not.EqualTo(BossState.Telegraph), "No pattern is chosen without a target.");
            }
            Assert.That(State, Is.EqualTo(BossState.Dormant));
            Assert.That(Brain.Board.Clock - start, Is.EqualTo(Brain.Settings.LoseTargetTime).Within(.06f));
        }

        [UnityTest] public IEnumerator PlayerUnitReportsMotorStateThroughMotionContract()
        {
            rig.Floor();
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab"), new Vector3(0, BossTestRig.FloorTop + 1.3f, 0), Quaternion.identity);
            rig.Track(go);
            var player = go.GetComponent<PlayerUnit>();
            yield return null;
            for (int i = 0; i < 10; i++) yield return BossTestRig.Step;
            var motion = (ICharacterMotionState)player;
            Assert.That(motion.IsGrounded, Is.True);
            Assert.That(motion.Velocity, Is.EqualTo(player.Motor.Velocity));
        }
    }
}
#endif
