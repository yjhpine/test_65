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
        private const float StageWidth = 60f;
        private StageFlowController flow;
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
            yield return SceneManager.LoadSceneAsync("CoreLoopStage");
            yield return null;
            FindStage();
            yield return new WaitForFixedUpdate();
        }

        private void FindStage()
        {
            flow = Object.FindFirstObjectByType<StageFlowController>();
            Assert.That(flow, Is.Not.Null);
            player = flow.Player;
            goal = Object.FindFirstObjectByType<StageGoal>();
            view = Object.FindFirstObjectByType<StageFlowView>();
            enemies = Object.FindObjectsByType<UnitHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(h => h.gameObject.scene == flow.gameObject.scene && h.Owner != player).ToArray();
            player.SetInputSource(new Driver());
            // These tests isolate gameplay; the cinematic suite covers automatic entry.
            var intro = Object.FindFirstObjectByType<ActionPlatformer.Cinematics.CutsceneTrigger>();
            if (intro != null) intro.enabled = false;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
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

        private IEnumerator AwaitReload(StageFlowController previous)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (previous != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(previous == null, Is.True, "Stage should reload once.");
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
            Assert.That(enemies.All(e => e.IsAlive && e.CurrentHealth == e.MaxHealth), Is.True);
        }

        [UnityTest] public IEnumerator StartsPlayableWithBalancedMixedEnemiesAndLockedExit()
        {
            Assert.That(flow.State, Is.EqualTo(StageState.Playing));
            Assert.That(flow.RemainingEnemies, Is.EqualTo(ExpectedEnemyCount));
            Assert.That(enemies.Length, Is.EqualTo(ExpectedEnemyCount));
            foreach (string id in new[] { "enemy.patrol", "enemy.shieldsoldier", "enemy.flyingdrone", "enemy.surveillanceturret" })
                Assert.That(enemies.Count(e => e.Owner.Definition.UnitId == id), Is.EqualTo(1), id);
            Assert.That(goal.transform.position.x, Is.EqualTo(StageWidth - 2f));
            var floor = flow.gameObject.scene.GetRootGameObjects().Single(g => g.name == "Terrain").transform.Find("Floor");
            Assert.That(floor.GetComponent<BoxCollider2D>().size.x, Is.EqualTo(StageWidth));
            Assert.That(floor.GetComponent<SpriteRenderer>().size.x, Is.EqualTo(StageWidth));
            var ledges = floor.parent.Cast<Transform>().Where(t => t.name.StartsWith("Ledge ")).OrderBy(t => t.position.x).ToArray();
            Assert.That(ledges.Select(t => t.position.x), Is.EqualTo(new[] { 20f, 36f, 52f }));
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
            Assert.That(camera.orthographicSize, Is.EqualTo(6.5f).Within(.001f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(savedFixed).Within(.00001f));
            Assert.That(Time.timeScale, Is.Zero);
        }

        [UnityTest] public IEnumerator DeathAnimationFinishesThenWholeStageResetsTwice()
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                enemies[0].ApplyDamage(enemies[0].MaxHealth);
                var previous = flow;
                var health = player.GetComponent<UnitHealth>();
                health.ApplyDamage(health.MaxHealth);
                yield return null;
                yield return null;
                Assert.That(flow.State, Is.EqualTo(StageState.Dying));
                Assert.That(player.gameObject.activeInHierarchy, Is.True, "Keep the existing death presentation.");
                Assert.That(player.ReactionPresentation.Kind, Is.EqualTo(PlayerReaction.Die));
                yield return AwaitReload(previous);
            }
        }

        [UnityTest] public IEnumerator DeathWinsWhenLethalDamageAndGoalArrivalCoincide()
        {
            KillEnemies();
            EnterGoal();
            player.GetComponent<UnitHealth>().ApplyDamage(int.MaxValue);
            var previous = flow;
            yield return null;
            yield return null;
            Assert.That(flow.State, Is.EqualTo(StageState.Dying));
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
                var previous = flow;
                var button = view.GetComponent<UIDocument>().rootVisualElement.Q<Button>("restart");
                using (var submit = NavigationSubmitEvent.GetPooled()) button.SendEvent(submit);
                Assert.That(flow.State, Is.EqualTo(StageState.Restarting));
                flow.Restart();
                yield return AwaitReload(previous);
                Assert.That(Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
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
            Assert.That(camera.orthographicSize, Is.EqualTo(6.5f).Within(.001f));
        }


        private IEnumerator PrepareUndergroundCamera()
        {
            foreach (var enemy in enemies)
            {
                enemy.Owner.Fsm.Stop();
                enemy.GetComponent<GroundMovement2D>()?.Stop();
            }
            var target = enemies.Single(e => e.Owner.Definition.UnitId == "enemy.patrol");
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
            float halfWidth = 6.5f * camera.aspect;
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

        [UnityTest] public IEnumerator UnloadingCompletedStageRestoresGlobalTime()
        {
            KillEnemies();
            EnterGoal();
            yield return null;
            yield return null;
            Assert.That(Time.timeScale, Is.Zero);
            var blank = SceneManager.CreateScene("AfterCompletedStage");
            SceneManager.SetActiveScene(blank);
            yield return SceneManager.UnloadSceneAsync(flow.gameObject.scene);
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
                var cutscene = Object.FindFirstObjectByType<ActionPlatformer.Cinematics.CutsceneRunner>();
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
