using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ActionPlatformer.Cinematics;
using ActionPlatformer.Flow;
using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ActionPlatformer.Tests
{
    public sealed class FinalWaveCutsceneTests
    {
        private StageFlowController flow;
        private CutsceneRunner reveal;
        private PlayerUnit player;
        private StageGoal goal;
        private float scale, fixedDelta;
        private sealed class NoInput : IPlayerInputSource { public PlayerCommand Sample() => default; }

        // RestCycleTests exercise all four authored encounters. Isolate the final encounter here.
        private static void IsolateFinalWave(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "CoreLoopStage") return;
            var stage = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StageFlowController>(true)).Single();
            var field = typeof(StageFlowController).GetField("encounters", BindingFlags.Instance | BindingFlags.NonPublic);
            var encounters = (StageEncounter[])field.GetValue(stage);
            foreach (var room in encounters) room.EnemyRoot.SetActive(false);
            field.SetValue(stage, new[] { encounters[encounters.Length - 1] });
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            scale = Time.timeScale; fixedDelta = Time.fixedDeltaTime; Time.timeScale = 1;
            SceneManager.sceneLoaded += IsolateFinalWave;
            yield return SceneManager.LoadSceneAsync("CoreLoopStage"); yield return null;
            FindStage();
            player.Motor.Teleport(new Vector2(191, .82f));
            yield return new WaitForFixedUpdate(); yield return null;
        }
        private void FindStage()
        {
            flow = Object.FindFirstObjectByType<StageFlowController>(); reveal = flow.FinalWaveCutscene;
            player = flow.Player; goal = Object.FindFirstObjectByType<StageGoal>();
            player.SetInputSource(new NoInput());
            Object.FindFirstObjectByType<CutsceneTrigger>().enabled = false;
            foreach (var enemy in flow.ActiveEnemies) { enemy.Owner.Fsm.Stop(); enemy.GetComponent<GroundMovement2D>()?.Stop(); }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= IsolateFinalWave;
            if (reveal != null) reveal.Cancel();
            var stage = SceneManager.GetSceneByName("CoreLoopStage");
            var empty = SceneManager.CreateScene("FinalWaveCutsceneCleanup"); SceneManager.SetActiveScene(empty);
            if (stage.isLoaded) yield return SceneManager.UnloadSceneAsync(stage);
            Time.timeScale = scale; Time.fixedDeltaTime = fixedDelta;
        }
        private void KillAll() { foreach (var enemy in flow.ActiveEnemies) enemy.ApplyDamage(int.MaxValue); }
        private IEnumerator Until(Func<bool> condition, float timeout = 7)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Final-wave cutscene timed out.");
        }
        private void AssertRestored()
        {
            Assert.That(reveal.IsRunning || player.IsControlLocked || reveal.Coordinator.IsBusy, Is.False);
            Assert.That(player.GetComponent<PlayerInput>().inputIsActive, Is.True);
            Assert.That(Object.FindFirstObjectByType<StageCameraFollow>().enabled, Is.True);
            Assert.That(reveal.Coordinator.CameraRig.IsAcquired, Is.False);
            Assert.That(Camera.main.orthographicSize, Is.EqualTo(8f).Within(.01f));
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedDelta));
        }

        [UnityTest] public IEnumerator AirborneLastKillRevealsDoorThenReturnsToCurrentPlayerAndSpeaksOnce()
        {
            foreach (var enemy in flow.ActiveEnemies.Skip(1)) enemy.ApplyDamage(int.MaxValue);
            yield return new WaitForSeconds(.15f);
            Assert.That(reveal.IsRunning, Is.False); Assert.That(goal.IsUnlocked, Is.False);
            player.Motor.Teleport(new Vector2(191, 7)); yield return new WaitForFixedUpdate(); yield return null;
            KillAll(); yield return Until(() => reveal.IsRunning);
            Assert.That(goal.IsUnlocked, Is.True); Assert.That(player.IsControlLocked, Is.True);
            Assert.That(Time.timeScale, Is.Zero); Assert.That(flow.State, Is.EqualTo(StageState.Playing));
            yield return Until(() => reveal.CurrentTime >= .85); yield return null;
            Assert.That(Camera.main.transform.position.x, Is.EqualTo(goal.transform.position.x).Within(.1f));
            Assert.That(Camera.main.orthographicSize, Is.EqualTo(3.2f).Within(.05f));
            Assert.That(reveal.View.IsDialogueVisible, Is.False);
            yield return Until(() => reveal.CurrentTime >= 3); yield return null;
            Assert.That(reveal.View.IsBubbleVisible, Is.True);
            Assert.That(reveal.View.VisibleText, Is.EqualTo("문이 열렸다. 어서 탈출하자."));
            Assert.That(reveal.CurrentSpeaker, Is.SameAs(player.GetComponent<DialogueSpeaker>()));
            Assert.That(Camera.main.transform.position.x, Is.EqualTo(player.transform.position.x).Within(.1f));
            Assert.That(Camera.main.transform.position.y, Is.EqualTo(player.transform.position.y + 1.1f).Within(.1f));
            Assert.That(Camera.main.orthographicSize, Is.EqualTo(4.5f).Within(.05f));
            yield return Until(() => !reveal.IsRunning); AssertRestored();
            yield return new WaitForSeconds(.2f);
            Assert.That(reveal.IsRunning, Is.False); Assert.That(flow.State, Is.EqualTo(StageState.Playing));
        }

        [UnityTest] public IEnumerator AlreadyInsideGoalStillPlaysRevealBeforeCompleting()
        {
            player.Motor.Teleport(new Vector2(215, .82f)); yield return new WaitForFixedUpdate();
            KillAll(); yield return Until(() => reveal.IsRunning);
            Assert.That(flow.State, Is.EqualTo(StageState.Playing));
            Assert.That(Object.FindFirstObjectByType<StageFlowView>().IsCompletedVisible, Is.False);
            reveal.Skip(); yield return Until(() => flow.State == StageState.Completed);
            Assert.That(reveal.IsRunning, Is.False);
            Assert.That(Object.FindFirstObjectByType<StageFlowView>().IsCompletedVisible, Is.True);
        }

        [UnityTest] public IEnumerator DisabledRevealReleasesControlAndDoesNotLoop()
        {
            KillAll(); yield return Until(() => reveal.IsRunning);
            yield return Until(() => reveal.CurrentTime >= .5);
            reveal.enabled = false; yield return null; AssertRestored();
            reveal.enabled = true; yield return new WaitForSeconds(.3f);
            Assert.That(reveal.IsRunning, Is.False); Assert.That(goal.IsUnlocked, Is.True);
        }

        [UnityTest] public IEnumerator ExternalPauseDefersRevealUntilResume()
        {
            KillAll(); Time.timeScale = 0; yield return new WaitForSecondsRealtime(.2f);
            Assert.That(reveal.IsRunning, Is.False); Assert.That(Time.timeScale, Is.Zero);
            Time.timeScale = 1; yield return Until(() => reveal.IsRunning);
            reveal.Skip(); yield return Until(() => !reveal.IsRunning); AssertRestored();
        }

        [UnityTest] public IEnumerator SimultaneousLethalDamageWinsAndRestartCanRevealAgain()
        {
            var previous = flow; KillAll(); player.GetComponent<UnitHealth>().ApplyDamage(int.MaxValue);
            yield return null; yield return null;
            Assert.That(flow.State, Is.EqualTo(StageState.Dying)); Assert.That(reveal.IsRunning, Is.False);
            yield return Until(() => previous == null); yield return null; FindStage();
            Assert.That(reveal.IsRunning, Is.False); Assert.That(goal.IsUnlocked, Is.False);
            KillAll(); yield return Until(() => reveal.IsRunning);
        }
    }
}
