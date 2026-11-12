using System.Collections;
using System.Linq;
using ActionPlatformer.Flow;
using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ActionPlatformer.Tests
{
    public sealed class StageFlowTests
    {
        private const int ExpectedEnemyCount = 4;
        private const float StageWidth = 217f;
        private GameManager flow;
        private PlayerUnit player;
        private StageGoal goal;
        private StageFlowView view;
        private UnitHealth[] enemies;
        private float savedScale, savedFixed;
        private Scene emptyScene;

        private sealed class Driver : IPlayerInputSource
        {
            public PlayerCommand Command;
            public PlayerCommand Sample() => Command;
        }

        [UnitySetUp] public IEnumerator LoadStage()
        {
            savedScale = Time.timeScale;
            savedFixed = Time.fixedDeltaTime;
            Time.timeScale = 1f;
            // Keep this suite exercising the supported legacy objective path; RestCycleTests cover the authored loop.
            SceneManager.sceneLoaded += UseLegacyObjective;
            yield return SceneManager.LoadSceneAsync("CoreLoopStage");
            yield return null;
            FindStage();
            yield return new WaitForFixedUpdate();
        }

        private static void UseLegacyObjective(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "CoreLoopStage") return;
            LegacyExitStage.Restore(scene);
            var stage = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageLayout>(true)).Single();
            typeof(StageLayout).GetField("encounters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(stage, null);
            // Isolate the original four-kind objective independently of the authored 2/3/4/5 course.
            var authored = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnitHealth>(true))
                .Where(h => h.GetComponent<PlayerUnit>() == null).ToArray();
            string[] kinds = { "enemy.patrol", "enemy.shieldsoldier", "enemy.flyingdrone", "enemy.surveillanceturret" };
            Vector2[] positions = { new Vector2(24, .52f), new Vector2(34, .82f), new Vector2(42, 4), new Vector2(50, .62f) };
            var selected = kinds.Select(id => authored.First(h => h.GetComponent<ActionPlatformer.Units.Unit>().Definition.UnitId == id)).ToArray();
            foreach (var enemy in authored) enemy.gameObject.SetActive(false);
            var root = new GameObject("Legacy Objective Test Enemies");
            for (int i = 0; i < selected.Length; i++)
            {
                selected[i].transform.SetParent(root.transform);
                selected[i].transform.position = positions[i];
                selected[i].gameObject.SetActive(true);
            }
            typeof(StageLayout).GetField("requiredEnemies", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(stage, selected);
            foreach (var gate in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageGate>(true))) gate.SetLocked(false);
        }

        private void FindStage()
        {
            flow = GameManager.Instance;
            Assert.That(flow, Is.Not.Null);
            Assert.That(flow.HasStage, Is.True);
            player = flow.Player;
            goal = Object.FindFirstObjectByType<StageGoal>();
            view = flow.View;
            enemies = flow.ActiveEnemies.ToArray();
            player.SetInputSource(new Driver());
            // These tests isolate gameplay; the cinematic suite covers automatic entry.
            var intro = Object.FindFirstObjectByType<ActionPlatformer.Cinematics.CutsceneTrigger>();
            if (intro != null) intro.enabled = false;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= UseLegacyObjective;
            var stage = SceneManager.GetSceneByName("CoreLoopStage");
            emptyScene = SceneManager.CreateScene("StageFlowTestCleanup");
            SceneManager.SetActiveScene(emptyScene);
            if (stage.IsValid() && stage.isLoaded) yield return SceneManager.UnloadSceneAsync(stage);
            Time.timeScale = savedScale;
            Time.fixedDeltaTime = savedFixed;
        }

        private void KillEnemies()
        {
            foreach (var enemy in enemies) enemy.ApplyDamage(enemy.MaxHealth);
        }

        private void EnterGoal()
        {
            player.Motor.Teleport(new Vector2(goal.transform.position.x, .82f));
            Physics2D.SyncTransforms();
        }

        private IEnumerator AwaitReload(int previousVersion)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (flow.StageVersion <= previousVersion && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(flow.StageVersion, Is.EqualTo(previousVersion + 1), "Stage should reload once.");
            yield return OutGameTestSupport.WaitForTransition(flow);
            yield return null;
            FindStage();
            Assert.That(flow.State, Is.EqualTo(StageState.Playing));
            Assert.That(player.GetComponent<UnitHealth>().CurrentHealth, Is.EqualTo(player.GetComponent<UnitHealth>().MaxHealth));
            Assert.That(flow.RemainingEnemies, Is.EqualTo(ExpectedEnemyCount));
            Assert.That(player.Motor.Position.x, Is.EqualTo(3f).Within(.05f));
            Assert.That(player.AttackPhase, Is.EqualTo(PlayerAttackPhase.Ready));
            Assert.That(player.IsUnderground, Is.False);
            Assert.That(player.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(player.GetComponent<PlayerInput>().inputIsActive, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(savedFixed).Within(.00001f));
            Assert.That(view.IsCompletedVisible, Is.False);
            // The persistent HUD must not keep the previous run's clear screen or disabled button.
            Assert.That(view.ResultItem("restart").enabledInHierarchy, Is.True);
            Assert.That(view.IsFailedVisible, Is.False);
            Assert.That(enemies.All(e => e.IsAlive && e.CurrentHealth == e.MaxHealth), Is.True);
        }

        [UnityTest] public IEnumerator StartsPlayableWithBalancedMixedEnemiesAndLockedExit()
        {
            Assert.That(flow.State, Is.EqualTo(StageState.Playing));
            Assert.That(flow.RemainingEnemies, Is.EqualTo(ExpectedEnemyCount));
            Assert.That(enemies.Length, Is.EqualTo(ExpectedEnemyCount));
            foreach (string id in new[] { "enemy.patrol", "enemy.shieldsoldier", "enemy.flyingdrone", "enemy.surveillanceturret" })
                Assert.That(enemies.Count(e => e.Owner.Definition.UnitId == id), Is.EqualTo(1), id);
            Assert.That(goal.transform.position.x, Is.EqualTo(LegacyExitStage.ExitX));
            var floor = flow.Stage.gameObject.scene.GetRootGameObjects().Single(g => g.name == "Terrain").transform.Find("Floor");
            Assert.That(floor.GetComponent<BoxCollider2D>().size.x, Is.EqualTo(60f));
            Assert.That(floor.GetComponent<SpriteRenderer>().size.x, Is.EqualTo(60f));
            var ledges = floor.parent.Cast<Transform>().Where(t => t.name.StartsWith("Ledge ")).OrderBy(t => t.position.x).ToArray();
            // Authored ledge layout (moved by the user on 2026-11-03).
            Assert.That(ledges.Select(t => t.position.x), Is.EqualTo(new[] { 26.3f, 37.31f, 48.03f }).Within(.001f));
            Assert.That(floor.parent.Find("Right Boundary").position.x, Is.EqualTo(StageWidth + .5f));
            Assert.That(enemies.All(e => e.transform.position.x > 0f && e.transform.position.x < StageWidth), Is.True);
            Assert.That(goal.IsUnlocked, Is.False);
            Assert.That(view.IsCompletedVisible, Is.False);
            var driver = new Driver { Command = new PlayerCommand { Move = Vector2.right } };
            player.SetInputSource(driver);
            float start = player.Motor.Position.x;
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.Motor.Position.x, Is.GreaterThan(start + .2f));
            driver.Command = new PlayerCommand { JumpPressed = true, JumpHeld = true };
            yield return null;
            yield return new WaitForFixedUpdate();
            driver.Command = new PlayerCommand { JumpHeld = true };
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.Motor.Velocity.y, Is.GreaterThan(0f));
        }

        [UnityTest] public IEnumerator LiveDisabledEnemyStillLocksGoalAndFinalKillInsideCompletes()
        {
            enemies[0].gameObject.SetActive(false);
            foreach (var enemy in enemies.Skip(1)) enemy.ApplyDamage(enemy.MaxHealth);
            EnterGoal();
            yield return null;
            yield return null;
            Assert.That(flow.State, Is.EqualTo(StageState.Playing));
            Assert.That(flow.RemainingEnemies, Is.EqualTo(1));
            Assert.That(goal.IsUnlocked, Is.False);
            Assert.That(view.GetComponent<UIDocument>().rootVisualElement.Q<Label>().style.display.value, Is.EqualTo(DisplayStyle.Flex));
            enemies[0].gameObject.SetActive(true);
            enemies[0].ApplyDamage(enemies[0].MaxHealth);
            yield return null;
            yield return null;
            Assert.That(flow.State, Is.EqualTo(StageState.Completed));
            Assert.That(goal.IsUnlocked, Is.True);
            Assert.That(view.IsCompletedVisible, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
        }

        [UnityTest] public IEnumerator ClearingTheLastEnemyBrieflySlowsTime()
        {
            var tuning = (PlayerCombatTuning)typeof(PlayerUnit).GetField("combatTuning",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(player);
            foreach (var enemy in enemies.Skip(1)) enemy.ApplyDamage(enemy.MaxHealth);
            yield return null; yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Only the kill that clears the group slows time.");
            enemies[0].ApplyDamage(enemies[0].MaxHealth);
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(tuning.FinalBlowSlowTimeScale).Within(.001f));
            float until = Time.realtimeSinceStartup + tuning.FinalBlowSlowDuration + tuning.FinalBlowSlowRecovery + .15f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest] public IEnumerator AllKillsRequireArrivalAndCompletionCleansFeedbackAndInput()
        {
            KillEnemies();
            yield return null;
            yield return null;
            Assert.That(goal.IsUnlocked, Is.True);
            Assert.That(flow.State, Is.EqualTo(StageState.Playing));
            var camera = Camera.main;
            player.Feedback.PlayDamage(Vector2.left, Time.unscaledTimeAsDouble);
            player.Feedback.LateTick(Time.unscaledTimeAsDouble);
            Assert.That(Time.timeScale, Is.LessThan(1f));
            EnterGoal();
            yield return null;
            yield return null;
            Assert.That(flow.State, Is.EqualTo(StageState.Completed));
            Assert.That(player.enabled, Is.False);
            Assert.That(player.GetComponent<PlayerInput>().inputIsActive, Is.False);
            Assert.That(player.GetComponent<UnitHealth>().CanReceiveDamage, Is.False);
            Assert.That(player.Motor.Velocity, Is.EqualTo(Vector2.zero));
            Assert.That(player.AttackPhase, Is.EqualTo(PlayerAttackPhase.Ready));
            Assert.That(camera.transform.localPosition, Is.EqualTo(new Vector3(0, 0, -10)));
            Assert.That(camera.orthographicSize, Is.EqualTo(8f).Within(.001f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(savedFixed).Within(.00001f));
            Assert.That(Time.timeScale, Is.Zero);
        }

        [UnityTest] public IEnumerator DeathAnimationFinishesThenWholeStageResetsTwice()
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                enemies[0].ApplyDamage(enemies[0].MaxHealth);
                int previous = flow.StageVersion;
                var health = player.GetComponent<UnitHealth>();
                health.ApplyDamage(health.MaxHealth);
                yield return null;
                yield return null;
                Assert.That(flow.State, Is.EqualTo(StageState.Dying));
                Assert.That(player.gameObject.activeInHierarchy, Is.True, "Keep the existing death presentation.");
                Assert.That(player.ReactionPresentation.Kind, Is.EqualTo(PlayerReaction.Die));
                yield return OutGameTestSupport.RetryAfterDeath(flow);
                yield return AwaitReload(previous);
            }
        }

        [UnityTest] public IEnumerator DeathWinsWhenLethalDamageAndGoalArrivalCoincide()
        {
            KillEnemies();
            EnterGoal();
            player.GetComponent<UnitHealth>().ApplyDamage(int.MaxValue);
            int previous = flow.StageVersion;
            yield return null;
            yield return null;
            Assert.That(flow.State, Is.EqualTo(StageState.Dying));
            Assert.That(view.IsCompletedVisible, Is.False);
            yield return OutGameTestSupport.RetryAfterDeath(flow);
            Assert.That(view.IsCompletedVisible, Is.False);
            yield return AwaitReload(previous);
        }

        [UnityTest] public IEnumerator RestartButtonWorksWhilePausedAndRepeatedRequestsOnlyReloadOnce()
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                KillEnemies();
                EnterGoal();
                yield return null;
                yield return null;
                Assert.That(Time.timeScale, Is.Zero);
                int previous = flow.StageVersion;
                var button = view.ResultItem("restart");
                // Navigation events go to the focused item; with two stages 다음 스테이지 is focused first.
                button.Focus();
                using (var submit = NavigationSubmitEvent.GetPooled()) button.SendEvent(submit);
                Assert.That(flow.Transition.TargetScene, Is.EqualTo("CoreLoopStage"), "다시 플레이 reloads this stage.");
                Assert.That(flow.State, Is.EqualTo(StageState.Restarting));
                flow.Restart();
                yield return AwaitReload(previous);
                Assert.That(Object.FindObjectsByType<StageLayout>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            }
        }

        [UnityTest] public IEnumerator CameraTracksAcrossStageClampsEdgesAndRestoresDamageOffsets()
        {
            var camera = Camera.main;
            var rig = camera.transform.parent;
            float halfWidth = camera.orthographicSize * camera.aspect;
            foreach (float x in new[] { 3f, StageWidth / 2f, StageWidth - 2f })
            {
                player.Motor.Teleport(new Vector2(x, 5f));
                yield return new WaitForFixedUpdate();
                yield return null;
                yield return null;
                Assert.That(rig.position.x, Is.EqualTo(Mathf.Clamp(x, halfWidth, StageWidth - halfWidth)).Within(.03f));
                Assert.That(rig.position.y, Is.EqualTo(3.4f).Within(.001f));
            }
            player.Feedback.PlayDamage(Vector2.right, Time.unscaledTimeAsDouble);
            yield return null;
            player.Motor.Teleport(new Vector2(StageWidth / 2f, 5f));
            yield return new WaitForFixedUpdate();
            yield return null;
            player.Feedback.Reset();
            yield return null;
            yield return null;
            Assert.That(rig.position.x, Is.EqualTo(StageWidth / 2f).Within(.03f));
            Assert.That(camera.transform.localPosition.x, Is.EqualTo(0f).Within(.001f));
            Assert.That(camera.transform.localPosition.y, Is.EqualTo(0f).Within(.001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(8f).Within(.001f));
        }


        private IEnumerator PrepareUndergroundCamera()
        {
            foreach (var enemy in enemies)
            {
                enemy.Owner.Fsm.Stop();
                enemy.GetComponent<GroundMovement2D>()?.Stop();
            }
            var target = enemies.Single(e => e.Owner.Definition.UnitId == "enemy.patrol");
            // Keep the emergence column clear; the authored course now has a low ledge at x=25..28.
            target.GetComponent<Rigidbody2D>().position = new Vector2(22f, .52f);
            Physics2D.SyncTransforms();
            player.Motor.Teleport(new Vector2(target.transform.position.x - 3f, .82f));
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(player.Glitch.TryExecute(new GlitchRequest
                { Target = target, Direction = GlitchDirection.Down }, Time.timeAsDouble), Is.True);
            Assert.That(player.IsUnderground, Is.True);
        }

        private void AssertCameraX(float expected)
        {
            var camera = Camera.main;
            float halfWidth = 8f * camera.aspect;
            Assert.That(camera.transform.parent.position.x,
                Is.EqualTo(Mathf.Clamp(expected, halfWidth, StageWidth - halfWidth)).Within(.03f));
            Assert.That(camera.transform.parent.position.y, Is.EqualTo(3.4f).Within(.001f));
        }

        [UnityTest] public IEnumerator CameraFollowsUndergroundMovementAndReturnsOnCancel()
        {
            yield return PrepareUndergroundCamera();
            Vector2 parked = player.Motor.Position;
            var driver = new Driver();
            player.SetInputSource(driver);
            foreach (float direction in new[] { 1f, -1f })
            {
                driver.Command = new PlayerCommand { Move = Vector2.right * direction };
                yield return null;
                for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
                driver.Command = default;
                yield return null;
                yield return null;
                Assert.That(player.IsUnderground, Is.True);
                Assert.That(Vector2.Distance(player.Motor.Position, parked), Is.LessThan(.001f),
                    "Logical underground movement must not move the parked body.");
                AssertCameraX(player.GroundMarkerPosition.x);
            }
            driver.Command = new PlayerCommand { JumpPressed = true };
            yield return null;
            yield return new WaitForFixedUpdate();
            driver.Command = default;
            yield return null;
            yield return null;
            Assert.That(player.IsUnderground, Is.False);
            AssertCameraX(parked.x);
        }

        [UnityTest] public IEnumerator CameraReturnsToBodyAfterUndergroundTimeout()
        {
            yield return PrepareUndergroundCamera();
            float parkedX = player.Motor.Position.x;
            player.Glitch.MoveUnderground(1f, .2f);
            yield return null;
            yield return null;
            AssertCameraX(player.GroundMarkerPosition.x);
            player.Glitch.Tick(Time.timeAsDouble + 60f);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.That(player.IsUnderground, Is.False);
            AssertCameraX(parkedX);
        }

        [UnityTest] public IEnumerator CameraFollowsEmergedBodyAfterUndergroundMovement()
        {
            yield return PrepareUndergroundCamera();
            player.Glitch.MoveUnderground(1f, .2f);
            float emergedX = player.GroundMarkerPosition.x;
            yield return null;
            yield return null;
            AssertCameraX(emergedX);
            Assert.That(player.Glitch.TryEmerge(), Is.True);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.That(player.IsUnderground, Is.False);
            Assert.That(player.Motor.Position.x, Is.EqualTo(emergedX).Within(.03f));
            AssertCameraX(player.transform.position.x);
        }

        [UnityTest] public IEnumerator AimingAtAnUnusableGlitchTargetShowsItsReasonNearTheMarker()
        {
            foreach (var enemy in enemies)
            {
                enemy.Owner.Fsm.Stop();
                enemy.GetComponent<GroundMovement2D>()?.Stop();
            }
            var target = enemies.Single(e => e.Owner.Definition.UnitId == "enemy.patrol");
            target.GetComponent<Rigidbody2D>().position = new Vector2(22f, .52f);
            Physics2D.SyncTransforms();
            player.Motor.Teleport(new Vector2(12f, .82f));
            var driver = new Driver();
            player.SetInputSource(driver);
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < 10 && !view.IsGlitchHintVisible; i++)
            {
                driver.Command = new PlayerCommand { HasAim = true,
                    AimScreenPosition = player.AimCamera.WorldToScreenPoint(target.GetComponent<Collider2D>().bounds.center) };
                yield return null;
            }
            Assert.That(player.GlitchPreview.Reason, Is.EqualTo(GlitchBlockReason.OutOfRange));
            Assert.That(view.IsGlitchHintVisible, Is.True);
            Assert.That(view.GlitchHintText, Is.EqualTo("OUT OF RANGE"));
            var marker = player.GlitchPreview.Bounds;
            view.ShowGlitchHint(new GlitchPreview { Visible = true, Reason = GlitchBlockReason.Busy, Bounds = marker }, player.AimCamera);
            Assert.That(view.IsGlitchHintVisible, Is.False, "Brief busy states keep only the red marker.");
            view.ShowGlitchHint(new GlitchPreview { Visible = true, Reason = GlitchBlockReason.NoSpace, Bounds = marker }, player.AimCamera);
            Assert.That(view.IsGlitchHintVisible, Is.False, "A blocked destination keeps only the red marker, with no text.");
            driver.Command = default;
            // The stage view refreshes in LateUpdate, after the coroutine resumes in the next frame.
            yield return null; yield return null;
            Assert.That(view.IsGlitchHintVisible, Is.False);
        }

        [UnityTest] public IEnumerator ComboHudShowsLandedStrikesAndTheStrongNext()
        {
            yield return null; yield return null;
            Assert.That(view.IsComboVisible, Is.True, "The stage shows the shared combo while playing.");
            Assert.That(view.ComboPipsLit, Is.Zero);
            view.ShowCombo(2, 3);
            Assert.That(view.ComboPipsLit, Is.EqualTo(2));
            Assert.That(view.IsStrongNextVisible, Is.True);
            view.ShowCombo(1, 3);
            Assert.That(view.IsStrongNextVisible, Is.False);
            view.ShowCombo(0, 1);
            Assert.That(view.IsComboVisible, Is.False, "Without a combo there is nothing to show.");
        }

        [UnityTest] public IEnumerator UnloadingCompletedStageRestoresGlobalTime()
        {
            KillEnemies();
            EnterGoal();
            yield return null;
            yield return null;
            Assert.That(Time.timeScale, Is.Zero);
            var stage = flow.Stage.gameObject.scene;
            var blank = SceneManager.CreateScene("AfterCompletedStage");
            SceneManager.SetActiveScene(blank);
            yield return SceneManager.UnloadSceneAsync(stage);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(savedFixed).Within(.00001f));
        }

        [UnityTest] public IEnumerator MovementAndRealAttacksTraverseCompactPatrolCourse()
        {
            var intro = Object.FindFirstObjectByType<ActionPlatformer.Cinematics.CutsceneTrigger>();
            if (intro != null) intro.enabled = true;
            // Keep the original movement/melee scenario focused on patrols; mixed enemy mechanics have their own tests.
            foreach (var enemy in enemies.Where(e => e.Owner.Definition.UnitId != "enemy.patrol"))
                enemy.ApplyDamage(enemy.MaxHealth);
            var driver = new Driver();
            player.SetInputSource(driver);
            float deadline = Time.realtimeSinceStartup + 120f;
            while (flow.State == StageState.Playing && Time.realtimeSinceStartup < deadline)
            {
                var cutscene = Object.FindFirstObjectByType<ActionPlatformer.Cinematics.CutsceneCoordinator>().Active;
                if (cutscene != null && cutscene.IsRunning) { cutscene.Skip(); yield return null; continue; }
                var closest = enemies.Where(e => e.IsAlive)
                    .OrderBy(e => Mathf.Abs(e.transform.position.x - player.Motor.Position.x)).FirstOrDefault();
                float destination = closest != null ? closest.transform.position.x : goal.transform.position.x;
                float distance = destination - player.Motor.Position.x;
                bool close = closest != null && Mathf.Abs(distance) < .9f;
                driver.Command = new PlayerCommand
                {
                    Move = close ? Vector2.zero : new Vector2(Mathf.Sign(distance), 0),
                    AttackPressed = close
                };
                yield return null;
            }
            Assert.That(flow.State, Is.EqualTo(StageState.Completed), $"Full clear failed: player={player.Motor.Position}, facing={player.Facing}, phase={player.AttackPhase}, health={player.GetComponent<UnitHealth>().CurrentHealth}, scale={Time.timeScale}; enemies=" + string.Join(";", enemies.Select(e => $"{e.name}:{e.CurrentHealth}@{e.transform.position}")));
            Assert.That(enemies.All(e => !e.IsAlive), Is.True);
            Assert.That(view.IsCompletedVisible, Is.True);
        }
    }
}
