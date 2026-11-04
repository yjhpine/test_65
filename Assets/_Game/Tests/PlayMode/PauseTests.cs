using System;
using System.Collections;
using System.Linq;
using ActionPlatformer.Cinematics;
using ActionPlatformer.Flow;
using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ActionPlatformer.Tests
{
    public sealed class PauseTests
    {
        private const string CleanupSceneName = "PauseTestCleanup";
        private float savedScale, savedFixed;
        private bool savedBackground;

        // Synthetic devices must reach the game regardless of which Editor window has focus.
        private sealed class InputFocusScope : IDisposable
        {
#if UNITY_EDITOR
            private readonly InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior;
            private readonly InputSettings.EditorInputBehaviorInPlayMode editor = InputSystem.settings.editorInputBehaviorInPlayMode;
#endif
            public InputFocusScope()
            {
#if UNITY_EDITOR
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            }
            public void Dispose()
            {
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = editor;
                InputSystem.settings.backgroundBehavior = background;
#endif
            }
        }

        [SetUp] public void SaveTime()
        {
            savedScale = Time.timeScale;
            savedFixed = Time.fixedDeltaTime;
            savedBackground = Application.runInBackground;
            Application.runInBackground = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            GameManager.Existing?.Resume();
            AudioListener.pause = false;
            var empty = SceneManager.GetSceneByName(CleanupSceneName);
            if (!empty.IsValid()) empty = SceneManager.CreateScene(CleanupSceneName);
            SceneManager.SetActiveScene(empty);
            foreach (string name in new[] { "CoreLoopStage", "MovementLab" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = savedScale;
            Time.fixedDeltaTime = savedFixed;
            Application.runInBackground = savedBackground;
        }

        private static IEnumerator Until(Func<bool> condition, string message, float seconds = 8f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        private static IEnumerator LoadStage()
        {
            var game = GameManager.Instance;
            int version = game.StageVersion;
            yield return SceneManager.LoadSceneAsync("CoreLoopStage");
            yield return Until(() => game.StageVersion > version, "CoreLoopStage should register once.");
            DisableIntro();
        }

        // These tests start cutscenes themselves; walking into the intro must not start one.
        private static void DisableIntro()
        {
            var intro = Object.FindFirstObjectByType<CutsceneTrigger>();
            if (intro != null) intro.enabled = false;
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(target, value);

        // One live enemy as the whole objective, so a test can complete the stage in a frame.
        private static void UseSingleEnemyObjective(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "CoreLoopStage") return;
            var stage = StageLayout.FindIn(scene);
            var enemy = stage.Encounters[0].Enemies[0];
            SetField(stage, "encounters", null);
            SetField(stage, "requiredEnemies", new[] { enemy });
            foreach (var gate in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageGate>(true))) gate.SetLocked(false);
        }

        private static Button MenuButton(GameManager game, string name) =>
            game.PauseMenu.GetComponent<UIDocument>().rootVisualElement.Q<Button>(name);

        // Navigation events go to the focused element, so move focus first as keyboard navigation would.
        private static void Submit(Button button)
        {
            button.Focus();
            Assert.That(button.focusController.focusedElement, Is.SameAs(button), button.name + " should take focus.");
            using (var submit = NavigationSubmitEvent.GetPooled()) button.SendEvent(submit);
        }

        private static void AssertPaused(GameManager game)
        {
            Assert.That(game.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(game.PauseMenu.IsVisible, Is.True);
        }

        private static void AssertResumed(GameManager game, float scale, float fixedStep)
        {
            Assert.That(game.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(scale));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(.00001f));
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(game.PauseMenu.IsVisible, Is.False);
        }

        [UnityTest] public IEnumerator TryPauseStopsTimeAudioAndPlayerInputThenResumeRestoresThem()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            var input = game.Player.GetComponent<PlayerInput>();
            float scale = Time.timeScale, fixedStep = Time.fixedDeltaTime;
            yield return null;
            Assert.That(game.View.IsComboVisible, Is.True, "Precondition: the combo HUD shows during play.");
            Assert.That(game.IsPaused, Is.False);
            Assert.That(game.PauseMenu.IsVisible, Is.False);

            Assert.That(game.TryPause(), Is.True);
            AssertPaused(game);
            Assert.That(input.inputIsActive, Is.False);
            Assert.That(game.TryPause(), Is.True, "Pausing again keeps the pause.");
            for (int i = 0; i < 3; i++) yield return null;
            AssertPaused(game);
            Assert.That(game.View.IsComboVisible, Is.False, "Combat hints hide while paused.");
            Assert.That(game.View.IsGlitchHintVisible, Is.False);

            game.Resume();
            AssertResumed(game, scale, fixedStep);
            Assert.That(input.inputIsActive, Is.True);
            yield return null;
            Assert.That(game.View.IsComboVisible, Is.True);

            // Only what the pause changed is given back.
            AudioListener.pause = true;
            input.DeactivateInput();
            Assert.That(game.TryPause(), Is.True);
            game.Resume();
            Assert.That(AudioListener.pause, Is.True, "An audio pause owned by someone else stays.");
            Assert.That(input.inputIsActive, Is.False, "Input that was already inactive stays inactive.");
            AudioListener.pause = false;
            input.ActivateInput();
        }

        [UnityTest] public IEnumerator EscapeKeyTogglesPause()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            Keyboard keyboard = null;
            Gamepad gamepad = null;
            using (new InputFocusScope())
            {
                try
                {
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    yield return null; yield return null;
                    AssertPaused(game);
                    // Holding the key is one press.
                    yield return null;
                    Assert.That(game.IsPaused, Is.True);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    Assert.That(game.IsPaused, Is.True);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    yield return null; yield return null;
                    AssertResumed(game, 1f, savedFixed);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;

                    gamepad = InputSystem.AddDevice<Gamepad>();
                    InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Start));
                    yield return null; yield return null;
                    AssertPaused(game);
                    InputSystem.QueueStateEvent(gamepad, new GamepadState());
                    yield return null;
                    InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Start));
                    yield return null; yield return null;
                    AssertResumed(game, 1f, savedFixed);
                }
                finally
                {
                    if (gamepad != null) InputSystem.RemoveDevice(gamepad);
                    if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                }
            }
        }

        [UnityTest] public IEnumerator ContinueButtonResumesAndOtherButtonsDoNothing()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            int version = game.StageVersion;
            Assert.That(game.TryPause(), Is.True);
            var root = game.PauseMenu.GetComponent<UIDocument>().rootVisualElement;
            var menu = root.Q("pause-menu");
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.parent, Is.SameAs(root), "The menu is its own section under the shared root.");
            Assert.That(root.Q("stage-hud").parent, Is.SameAs(root), "The stage HUD section stays.");
            Assert.That(root.IndexOf(menu), Is.EqualTo(root.childCount - 1), "The menu draws above the stage HUD.");
            var buttons = new[] { "pause-continue", "pause-options", "pause-save-quit" }.Select(name => MenuButton(game, name)).ToArray();
            Assert.That(buttons.All(b => b != null && b.parent != null && menu.Contains(b)), Is.True);
            Assert.That(buttons.Select(b => b.text), Is.EqualTo(new[] { "계속하기", "옵션", "저장 후 종료" }));
            yield return Until(() => root.panel.focusController.focusedElement == buttons[0], "계속하기 should take focus.", 2f);
            Assert.That(buttons.All(b => b.resolvedStyle.unityFontDefinition.font != null &&
                b.resolvedStyle.unityFontDefinition.font.name == "NotoSansKR-Regular"), Is.True, "Korean labels need the Noto Sans KR font.");

            foreach (var button in buttons.Skip(1))
            {
                Submit(button);
                AssertPaused(game);
            }
            yield return null;
            AssertPaused(game);
            Assert.That(game.StageVersion, Is.EqualTo(version));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CoreLoopStage"));

            Submit(buttons[0]);
            AssertResumed(game, 1f, savedFixed);
            Assert.That(game.Player.GetComponent<PlayerInput>().inputIsActive, Is.True);
            var focused = root.panel.focusController.focusedElement as VisualElement;
            Assert.That(focused == null || !menu.Contains(focused), Is.True, "Hiding the menu releases its focus.");
        }

        [UnityTest] public IEnumerator PauseIsRefusedDuringACutsceneAndOnTheFrameItEnds()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            var runner = Object.FindFirstObjectByType<CutsceneTrigger>().GetComponentInParent<CutsceneRunner>();
            // Without a camera return the skip ends the cutscene in the same frame.
            SetField(runner, "returnDuration", 0f);
            Assert.That(runner.TryPlay(), Is.True);
            yield return Until(() => runner.State == CutsceneState.Waiting, "The cutscene should wait for input.");
            Assert.That(game.TryPause(), Is.False, "A running cutscene refuses the pause.");
            Assert.That(game.IsPaused, Is.False);
            Assert.That(game.PauseMenu.IsVisible, Is.False);
            runner.Skip();
            Assert.That(runner.Coordinator.IsBusy, Is.False, "Precondition: the skip ended the cutscene at once.");
            Assert.That(game.TryPause(), Is.False, "The frame a cutscene ends still refuses the pause.");
            Assert.That(game.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return null;
            yield return null;

            // Esc skips a cutscene; the same press must not also open the menu.
            Keyboard keyboard = null;
            using (new InputFocusScope())
            {
                try
                {
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    Assert.That(runner.TryPlay(), Is.True);
                    yield return Until(() => runner.State == CutsceneState.Waiting, "The cutscene should wait again.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    yield return Until(() => !runner.IsRunning, "Esc should skip the cutscene.", 2f);
                    yield return null; yield return null;
                    Assert.That(game.IsPaused, Is.False, "Skipping with Esc must not pause.");
                    Assert.That(game.PauseMenu.IsVisible, Is.False);
                    Assert.That(Time.timeScale, Is.EqualTo(1f));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    yield return null; yield return null;
                    Assert.That(game.IsPaused, Is.True, "The next Esc pauses.");
                }
                finally
                {
                    if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                }
            }
        }

        [UnityTest] public IEnumerator PauseIsRefusedWhenTheStageIsNotPlaying()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            Assert.That(game.TryPause(), Is.True, "Precondition: a playing stage pauses.");
            game.Resume();
            int version = game.StageVersion;
            SceneManager.sceneLoaded += UseSingleEnemyObjective;
            try
            {
                var health = game.Player.GetComponent<UnitHealth>();
                health.ApplyDamage(health.MaxHealth);
                yield return null; yield return null;
                Assert.That(game.State, Is.EqualTo(StageState.Dying));
                Assert.That(game.TryPause(), Is.False, "A dying player cannot pause.");
                Assert.That(game.IsPaused, Is.False);
                Assert.That(game.PauseMenu.IsVisible, Is.False);
                yield return Until(() => game.StageVersion > version, "The stage should reload after the death.");
                yield return null;
                DisableIntro();

                var enemy = game.ActiveEnemies.Single();
                enemy.ApplyDamage(enemy.MaxHealth);
                var goal = Object.FindFirstObjectByType<StageGoal>();
                game.Player.Motor.Teleport(new Vector2(goal.transform.position.x, .82f));
                Physics2D.SyncTransforms();
                yield return Until(() => game.State == StageState.Completed, "The stage should complete at the goal.");
                Assert.That(game.TryPause(), Is.False, "A completed stage cannot pause.");
                Assert.That(game.IsPaused, Is.False);
                Assert.That(game.PauseMenu.IsVisible, Is.False);
                Assert.That(game.View.IsCompletedVisible, Is.True);
            }
            finally { SceneManager.sceneLoaded -= UseSingleEnemyObjective; }
        }

        [UnityTest] public IEnumerator PausingDuringHitStopRestoresBaseTimeOnResume()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            var feedback = game.Player.Feedback;
            float scale = Time.timeScale, fixedStep = Time.fixedDeltaTime;

            feedback.Play(new PlayerImpact(Vector2.right, PlayerImpactStrength.Heavy), Time.unscaledTimeAsDouble);
            Assert.That(feedback.IsTimeEffectActive, Is.True, "Precondition: a hit stop owns time.");
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(game.TryPause(), Is.True);
            Assert.That(feedback.IsTimeEffectActive, Is.False, "Pausing ends the hit stop first.");
            Assert.That(Time.timeScale, Is.Zero);
            // Longer than the hit stop: the ended effect must not release the pause.
            yield return new WaitForSecondsRealtime(.2f);
            AssertPaused(game);
            game.Resume();
            AssertResumed(game, scale, fixedStep);
            Assert.That(feedback.IsTimeEffectActive, Is.False);

            // Damage slow motion also scales the physics step.
            feedback.PlayDamage(Vector2.left, Time.unscaledTimeAsDouble);
            Assert.That(feedback.IsTimeEffectActive, Is.True, "Precondition: slow motion owns time.");
            Assert.That(Time.timeScale, Is.LessThan(1f));
            Assert.That(Time.fixedDeltaTime, Is.LessThan(fixedStep));
            Assert.That(game.TryPause(), Is.True);
            Assert.That(feedback.IsTimeEffectActive, Is.False);
            yield return new WaitForSecondsRealtime(.3f);
            AssertPaused(game);
            game.Resume();
            AssertResumed(game, scale, fixedStep);
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(scale));
        }

        [UnityTest] public IEnumerator InputPressedWhilePausedDoesNotRunAfterResume()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            var player = game.Player;
            var component = player.GetComponent<PlayerInput>();
            Keyboard keyboard = null;
            Mouse mouse = null;
            using (new InputFocusScope())
            {
                try
                {
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    mouse = InputSystem.AddDevice<Mouse>();
                    component.enabled = false; component.enabled = true;
                    component.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
                    player.SetInputSource(null);
                    yield return Until(() => player.Motor.IsGrounded, "The player should stand on the floor.");
                    Assert.That(game.TryPause(), Is.True);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J, Key.Space));
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    // Pressed again and still held when the game resumes.
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J, Key.Space));
                    yield return null;
                    Assert.That(game.IsPaused, Is.True);
                    game.Resume();
                    for (int i = 0; i < 10; i++)
                    {
                        yield return new WaitForFixedUpdate();
                        Assert.That(player.AttackPhase, Is.EqualTo(PlayerAttackPhase.Ready), "An attack pressed while paused must not start.");
                        Assert.That(player.Motor.Velocity.y, Is.LessThanOrEqualTo(.01f), "A jump pressed while paused must not start.");
                    }

                    // A press after the resume still works.
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
                    yield return Until(() => player.AttackPhase != PlayerAttackPhase.Ready, "A fresh press should attack.", 2f);
                }
                finally
                {
                    if (mouse != null) InputSystem.RemoveDevice(mouse);
                    if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                }
            }
        }

        [UnityTest] public IEnumerator PauseWorksInASceneWithoutStage()
        {
            yield return SceneManager.LoadSceneAsync("MovementLab");
            yield return null;
            var game = GameManager.Instance;
            Assert.That(game.HasStage, Is.False);
            var inputs = PlayerInput.all.Where(i => i.inputIsActive).ToArray();
            Assert.That(inputs, Is.Not.Empty, "Precondition: the lab player has input.");
            Time.timeScale = .5f;
            Time.fixedDeltaTime = .01f;
            float fixedStep = Time.fixedDeltaTime;
            Assert.That(game.TryPause(), Is.True);
            AssertPaused(game);
            Assert.That(inputs.All(i => !i.inputIsActive), Is.True);
            yield return null; yield return null;
            AssertPaused(game);
            game.Resume();
            AssertResumed(game, .5f, fixedStep);
            Assert.That(inputs.All(i => i.inputIsActive), Is.True);
        }

        [UnityTest] public IEnumerator LeavingTheSceneWhilePausedRestoresTime()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            Assert.That(game.TryPause(), Is.True);
            yield return SceneManager.LoadSceneAsync("MovementLab");
            yield return null;
            AssertResumed(game, 1f, savedFixed);
            Assert.That(game.HasStage, Is.False);
            Assert.That(PlayerInput.all.All(i => i.inputIsActive), Is.True);

            // Leaving a scene without a stage also resumes.
            Assert.That(game.TryPause(), Is.True);
            yield return SceneManager.LoadSceneAsync("CoreLoopStage");
            yield return null;
            AssertResumed(game, 1f, savedFixed);
        }

        [UnityTest] public IEnumerator LosingTheStageOrDisablingTheManagerWhilePausedResumes()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            Assert.That(game.TryPause(), Is.True);
            LogAssert.Expect(LogType.Error, GameManager.ConfigurationErrorMessage);
            Object.Destroy(game.ActiveEnemies[0].gameObject);
            yield return null; yield return null;
            Assert.That(game.HasStage, Is.False);
            AssertResumed(game, 1f, savedFixed);

            // No stage is registered now; the scene still pauses, and disabling the manager resumes.
            Assert.That(game.TryPause(), Is.True);
            try
            {
                game.enabled = false;
                AssertResumed(game, 1f, savedFixed);
                Assert.That(PlayerInput.all.All(i => i.inputIsActive), Is.True);
            }
            finally { game.enabled = true; }
            yield return null;
            Assert.That(game.TryPause(), Is.True, "A re-enabled manager pauses again.");
        }

        private static IEnumerator LoadSingleEnemyStageAndClearIt()
        {
            SceneManager.sceneLoaded += UseSingleEnemyObjective;
            try { yield return LoadStage(); }
            finally { SceneManager.sceneLoaded -= UseSingleEnemyObjective; }
            var game = GameManager.Instance;
            var enemy = game.ActiveEnemies.Single();
            enemy.ApplyDamage(enemy.MaxHealth);
            yield return Until(() => game.RemainingEnemies == 0, "The single objective enemy should be cleared.");
            Assert.That(game.State, Is.EqualTo(StageState.Playing));
        }

        private static void EnterGoal(GameManager game)
        {
            var goal = Object.FindFirstObjectByType<StageGoal>();
            game.Player.Motor.Teleport(new Vector2(goal.transform.position.x, .82f));
            Physics2D.SyncTransforms();
        }

        [UnityTest] public IEnumerator PauseRequestedOnTheFrameTheGoalIsReachedIsRefused()
        {
            yield return LoadSingleEnemyStageAndClearIt();
            var game = GameManager.Instance;
            var input = game.Player.GetComponent<PlayerInput>();
            Keyboard keyboard = null;
            using (new InputFocusScope())
            {
                try
                {
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    yield return null;
                    // Esc is down this frame; the player reaches the goal after every Update has run.
                    Assert.That(game.State, Is.EqualTo(StageState.Playing));
                    EnterGoal(game);
                    yield return null;
                    Assert.That(game.State, Is.EqualTo(StageState.Completed));
                    Assert.That(game.IsPaused, Is.False, "The clear resolved first in the same frame, so the pause is refused.");
                    Assert.That(game.PauseMenu.IsVisible, Is.False);
                    game.Resume();
                    Assert.That(Time.timeScale, Is.Zero, "The clear screen keeps the world stopped.");
                    Assert.That(input.inputIsActive, Is.False, "The clear screen keeps player input off.");
                    Assert.That(game.View.IsCompletedVisible, Is.True);
                }
                finally
                {
                    if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                }
            }
        }

        [UnityTest] public IEnumerator StageCompletedDuringThePauseKeepsItsStoppedTimeAndInputOnResume()
        {
            yield return LoadSingleEnemyStageAndClearIt();
            var game = GameManager.Instance;
            var input = game.Player.GetComponent<PlayerInput>();
            Assert.That(game.TryPause(), Is.True);
            EnterGoal(game);
            yield return null;
            Assert.That(game.State, Is.EqualTo(StageState.Completed), "Precondition: the clear resolves under the pause.");
            Assert.That(game.IsPaused, Is.True);
            game.Resume();
            Assert.That(game.IsPaused, Is.False);
            Assert.That(game.PauseMenu.IsVisible, Is.False);
            Assert.That(AudioListener.pause, Is.False, "The pause still gives back the audio it stopped.");
            Assert.That(Time.timeScale, Is.Zero, "The clear owns time now; resuming must not restart the world.");
            Assert.That(input.inputIsActive, Is.False, "The clear owns input now; resuming must not turn it back on.");
            Assert.That(game.View.IsCompletedVisible, Is.True);
        }

        [UnityTest] public IEnumerator CutsceneStartingOnThePauseFrameRefusesThePause()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            var runner = Object.FindFirstObjectByType<CutsceneTrigger>().GetComponentInParent<CutsceneRunner>();
            var input = game.Player.GetComponent<PlayerInput>();
            Keyboard keyboard = null;
            using (new InputFocusScope())
            {
                try
                {
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    yield return null;
                    // A script that runs after this manager's Update (the intro trigger) starts the cutscene this frame.
                    Assert.That(runner.TryPlay(), Is.True);
                    yield return null;
                    Assert.That(game.IsPaused, Is.False, "The cutscene began first in the same frame, so the pause is refused.");
                    Assert.That(game.PauseMenu.IsVisible, Is.False);
                    Assert.That(runner.IsRunning, Is.True);
                    yield return Until(() => runner.State == CutsceneState.Waiting, "The cutscene should wait for input.");
                    Assert.That(Time.timeScale, Is.Zero, "The cutscene owns the stopped time.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    runner.Skip();
                    yield return Until(() => !runner.IsRunning, "The skipped cutscene should end.");
                    Assert.That(game.IsPaused, Is.False);
                    Assert.That(Time.timeScale, Is.EqualTo(1f));
                    Assert.That(input.inputIsActive, Is.True);
                }
                finally
                {
                    if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                }
            }
        }
    }
}
