using System.Collections;
using System.Linq;
using ActionPlatformer.Flow;
using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    public sealed class RestCycleTests
    {
        private StageFlowController flow;
        private PlayerUnit player;
        private UnitHealth health;
        private StageRestArea rest;
        private Driver driver;
        private float savedScale, savedFixed;
        private sealed class Driver : IPlayerInputSource { public PlayerCommand Command; public PlayerCommand Sample() => Command; }

        [UnitySetUp] public IEnumerator Setup()
        {
            savedScale = Time.timeScale; savedFixed = Time.fixedDeltaTime; Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("CoreLoopStage");
            yield return null;
            flow = Object.FindFirstObjectByType<StageFlowController>(); player = flow.Player;
            health = player.GetComponent<UnitHealth>(); rest = flow.CurrentRestArea;
            driver = new Driver(); player.SetInputSource(driver);
            var intro = Object.FindFirstObjectByType<ActionPlatformer.Cinematics.CutsceneTrigger>();
            if (intro != null) intro.enabled = false;
            yield return new WaitForFixedUpdate();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var stage = SceneManager.GetSceneByName("CoreLoopStage");
            var blank = SceneManager.CreateScene("RestCycleCleanup"); SceneManager.SetActiveScene(blank);
            if (stage.isLoaded) yield return SceneManager.UnloadSceneAsync(stage);
            Time.timeScale = savedScale; Time.fixedDeltaTime = savedFixed;
        }
        private void KillRoom() { foreach (var enemy in flow.ActiveEnemies) enemy.ApplyDamage(int.MaxValue); }
        private IEnumerator RequestTransfer()
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!flow.CanRequestTransfer && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(flow.CanRequestTransfer, Is.True);
            driver.Command = new PlayerCommand { GlitchPressed = true };
            yield return null;
            driver.Command = default;
        }
        private IEnumerator Until(EncounterPhase phase, float timeout = 5)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (flow != null && flow.Phase != phase && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(flow, Is.Not.Null); Assert.That(flow.Phase, Is.EqualTo(phase));
        }
        private void AssertRestored()
        {
            var body = player.GetComponent<Rigidbody2D>();
            Assert.That(player.IsScriptedTransferring || player.IsControlLocked || player.IsUnderground, Is.False);
            Assert.That(body.simulated, Is.True); Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(player.AttackPhase, Is.EqualTo(PlayerAttackPhase.Ready));
        }
        private IEnumerator EnterNextRoom()
        {
            int nextIndex = flow.CurrentEncounterIndex + 1;
            var previousRest = flow.CurrentRestArea;
            player.Motor.Teleport(new Vector2(previousRest.NextEntry.EntryX - 1.15f, .82f));
            driver.Command = new PlayerCommand { Move = Vector2.right };
            float deadline = Time.realtimeSinceStartup + 3;
            while (flow.CurrentEncounterIndex != nextIndex && Time.realtimeSinceStartup < deadline) yield return null;
            driver.Command = default;
            Assert.That(flow.CurrentEncounterIndex, Is.EqualTo(nextIndex));
            Assert.That(flow.Phase, Is.EqualTo(EncounterPhase.Combat));
            Assert.That(previousRest.NextEntry.IsLocked, Is.True);
        }
        [UnityTest] public IEnumerator ClearedGateAllowsWalkingIntoEachRestAndHealsOnlyOnce()
        {
            for (int wave = 0; wave < 3; wave++)
            {
                var area = flow.CurrentRestArea;
                var exit = Object.FindObjectsByType<StageGate>(FindObjectsSortMode.None)
                    .Where(g => g.EntryX < area.NextEntry.EntryX).OrderByDescending(g => g.EntryX).First();
                Assert.That(exit.IsLocked, Is.True);
                health.ApplyDamage(10); yield return null;
                KillRoom(); yield return Until(EncounterPhase.TransferPending);
                Assert.That(exit.IsLocked, Is.False, "Clearing a room must open the walking route before Shift is pressed.");
                Assert.That(area.NextEntry.IsLocked, Is.True, "Next fight stays locked until rest is visited.");
                player.Motor.Teleport(new Vector2(exit.EntryX - 1.2f, .82f));
                driver.Command = new PlayerCommand { Move = Vector2.right };
                yield return Until(EncounterPhase.Rest);
                driver.Command = default;
                Assert.That(player.IsScriptedTransferring || player.IsControlLocked, Is.False);
                Assert.That(area.Visited, Is.True); Assert.That(area.NextEntry.IsLocked, Is.False);
                Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
                health.ApplyDamage(5);
                yield return null;
                Assert.That(area.TryVisit(health, player.GetComponent<Collider2D>()), Is.False);
                Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 5));
                yield return EnterNextRoom();
            }
        }

        [UnityTest] public IEnumerator WholeCycleHealsOnceActivatesNextRoomAndCompletes()
        {
            Assert.That(flow.UsesEncounters, Is.True); Assert.That(flow.RemainingEnemies, Is.EqualTo(2));
            var all = Object.FindObjectsByType<UnitHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(h => h != health).ToArray();
            Assert.That(all.Length, Is.EqualTo(14));
            Assert.That(all.Count(h => !h.gameObject.activeInHierarchy), Is.EqualTo(12));
            Assert.That(all.Where(h => !h.gameObject.activeInHierarchy).All(h => !h.CanReceiveDamage && h.MaxHealth == 0), Is.True);
            var goal = Object.FindFirstObjectByType<StageGoal>();
            float[] destinations = { 66, 116, 168 };
            string[][] expectedKinds = {
                new[] { "enemy.surveillanceturret", "enemy.surveillanceturret" },
                new[] { "enemy.patrol", "enemy.surveillanceturret", "enemy.flyingdrone" },
                new[] { "enemy.patrol", "enemy.surveillanceturret", "enemy.flyingdrone", "enemy.surveillanceturret" },
                new[] { "enemy.patrol", "enemy.surveillanceturret", "enemy.flyingdrone", "enemy.surveillanceturret", "enemy.shieldsoldier" }
            };
            for (int wave = 0; wave < 4; wave++)
            {
                Assert.That(flow.CurrentEncounterIndex, Is.EqualTo(wave));
                Assert.That(flow.RemainingEnemies, Is.EqualTo(wave + 2));
                Assert.That(flow.ActiveEnemies.Select(h => h.Owner.Definition.UnitId), Is.EquivalentTo(expectedKinds[wave]));
                Assert.That(all.Count(h => h.gameObject.activeInHierarchy && h.IsAlive), Is.EqualTo(wave + 2));
                Assert.That(goal.IsUnlocked, Is.False);
                if (wave == 3) break;
                var currentRest = flow.CurrentRestArea;
                Assert.That(currentRest.Visited, Is.False);
                health.ApplyDamage(20); yield return null;
                KillRoom(); yield return RequestTransfer(); yield return Until(EncounterPhase.Transferring);
                float before = player.Motor.Position.x; yield return new WaitForFixedUpdate();
                Assert.That(player.Motor.Position.x, Is.GreaterThan(before));
                Assert.That(player.transform.position.x, Is.EqualTo(player.Motor.Position.x).Within(.001f));
                yield return Until(EncounterPhase.Rest);
                AssertRestored(); Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
                Assert.That(player.Motor.Position.x, Is.EqualTo(destinations[wave]).Within(.03f));
                Assert.That(currentRest.Visited, Is.True); Assert.That(goal.IsUnlocked, Is.False);
                health.ApplyDamage(1); yield return null;
                Assert.That(currentRest.TryVisit(health, player.GetComponent<Collider2D>()), Is.False);
                yield return new WaitForSeconds(.4f);
                Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 1));
                Assert.That(all.Count(h => h.gameObject.activeInHierarchy && h.IsAlive), Is.Zero);
                Assert.That(flow.State, Is.EqualTo(StageState.Playing));
                yield return EnterNextRoom();
            }
            Assert.That(flow.CurrentRestArea, Is.Null);
            KillRoom(); yield return null; yield return null;
            Assert.That(goal.IsUnlocked, Is.True);
            float revealStartDeadline = Time.realtimeSinceStartup + 5;
            while (!flow.FinalWaveCutscene.IsRunning && Time.realtimeSinceStartup < revealStartDeadline) yield return null;
            Assert.That(flow.FinalWaveCutscene.IsRunning, Is.True);
            Assert.That(flow.ActiveEnemies.All(enemy => !enemy.gameObject.activeInHierarchy), Is.True,
                "Death animations must finish before the reveal pauses the scene.");
            flow.FinalWaveCutscene.Skip();
            float revealDeadline = Time.realtimeSinceStartup + 3;
            while (flow.FinalWaveCutscene.IsRunning && Time.realtimeSinceStartup < revealDeadline) yield return null;
            Assert.That(flow.FinalWaveCutscene.IsRunning, Is.False);
            Assert.That(flow.CanRequestTransfer, Is.False);
            player.Motor.Teleport(new Vector2(215, .82f)); Physics2D.SyncTransforms(); yield return null; yield return null;
            Assert.That(flow.State, Is.EqualTo(StageState.Completed)); Assert.That(Time.timeScale, Is.Zero);
        }
        [UnityTest] public IEnumerator DisabledLivingEnemyDoesNotCountAsCleared()
        {
            var survivor = flow.ActiveEnemies[0]; survivor.gameObject.SetActive(false);
            foreach (var enemy in flow.ActiveEnemies.Skip(1)) enemy.ApplyDamage(int.MaxValue);
            yield return new WaitForSeconds(.6f);
            Assert.That(flow.RemainingEnemies, Is.EqualTo(1)); Assert.That(flow.Phase, Is.EqualTo(EncounterPhase.Combat));
            survivor.gameObject.SetActive(true); survivor.ApplyDamage(int.MaxValue);
            yield return RequestTransfer();
            yield return Until(EncounterPhase.Rest); AssertRestored();
        }
        [UnityTest] public IEnumerator BlockedArrivalPreservesControlAndNeverRetriesOrHeals()
        {
            var obstruction = new GameObject("Blocked rest destination"); obstruction.AddComponent<BoxCollider2D>().size = new Vector2(3, 3);
            obstruction.transform.position = new Vector3(66, 1, 0); Physics2D.SyncTransforms();
            health.ApplyDamage(10); yield return null;
            LogAssert.Expect(LogType.Error, "Rest transfer destination is blocked or unsupported; player control is preserved.");
            KillRoom(); yield return RequestTransfer(); yield return Until(EncounterPhase.TransferBlocked); Object.Destroy(obstruction);
            yield return new WaitForSeconds(1.5f);
            Assert.That(flow.Phase, Is.EqualTo(EncounterPhase.TransferBlocked)); Assert.That(rest.Visited, Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 10)); AssertRestored();
        }
        [UnityTest] public IEnumerator PauseFreezesTransferAndDisableRestoresBodyWithoutHealing()
        {
            health.ApplyDamage(10); yield return null; KillRoom(); yield return RequestTransfer(); yield return Until(EncounterPhase.Transferring);
            var position = player.Motor.Position; Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(player.Motor.Position, Is.EqualTo(position)); Assert.That(Time.timeScale, Is.Zero);
            player.enabled = false; yield return null;
            Assert.That(flow.Phase, Is.EqualTo(EncounterPhase.TransferBlocked)); AssertRestored();
            Assert.That(player.Motor.Position.x, Is.EqualTo(3f).Within(.1f)); Assert.That(rest.Visited, Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 10)); Time.timeScale = 1;
        }
        [UnityTest] public IEnumerator FailedTransferKeepsGateOpenAndAllowsExplicitShiftRetry()
        {
            var obstruction = new GameObject("Blocked optional transfer");
            obstruction.AddComponent<BoxCollider2D>().size = new Vector2(3, 3);
            obstruction.transform.position = new Vector3(66, 1, 0); Physics2D.SyncTransforms();
            KillRoom();
            LogAssert.Expect(LogType.Error, "Rest transfer destination is blocked or unsupported; player control is preserved.");
            yield return RequestTransfer(); yield return Until(EncounterPhase.TransferBlocked);
            var exit = Object.FindObjectsByType<StageGate>(FindObjectsSortMode.None)
                .Where(g => g.EntryX < rest.NextEntry.EntryX).OrderByDescending(g => g.EntryX).First();
            Assert.That(exit.IsLocked, Is.False);
            Object.Destroy(obstruction); yield return null;
            yield return RequestTransfer(); yield return Until(EncounterPhase.Rest);
            AssertRestored(); Assert.That(rest.Visited, Is.True);
        }
        [UnityTest] public IEnumerator WalkingRecoversFromBlockedTransferWithoutUsingItsArrivalPoint()
        {
            var obstruction = new GameObject("Blocked transfer destination with open walking entrance");
            obstruction.AddComponent<BoxCollider2D>().size = new Vector2(3, 3);
            obstruction.transform.position = new Vector3(66, 1, 0); Physics2D.SyncTransforms();
            health.ApplyDamage(10); yield return null; KillRoom();
            LogAssert.Expect(LogType.Error, "Rest transfer destination is blocked or unsupported; player control is preserved.");
            yield return RequestTransfer(); yield return Until(EncounterPhase.TransferBlocked);
            var exit = Object.FindObjectsByType<StageGate>(FindObjectsSortMode.None)
                .Where(g => g.EntryX < rest.NextEntry.EntryX).OrderByDescending(g => g.EntryX).First();
            player.Motor.Teleport(new Vector2(exit.EntryX - 1.2f, .82f));
            driver.Command = new PlayerCommand { Move = Vector2.right };
            yield return Until(EncounterPhase.Rest);
            driver.Command = default; Object.Destroy(obstruction);
            Assert.That(rest.Visited, Is.True); Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
            Assert.That(player.IsScriptedTransferring || player.IsControlLocked, Is.False);
        }
        [UnityTest] public IEnumerator DeathWinsOverFinalKillAndResetsTheWholeLoop()
        {
            var previous = flow; KillRoom(); health.ApplyDamage(int.MaxValue); yield return null; yield return null;
            Assert.That(flow.State, Is.EqualTo(StageState.Dying)); Assert.That(rest.Visited, Is.False);
            Assert.That(health.RestoreToFull(), Is.Zero);
            float deadline = Time.realtimeSinceStartup + 6;
            while (previous != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(previous == null, Is.True); yield return null;
            var restarted = Object.FindFirstObjectByType<StageFlowController>();
            Assert.That(restarted.CurrentEncounterIndex, Is.Zero); Assert.That(restarted.RemainingEnemies, Is.EqualTo(2));
            Assert.That(restarted.CurrentRestArea.Visited, Is.False);
        }
        [UnityTest] public IEnumerator UndergroundClearCancelsHoldAndCannotCrossTheOriginalFloor()
        {
            foreach (var enemy in flow.ActiveEnemies) { enemy.Owner.Fsm.Stop(); enemy.GetComponent<GroundMovement2D>()?.Stop(); }
            var target = flow.ActiveEnemies.Single(h => h.transform.position.y < 1);
            player.Motor.Teleport(new Vector2(target.transform.position.x - 3, .82f)); yield return new WaitForFixedUpdate();
            Assert.That(player.Glitch.TryExecute(new GlitchRequest { Target = target, Direction = GlitchDirection.Down }, Time.timeAsDouble), Is.True);
            player.Glitch.MoveUnderground(1, 100);
            Assert.That(player.GroundMarkerPosition.x, Is.LessThan(60));
            KillRoom(); yield return RequestTransfer(); yield return Until(EncounterPhase.Rest); AssertRestored();
        }
        [UnityTest] public IEnumerator AirborneSlamClearRestoresNormalMovementAndJump()
        {
            player.Motor.Teleport(new Vector2(10, 10)); player.Combat.ObserveArrival(true);
            Assert.That(player.Combat.TryAttack(null, 1, false, Time.timeAsDouble, false), Is.True);
            KillRoom(); yield return RequestTransfer(); yield return Until(EncounterPhase.Rest); AssertRestored();
            driver.Command = new PlayerCommand { JumpPressed = true, JumpHeld = true }; yield return null;
            yield return new WaitForFixedUpdate(); driver.Command = new PlayerCommand { JumpHeld = true };
            yield return new WaitForFixedUpdate(); Assert.That(player.Motor.Velocity.y, Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator GatesBlockAirborneEscapeAndRetreatDuringNextFight()
        {
            player.Motor.Teleport(new Vector2(59, 10)); driver.Command = new PlayerCommand { Move = Vector2.right };
            yield return new WaitForSeconds(.45f);
            Assert.That(player.GetComponent<Collider2D>().bounds.max.x, Is.LessThan(60.01f));
            driver.Command = default; KillRoom(); yield return RequestTransfer(); yield return Until(EncounterPhase.Rest); yield return EnterNextRoom();
            player.Motor.Teleport(new Vector2(73.2f, 8)); driver.Command = new PlayerCommand { Move = Vector2.left };
            yield return new WaitForSeconds(.45f);
            Assert.That(player.GetComponent<Collider2D>().bounds.min.x, Is.GreaterThan(71.99f));
        }
        [UnityTest] public IEnumerator HealingDoesNotEmitDamageOrReviveDeadUnits()
        {
            int events = 0; health.DamageApplied += _ => events++;
            health.ApplyDamage(17); uint version = health.DamageVersion;
            Assert.That(health.RestoreToFull(), Is.EqualTo(17)); Assert.That(health.RestoreToFull(), Is.Zero);
            Assert.That(health.DamageVersion, Is.EqualTo(version)); Assert.That(events, Is.EqualTo(1));
            var enemy = flow.ActiveEnemies[0]; enemy.ApplyDamage(int.MaxValue);
            Assert.That(enemy.RestoreToFull(), Is.Zero); Assert.That(enemy.CurrentHealth, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator CinematicAndTransferNeverOwnThePlayerTogether()
        {
            var runner = Object.FindFirstObjectByType<ActionPlatformer.Cinematics.CutsceneTrigger>()
                .GetComponentInParent<ActionPlatformer.Cinematics.CutsceneRunner>();
            KillRoom(); Assert.That(runner.TryPlay(), Is.True);
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(player.IsScriptedTransferring, Is.False);
            runner.Skip();
            yield return RequestTransfer();
            yield return Until(EncounterPhase.Transferring);
            Assert.That(runner.TryPlay(), Is.False);
            yield return Until(EncounterPhase.Rest); AssertRestored();
        }

        [UnityTest] public IEnumerator DestinationBlockedDuringTravelRollsBackWithoutConsumingRecovery()
        {
            health.ApplyDamage(10); yield return null; KillRoom(); yield return RequestTransfer(); yield return Until(EncounterPhase.Transferring);
            var obstruction = new GameObject("Destination changed during travel");
            obstruction.AddComponent<BoxCollider2D>().size = new Vector2(3, 3);
            obstruction.transform.position = new Vector3(66, 1, 0); Physics2D.SyncTransforms();
            LogAssert.Expect(LogType.Error, "Rest transfer arrival became invalid; recovery was not consumed.");
            yield return Until(EncounterPhase.TransferBlocked);
            AssertRestored(); Assert.That(rest.Visited, Is.False);
            Assert.That(player.Motor.Position.x, Is.EqualTo(3f).Within(.1f));
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 10));
            Object.Destroy(obstruction);
        }

        [UnityTest] public IEnumerator DeathDuringTransferRestoresPhysicsAndCannotHeal()
        {
            KillRoom(); yield return RequestTransfer(); yield return Until(EncounterPhase.Transferring);
            health.ApplyDamage(int.MaxValue); yield return null; yield return null;
            Assert.That(flow.State, Is.EqualTo(StageState.Dying)); Assert.That(rest.Visited, Is.False);
            Assert.That(health.CurrentHealth, Is.Zero); AssertRestored();
        }

        [UnityTest] public IEnumerator ClearWaitsForGlitchInputAndDoesNotHealBeforeDeparture()
        {
            health.ApplyDamage(10); yield return null; KillRoom();
            yield return new WaitForSeconds(1.2f);
            Assert.That(flow.Phase, Is.EqualTo(EncounterPhase.TransferPending));
            Assert.That(player.Motor.Position.x, Is.EqualTo(3f).Within(.1f));
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 10));
            Assert.That(rest.Visited, Is.False); Assert.That(player.IsControlLocked, Is.False);
            var view = Object.FindFirstObjectByType<StageFlowView>();
            Assert.That(view.IsTransferHintVisible, Is.True);
            yield return RequestTransfer(); yield return Until(EncounterPhase.Rest);
            Assert.That(view.IsTransferHintVisible, Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
        }

        [UnityTest] public IEnumerator FallingDepartureReachesRestWithoutRollback()
        {
            health.ApplyDamage(10); yield return null;
            KillRoom(); yield return new WaitForSeconds(1.2f);
            player.Motor.Teleport(new Vector2(10, 20));
            yield return new WaitForSeconds(.24f);
            Assert.That(player.Motor.Velocity.y, Is.LessThan(-5f));
            yield return RequestTransfer(); yield return Until(EncounterPhase.Rest);
            AssertRestored(); Assert.That(rest.Visited, Is.True);
            Assert.That(player.Motor.Position.x, Is.EqualTo(66).Within(.03f));
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
        }

        [UnityTest] public IEnumerator RisingDepartureReachesRestWithoutRollback()
        {
            health.ApplyDamage(10); yield return null;
            KillRoom(); yield return new WaitForSeconds(1.2f);
            driver.Command = new PlayerCommand { JumpPressed = true, JumpHeld = true, Move = Vector2.right };
            yield return null;
            driver.Command = new PlayerCommand { JumpHeld = true, Move = Vector2.right };
            yield return new WaitForSeconds(.08f);
            Assert.That(player.Motor.Velocity.y, Is.GreaterThan(5f));
            yield return RequestTransfer(); yield return Until(EncounterPhase.Rest);
            AssertRestored(); Assert.That(rest.Visited, Is.True);
            Assert.That(player.Motor.Position.x, Is.EqualTo(66).Within(.03f));
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
        }

        [UnityTest] public IEnumerator CombatAndTooEarlyGlitchPressesAreNotBankedForDeparture()
        {
            driver.Command = new PlayerCommand { GlitchPressed = true }; yield return null; driver.Command = default;
            KillRoom(); yield return Until(EncounterPhase.TransferPending);
            Assert.That(flow.CanRequestTransfer, Is.False);
            driver.Command = new PlayerCommand { GlitchPressed = true }; yield return null; driver.Command = default;
            yield return new WaitForSeconds(.7f);
            Assert.That(flow.Phase, Is.EqualTo(EncounterPhase.TransferPending)); Assert.That(player.IsScriptedTransferring, Is.False);
            yield return RequestTransfer(); yield return Until(EncounterPhase.Rest);
        }
    }
}
