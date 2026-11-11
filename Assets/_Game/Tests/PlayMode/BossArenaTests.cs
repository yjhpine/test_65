using System.Collections;
using ActionPlatformer.Cinematics;
using ActionPlatformer.Flow;
using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    // The boss arena after the third wave: cliff drop, introduction when walking in, fight after the fade, clear on the kill.
    public sealed class BossArenaTests
    {
        private GameManager flow;
        private PlayerUnit player;
        private StageBoss arena;
        private Driver driver;
        private float savedScale, savedFixed;
        private sealed class Driver : IPlayerInputSource { public PlayerCommand Command; public PlayerCommand Sample() => Command; }

        [UnitySetUp] public IEnumerator Setup()
        {
            savedScale = Time.timeScale; savedFixed = Time.fixedDeltaTime; Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("CoreLoopStage");
            yield return null;
            flow = GameManager.Instance; Assert.That(flow.HasStage, Is.True); player = flow.Player;
            driver = new Driver(); player.SetInputSource(driver);
            var intro = Object.FindFirstObjectByType<CutsceneTrigger>();
            if (intro != null) intro.enabled = false;
            arena = Object.FindFirstObjectByType<StageBoss>();
            yield return new WaitForFixedUpdate();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var stage = SceneManager.GetSceneByName("CoreLoopStage");
            var blank = SceneManager.CreateScene("BossArenaCleanup"); SceneManager.SetActiveScene(blank);
            if (stage.isLoaded) yield return SceneManager.UnloadSceneAsync(stage);
            Time.timeScale = savedScale; Time.fixedDeltaTime = savedFixed;
        }

        private void KillRoom() { foreach (var enemy in flow.ActiveEnemies) enemy.ApplyDamage(int.MaxValue); }
        private IEnumerator Until(System.Func<bool> condition, float timeout, string what)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, what);
        }

        // Clears the three waves the way a player does: kill the room, walk (or Shift) into the rest, walk on.
        private IEnumerator ClearAllWaves()
        {
            for (int wave = 0; wave < 2; wave++)
            {
                int next = flow.CurrentEncounterIndex + 1;
                var rest = flow.CurrentRestArea;
                KillRoom();
                yield return Until(() => flow.CanRequestTransfer, 5, "rest transfer available");
                driver.Command = new PlayerCommand { GlitchPressed = true }; yield return null; driver.Command = default;
                yield return Until(() => flow.Phase == EncounterPhase.Rest, 5, "arrived in rest");
                player.Motor.Teleport(new Vector2(rest.NextEntry.EntryX - 1.15f, .82f));
                driver.Command = new PlayerCommand { Move = Vector2.right };
                yield return Until(() => flow.CurrentEncounterIndex == next, 3, "next room started");
                driver.Command = default;
            }
            Assert.That(flow.CurrentEncounterIndex, Is.EqualTo(2));
            KillRoom();
            yield return Until(() => flow.RemainingEnemies == 0, 2, "last wave cleared");
        }

        [UnityTest] public IEnumerator TheBossSleepsAndTheCliffOnlyOpensAfterTheLastWave()
        {
            Assert.That(arena, Is.Not.Null);
            Assert.That(arena.Boss.gameObject.activeSelf, Is.False, "The boss is not awake before the introduction.");
            Assert.That(arena.Started, Is.False);
            Assert.That(flow.Stage.Encounters.Length, Is.EqualTo(3), "Three waves, two rests, then the boss arena.");
            var gate = flow.Stage.Encounters[2].ExitGate;
            Assert.That(gate, Is.Not.Null);
            Assert.That(gate.IsLocked, Is.True, "The last room keeps its exit closed while it is not cleared.");
            yield return ClearAllWaves();
            Assert.That(gate.IsLocked, Is.False, "Clearing the last room opens the way to the cliff.");
            Assert.That(flow.State, Is.EqualTo(StageState.Playing));
        }

        [UnityTest] public IEnumerator WalkingIntoTheArenaPlaysTheIntroThenFadesInToTheFight()
        {
            yield return ClearAllWaves();
            // Over the cliff edge (x 177), then a few steps on the arena floor (trigger x 188..191).
            player.Motor.Teleport(new Vector2(176f, .82f)); Physics2D.SyncTransforms();
            driver.Command = new PlayerCommand { Move = Vector2.right };
            yield return Until(() => arena.Started, 6, "the walk triggers the introduction");
            driver.Command = default;
            Assert.That(player.Motor.Position.y, Is.LessThan(-5f), "The player is down in the arena.");
            Assert.That(arena.Boss.gameObject.activeSelf, Is.True, "The boss wakes for the conversation.");
            var runner = flow.Stage.BossArena == arena ? FindRunner() : null;
            Assert.That(runner, Is.Not.Null);
            CutsceneEndReason? ended = null;
            runner.Finished += reason => ended = reason;
            // Landing wait first, then two confirm-lines: reveal then advance, for both speakers.
            float deadline = Time.realtimeSinceStartup + 15;
            while (runner.IsRunning && Time.realtimeSinceStartup < deadline) { runner.Confirm(); yield return null; }
            Assert.That(ended, Is.EqualTo(CutsceneEndReason.Completed));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "The world runs again once the screen has faded back in.");
            Assert.That(player.IsControlLocked, Is.False);
            var boss = arena.Boss;
            Assert.That(boss.IsAlive && boss.gameObject.activeInHierarchy, Is.True);
            Assert.That(arena.IsDefeated, Is.False);
        }

        [UnityTest] public IEnumerator DefeatingTheBossClearsTheStageAfterItsDeathAnimation()
        {
            yield return ClearAllWaves();
            player.Motor.Teleport(new Vector2(176f, .82f)); Physics2D.SyncTransforms();
            driver.Command = new PlayerCommand { Move = Vector2.right };
            yield return Until(() => arena.Started, 6, "introduction started");
            driver.Command = default;
            var runner = FindRunner();
            float deadline = Time.realtimeSinceStartup + 15;
            while (runner.IsRunning && Time.realtimeSinceStartup < deadline) { runner.Confirm(); yield return null; }
            Assert.That(flow.State, Is.EqualTo(StageState.Playing));
            // Reaching the old exit position must not end the stage any more.
            arena.Boss.ApplyDamage(int.MaxValue);
            yield return null;
            Assert.That(arena.IsDefeated, Is.True);
            Assert.That(flow.State, Is.EqualTo(StageState.Playing), "A short pause lets the death animation play.");
            yield return Until(() => flow.State == StageState.Completed, 4, "the stage completes after the delay");
            Assert.That(Time.timeScale, Is.Zero);
        }

        [UnityTest] public IEnumerator TheCameraFollowsTheDropIntoTheArena()
        {
            var follow = Object.FindFirstObjectByType<StageCameraFollow>();
            float startY = follow.transform.position.y;
            player.Motor.Teleport(new Vector2(185f, -8.18f)); Physics2D.SyncTransforms();
            yield return new WaitForSeconds(1f);
            Assert.That(follow.transform.position.y, Is.LessThan(startY - 5f), "The camera sinks to frame the arena floor.");
            Assert.That(follow.transform.position.y, Is.GreaterThan(startY - 12.1f));
        }

        private static CutsceneRunner FindRunner()
        {
            foreach (var runner in Object.FindObjectsByType<CutsceneRunner>(FindObjectsSortMode.None))
                if (runner.name.StartsWith("BossIntro")) return runner;
            return null;
        }
    }
}
