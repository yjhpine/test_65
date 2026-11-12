using System;
using System.Collections;
using System.Linq;
using ActionPlatformer.Cinematics;
using ActionPlatformer.Flow;
using ActionPlatformer.Player;
using ActionPlatformer.Presentation;
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
    // Planning doc "치지직 아웃게임 기능 기획서" v0.1 section 10 checks, with rest areas as the save and retry points.
    public sealed class OutGameFlowTests
    {
        private const string CleanupSceneName = "OutGameCleanup";
        private GameManager game;
        private MemorySaveStorage storage;
        private float savedScale, savedFixed;
        private bool savedBackground;
        private sealed class Driver : IPlayerInputSource { public PlayerCommand Command; public PlayerCommand Sample() => Command; }

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
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editor;
#endif
            }
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            savedScale = Time.timeScale; savedFixed = Time.fixedDeltaTime; savedBackground = Application.runInBackground;
            Application.runInBackground = true;
            Time.timeScale = 1f;
            game = GameManager.Instance;
            storage = OutGameTestSupport.FreshSaves(game);
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            game.Resume();
            game.KeyBindings?.Cancel();
            var empty = SceneManager.GetSceneByName(CleanupSceneName);
            if (!empty.IsValid()) empty = SceneManager.CreateScene(CleanupSceneName);
            SceneManager.SetActiveScene(empty);
            foreach (string name in new[] { "Title", "CoreLoopStage" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            game.Transition.Cancel();
            game.OutGame.Stack.CloseAll();
            game.OutGame.SetInteractable(true);
            game.OutGame.SetCover(0f, false);
            OutGameTestSupport.FreshSaves(game);
            Time.timeScale = savedScale; Time.fixedDeltaTime = savedFixed;
            Application.runInBackground = savedBackground;
        }

        private static IEnumerator Until(Func<bool> condition, string message, float seconds = 8f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        private IEnumerator LoadTitle()
        {
            yield return SceneManager.LoadSceneAsync("Title");
            yield return Until(() => game.IsTitle && game.OutGame.IsOpen(game.OutGame.Title), "The title menu should open.");
        }

        private IEnumerator LoadStage()
        {
            int version = game.StageVersion;
            yield return SceneManager.LoadSceneAsync("CoreLoopStage");
            yield return Until(() => game.StageVersion > version, "CoreLoopStage should register.");
            DisableIntro();
        }

        private static void DisableIntro()
        {
            var intro = Object.FindFirstObjectByType<CutsceneTrigger>();
            if (intro != null) intro.enabled = false;
        }

        private IEnumerator AwaitStage(int previousVersion)
        {
            yield return Until(() => game.StageVersion > previousVersion && game.HasStage, "The stage should load.");
            yield return OutGameTestSupport.WaitForTransition(game);
            DisableIntro();
            yield return null;
        }

        private void StoreProgress(int checkpoint, bool cleared = false)
        {
            var data = ProgressRules.NewGame(game.Catalog);
            data.mainCheckpoint = checkpoint;
            if (cleared) ProgressRules.OnStageCleared(data, game.Catalog, data.mainStage);
            storage.Set(OutGameSaves.ProgressName, data.ToJson());
            game.ReloadSaves(storage);
        }

        private static void KillRoom(GameManager g) { foreach (var enemy in g.ActiveEnemies) enemy.ApplyDamage(int.MaxValue); }

        // Clears the current room and walks into the rest area after it.
        private IEnumerator WalkIntoRest(Driver driver)
        {
            var area = game.CurrentRestArea;
            KillRoom(game);
            yield return Until(() => game.Phase == EncounterPhase.TransferPending, "The room should clear.");
            var exit = Object.FindObjectsByType<StageGate>(FindObjectsSortMode.None)
                .Where(g => g.EntryX < area.NextEntry.EntryX).OrderByDescending(g => g.EntryX).First();
            game.Player.Motor.Teleport(new Vector2(exit.EntryX - 1.2f, .82f));
            driver.Command = new PlayerCommand { Move = Vector2.right };
            yield return Until(() => game.Phase == EncounterPhase.Rest, "The player should walk into the rest area.");
            driver.Command = default;
        }

        [UnityTest] public IEnumerator TitleWithoutSavesOffersNewGameAndEscNeverPausesOrQuits()
        {
            yield return LoadTitle();
            var title = game.OutGame.Title;
            Assert.That(TitlePanel.Logo, Is.EqualTo("치지직"));
            Assert.That(title.Continue.Available, Is.False);
            Assert.That(title.Continue.UnavailableReason, Is.EqualTo(ProgressRules.NoRecordReason));
            Assert.That(title.StageSelect.resolvedStyle.display == DisplayStyle.None || title.StageSelect.style.display == DisplayStyle.None,
                Is.True, "A one-stage catalog hides the stage list.");
            yield return Until(() => title.Focused == title.NewGame, "새 게임 is selected first when nothing can be continued.", 2f);
            title.Continue.Activate();
            Assert.That(game.Transition.IsBusy, Is.False, "An unavailable item does nothing.");
            Assert.That(title.HintText, Is.EqualTo(ProgressRules.NoRecordReason), "It explains why instead.");
            Assert.That(game.OutGame.HandleBack(), Is.True, "Esc on the title is used up...");
            Assert.That(game.OutGame.IsOpen(title), Is.True, "...without closing the main menu.");
            Assert.That(game.TryPause(), Is.False);
            Assert.That(game.IsPaused, Is.False);
            title.Move(1);
            Assert.That(title.Focused, Is.Not.SameAs(title.NewGame), "Arrow keys move the selection.");
        }

        [UnityTest] public IEnumerator NewGameEntersTheFirstStageOnceAndSavesItsStart()
        {
            yield return LoadTitle();
            int version = game.StageVersion, started = game.Transition.Started;
            game.OutGame.Title.NewGame.Activate();
            game.OutGame.Title.NewGame.Activate();
            Assert.That(game.Transition.Started, Is.EqualTo(started + 1), "A second click during the change is ignored.");
            Assert.That(game.OutGame.IsInteractable, Is.False);
            yield return Until(() => game.StageVersion > version, "The first stage should load.");
            Assert.That(game.Transition.IsBusy, Is.True, "The cover is still lifting.");
            Assert.That(game.Player.GetComponent<PlayerInput>().inputIsActive, Is.False, "Play input waits for the cover.");
            yield return OutGameTestSupport.WaitForTransition(game);
            Assert.That(game.StageVersion, Is.EqualTo(version + 1));
            Assert.That(game.Player.GetComponent<PlayerInput>().inputIsActive, Is.True);
            Assert.That(game.StageId, Is.EqualTo("stage1"));
            Assert.That(game.ResumedCheckpoint, Is.EqualTo(ProgressData.NoCheckpoint));
            Assert.That(game.IsTitle, Is.False);
            Assert.That(game.OutGame.HasOpenPanel, Is.False);
            Assert.That(storage.Entries.ContainsKey(OutGameSaves.ProgressName), Is.True);
            var saved = ProgressData.FromJson(storage.Entries[OutGameSaves.ProgressName], game.Catalog);
            Assert.That(saved.mainStage, Is.EqualTo("stage1"));
            Assert.That(saved.mainCheckpoint, Is.EqualTo(ProgressData.NoCheckpoint));
        }

        [UnityTest] public IEnumerator NewGameOverExistingProgressAsksAndCancelKeepsEverything()
        {
            StoreProgress(0);
            storage.Set(OutGameSaves.SettingsName, new SettingsData { masterVolume = 30 }.ToJson());
            game.ReloadSaves(storage);
            yield return LoadTitle();
            var title = game.OutGame.Title;
            Assert.That(title.Continue.Available, Is.True);
            Assert.That(title.Continue.Detail, Does.Contain("휴식 구역 1부터"));
            yield return Until(() => title.Focused == title.Continue, "이어하기 is selected first when it is available.", 2f);
            string before = storage.Entries[OutGameSaves.ProgressName];
            title.NewGame.Activate();
            var dialog = game.OutGame.Dialog;
            Assert.That(game.OutGame.IsOpen(dialog), Is.True);
            Assert.That(dialog.Message, Is.EqualTo(GameManager.NewGameQuestion));
            yield return Until(() => dialog.Focused?.name == "new-game-cancel", "The question selects 취소 first.", 2f);
            Assert.That(game.OutGame.HandleBack(), Is.True, "Esc answers 취소.");
            Assert.That(game.OutGame.IsOpen(dialog), Is.False);
            Assert.That(game.Transition.IsBusy, Is.False);
            Assert.That(storage.Entries[OutGameSaves.ProgressName], Is.EqualTo(before));
            Assert.That(game.Saves.Settings.masterVolume, Is.EqualTo(30));
        }

        [UnityTest] public IEnumerator RestAreaIsTheCheckpointForRetry()
        {
            yield return LoadStage();
            var driver = new Driver();
            game.Player.SetInputSource(driver);
            yield return new WaitForFixedUpdate();
            yield return WalkIntoRest(driver);
            Assert.That(game.Saves.Progress.mainCheckpoint, Is.EqualTo(0), "Entering rest area 1 saves it.");
            Assert.That(ProgressData.FromJson(storage.Entries[OutGameSaves.ProgressName], game.Catalog).mainCheckpoint, Is.EqualTo(0));

            int version = game.StageVersion;
            var health = game.Player.GetComponent<UnitHealth>();
            health.ApplyDamage(health.MaxHealth);
            yield return Until(() => game.State == StageState.Failed, "The failure screen should follow the death.");
            Assert.That(Time.timeScale, Is.Zero, "The world stops behind the failure screen.");
            Assert.That(game.View.ResultItem("fail-retry").Detail, Is.EqualTo("휴식 구역 1부터"), "The retry button says where it restarts.");
            yield return Until(() => game.View.ResultItem("fail-retry").focusController?.focusedElement == game.View.ResultItem("fail-retry"),
                "재도전 is selected first.", 2f);
            Assert.That(game.TryPause(), Is.False);
            game.View.ResultItem("fail-retry").Activate();
            yield return AwaitStage(version);

            Assert.That(game.ResumedCheckpoint, Is.EqualTo(0));
            Assert.That(game.Phase, Is.EqualTo(EncounterPhase.Rest));
            Assert.That(game.CurrentEncounterIndex, Is.Zero);
            Assert.That(game.RemainingEnemies, Is.Zero);
            Assert.That(game.CurrentRestArea.Visited, Is.True);
            Assert.That(game.CurrentRestArea.NextEntry.IsLocked, Is.False);
            Assert.That(game.ActiveEnemies.All(e => !e.gameObject.activeInHierarchy), Is.True, "Room 1 stays cleared.");
            Assert.That(game.Player.Motor.Position.x, Is.InRange(60f, 72f), "The player starts in rest area 1.");
            Assert.That(game.Player.GetComponent<UnitHealth>().CurrentHealth, Is.EqualTo(game.Player.GetComponent<UnitHealth>().MaxHealth));
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            // The next room still starts when the player walks through the open entry.
            driver = new Driver();
            game.Player.SetInputSource(driver);
            var rest = game.CurrentRestArea;
            game.Player.Motor.Teleport(new Vector2(rest.NextEntry.EntryX - 1.15f, .82f));
            driver.Command = new PlayerCommand { Move = Vector2.right };
            yield return Until(() => game.CurrentEncounterIndex == 1, "Walking on enters room 2.", 4f);
            driver.Command = default;
            Assert.That(game.Phase, Is.EqualTo(EncounterPhase.Combat));
            Assert.That(game.RemainingEnemies, Is.GreaterThan(0));
            Assert.That(rest.NextEntry.IsLocked, Is.True);
        }

        [UnityTest] public IEnumerator ContinueFromTitleResumesAtTheSavedRestArea()
        {
            StoreProgress(1);
            yield return LoadTitle();
            int version = game.StageVersion;
            game.OutGame.Title.Continue.Activate();
            yield return AwaitStage(version);
            Assert.That(game.ResumedCheckpoint, Is.EqualTo(1));
            Assert.That(game.CurrentEncounterIndex, Is.EqualTo(1));
            Assert.That(game.Phase, Is.EqualTo(EncounterPhase.Rest));
            Assert.That(game.Player.Motor.Position.x, Is.InRange(110f, 122f), "The player starts in rest area 2.");
            Assert.That(game.Saves.Progress.mainCheckpoint, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator PauseRestartAsksFirstAndStartsTheStageOver()
        {
            StoreProgress(0);
            yield return LoadTitle();
            int version = game.StageVersion;
            game.ContinueGame();
            yield return AwaitStage(version);
            Assert.That(game.TryPause(), Is.True);
            game.PauseMenu.Item(PauseMenuView.RestartName).Activate();
            var dialog = game.OutGame.Dialog;
            Assert.That(game.OutGame.IsOpen(dialog), Is.True);
            Assert.That(dialog.Message, Does.Contain("저장된 휴식 구역 기록"));
            yield return Until(() => dialog.Focused?.name == "restart-cancel", "The question selects 취소 first.", 2f);
            Assert.That(game.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero, "The question keeps the pause.");
            dialog.Item("restart-confirm").Activate();
            version = game.StageVersion;
            yield return AwaitStage(version);
            Assert.That(game.ResumedCheckpoint, Is.EqualTo(ProgressData.NoCheckpoint));
            Assert.That(game.CurrentEncounterIndex, Is.Zero);
            Assert.That(game.Phase, Is.EqualTo(EncounterPhase.Combat));
            Assert.That(game.Saves.Progress.mainCheckpoint, Is.EqualTo(ProgressData.NoCheckpoint), "Restart starts this attempt over.");
            Assert.That(game.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest] public IEnumerator PauseToTitleSaysWhatIsKeptAndReturnsToTheMenu()
        {
            StoreProgress(0);
            yield return LoadTitle();
            int version = game.StageVersion;
            game.ContinueGame();
            yield return AwaitStage(version);
            Assert.That(game.TryPause(), Is.True);
            game.PauseMenu.Item(PauseMenuView.TitleName).Activate();
            var dialog = game.OutGame.Dialog;
            Assert.That(dialog.Message, Does.StartWith("마지막 휴식 구역 이후의 진행은 저장되지 않습니다."));
            Assert.That(dialog.Message, Does.Not.Contain("이어하기는"));
            Assert.That(game.OutGame.HandleBack(), Is.True);
            Assert.That(game.PauseMenu.IsVisible, Is.True, "취소 returns to the pause menu.");
            game.PauseMenu.Item(PauseMenuView.TitleName).Activate();
            game.OutGame.Dialog.Item("title-confirm").Activate();
            yield return Until(() => game.IsTitle, "The title should load.");
            yield return OutGameTestSupport.WaitForTransition(game);
            Assert.That(game.HasStage, Is.False);
            Assert.That(game.IsPaused, Is.False);
            Assert.That(game.OutGame.IsOpen(game.OutGame.Title), Is.True);
            Assert.That(game.OutGame.Title.Continue.Available, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        private static void UseSingleEnemyObjective(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "CoreLoopStage") return;
            LegacyExitStage.Restore(scene);
            var stage = StageLayout.FindIn(scene);
            var enemy = stage.Encounters[0].Enemies[0];
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(StageLayout).GetField("encounters", flags).SetValue(stage, null);
            typeof(StageLayout).GetField("requiredEnemies", flags).SetValue(stage, new[] { enemy });
            foreach (var gate in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageGate>(true))) gate.SetLocked(false);
        }

        [UnityTest] public IEnumerator ClearingTheOnlyStageIsTheFinalClearAndEndsContinue()
        {
            SceneManager.sceneLoaded += UseSingleEnemyObjective;
            try { yield return LoadStage(); }
            finally { SceneManager.sceneLoaded -= UseSingleEnemyObjective; }
            var enemy = game.ActiveEnemies.Single();
            enemy.ApplyDamage(enemy.MaxHealth);
            var goal = Object.FindFirstObjectByType<StageGoal>();
            game.Player.Motor.Teleport(new Vector2(goal.transform.position.x, .82f));
            Physics2D.SyncTransforms();
            yield return Until(() => game.State == StageState.Completed, "The stage should clear at the goal.");
            Assert.That(game.View.Result, Is.EqualTo(StageFlowView.ResultKind.FinalClear));
            Assert.That(game.View.ResultHeading, Is.EqualTo("ALL CLEAR"));
            Assert.That(game.View.ResultItem("result-next").style.display, Is.EqualTo(new StyleEnum<DisplayStyle>(DisplayStyle.None)),
                "There is no next stage after the last one.");
            Assert.That(game.View.ResultItem("restart").Text, Is.EqualTo("마지막 스테이지 다시 플레이"));
            Assert.That(game.Saves.Progress.allCleared, Is.True);
            Assert.That(game.Saves.CanContinue(out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(ProgressRules.AllClearedReason));
            game.NextStage();
            Assert.That(game.Transition.IsBusy, Is.False, "No stage follows the last one.");

            game.View.ResultItem("result-title").Activate();
            yield return Until(() => game.IsTitle, "The title should load.");
            yield return OutGameTestSupport.WaitForTransition(game);
            Assert.That(game.OutGame.Title.Continue.UnavailableReason, Is.EqualTo(ProgressRules.AllClearedReason));
        }

        // Planning doc 7.3: after a failed save, leaving for the title explains the loss again and can retry the save.
        [UnityTest] public IEnumerator ResultScreenTitleAsksAgainAfterAFailedSaveAndCanRetryIt()
        {
            SceneManager.sceneLoaded += UseSingleEnemyObjective;
            try { yield return LoadStage(); }
            finally { SceneManager.sceneLoaded -= UseSingleEnemyObjective; }
            storage.FailWrites = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Progress could not be saved"));
            var enemy = game.ActiveEnemies.Single();
            enemy.ApplyDamage(enemy.MaxHealth);
            var goal = Object.FindFirstObjectByType<StageGoal>();
            game.Player.Motor.Teleport(new Vector2(goal.transform.position.x, .82f));
            Physics2D.SyncTransforms();
            yield return Until(() => game.State == StageState.Completed, "The stage should clear.");
            var dialog = game.OutGame.Dialog;
            Assert.That(dialog.Title, Is.EqualTo("저장 실패"), "The clear could not be saved.");
            dialog.Item("save-continue").Activate();
            Assert.That(game.HasUnsavedRecords, Is.True);

            game.View.ResultItem("result-title").Activate();
            Assert.That(game.Transition.IsBusy, Is.False, "Leaving waits for an answer.");
            Assert.That(game.OutGame.IsOpen(dialog), Is.True);
            Assert.That(dialog.Message, Does.Contain("저장하지 못한 기록"));
            Assert.That(dialog.ChoiceNames, Is.EqualTo(new[] { "leave-save-retry", "title-confirm", "title-cancel" }));
            yield return Until(() => dialog.Focused?.name == "title-cancel", "취소 is selected first.", 2f);
            Assert.That(game.OutGame.HandleBack(), Is.True);
            Assert.That(game.Transition.IsBusy, Is.False);

            game.View.ResultItem("result-title").Activate();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Save retry failed"));
            dialog.Item("leave-save-retry").Activate();
            Assert.That(game.Transition.IsBusy, Is.False, "A retry that fails stays and asks again.");
            Assert.That(game.OutGame.IsOpen(dialog), Is.True);

            storage.FailWrites = false;
            dialog.Item("leave-save-retry").Activate();
            Assert.That(game.HasUnsavedRecords, Is.False);
            Assert.That(ProgressData.FromJson(storage.Entries[OutGameSaves.ProgressName], game.Catalog).allCleared, Is.True);
            Assert.That(game.Transition.IsBusy, Is.True, "A retry that works goes on to the title.");
            yield return Until(() => game.IsTitle, "The title should load.");
            yield return OutGameTestSupport.WaitForTransition(game);
        }

        // Planning doc 3.2: the scene change and the title background carry the glitch look; the distortion setting turns it off.
        [UnityTest] public IEnumerator TransitionsAndTheTitleShowGlitchBandsUnlessDistortionIsOff()
        {
            yield return LoadTitle();
            var view = game.OutGame;
            view.UpdateGlitch(OutGameView.TitleBurstPeriod * 10 + .05);
            Assert.That(view.TitleGlitchBands, Is.GreaterThan(0), "A short burst behind the title menu.");
            view.UpdateGlitch(OutGameView.TitleBurstPeriod * 10 + 1.0);
            Assert.That(view.TitleGlitchBands, Is.Zero, "Most of the time the background is still.");
            view.SetCover(.5f, false);
            view.UpdateGlitch(5.0);
            Assert.That(view.TransitionGlitchBands, Is.GreaterThan(0), "Halfway through a fade the cover tears.");
            view.SetCover(1f, true);
            view.UpdateGlitch(5.1);
            Assert.That(view.TransitionGlitchBands, Is.Zero, "The full cover with the loading text stays clean.");
            view.SetCover(0f, false);
            view.UpdateGlitch(5.2);

            int most = 0;
            bool textWithBands = false;
            game.OutGame.Title.NewGame.Activate();
            while (game.Transition.IsBusy)
            {
                most = Mathf.Max(most, view.TransitionGlitchBands);
                if (view.IsLoadingVisible && game.Transition.Cover >= 1f && view.TransitionGlitchBands > 0) textWithBands = true;
                yield return null;
            }
            Assert.That(most, Is.GreaterThan(0), "The fades tear with glitch bands.");
            Assert.That(textWithBands, Is.False, "The full cover with the loading text stays clean.");

            PresentationSettings.Set(1f, 0f, 1f);
            try
            {
                most = 0;
                Assert.That(game.TryPause(), Is.True);
                game.PauseMenu.Item(PauseMenuView.TitleName).Activate();
                game.OutGame.Dialog.Item("title-confirm").Activate();
                while (game.Transition.IsBusy) { most = Mathf.Max(most, view.TransitionGlitchBands); yield return null; }
                Assert.That(most, Is.Zero, "Distortion 0 turns the bands off.");
                view.UpdateGlitch(OutGameView.TitleBurstPeriod * 10 + .05);
                Assert.That(view.TitleGlitchBands, Is.Zero);
            }
            finally { PresentationSettings.Reset(); }
        }

        // 2026-11-12: the settings arrows step the way they point; a click elsewhere on the row steps forward, like Enter.
        [UnityTest] public IEnumerator ChoiceRowArrowsStepTheWayTheyPoint()
        {
            yield return LoadTitle();
            game.OutGame.Title.Settings.Activate();
            var row = game.OutGame.Settings.Resolution;
            row.SetOptions(new[] { "A", "B", "C" }, 1);
            yield return null;
            Assert.That(row.PreviousArrow.pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(row.NextArrow.pickingMode, Is.EqualTo(PickingMode.Position));
            PointerUp(row.NextArrow);
            Assert.That(row.Index, Is.EqualTo(2), "▶ steps right.");
            PointerUp(row.PreviousArrow);
            PointerUp(row.PreviousArrow);
            Assert.That(row.Index, Is.EqualTo(0), "◀ steps left.");
            PointerUp(row.PreviousArrow);
            Assert.That(row.Index, Is.EqualTo(2), "◀ wraps from the first option to the last.");
            Assert.That(row.ValueText, Is.EqualTo("C"));
            PointerUp(row);
            Assert.That(row.Index, Is.EqualTo(0), "A click elsewhere on the row steps forward.");
        }

        private static void PointerUp(VisualElement target)
        {
            var mouse = new Event { type = EventType.MouseUp, button = 0, mousePosition = target.worldBound.center };
            using (var up = PointerUpEvent.GetPooled(mouse)) target.SendEvent(up);
        }

        [UnityTest] public IEnumerator SettingsApplyAtOnceSaveSeparatelyAndResetOnlyTheirTab()
        {
            yield return LoadTitle();
            game.OutGame.Title.Settings.Activate();
            var settings = game.OutGame.Settings;
            Assert.That(game.OutGame.IsOpen(settings), Is.True);
            Assert.That(settings.DataTabVisible, Is.True, "Progress deletion is reached from the title only.");
            Assert.That(settings.Music.style.display, Is.EqualTo(new StyleEnum<DisplayStyle>(DisplayStyle.None)), "No music exists yet.");
            settings.Master.SetValue(40, true);
            Assert.That(AudioListener.volume, Is.EqualTo(.4f).Within(.001f), "Volume changes apply while adjusting.");
            settings.Master.Commit();
            settings.Shake.SetValue(0, true);
            settings.Shake.Commit();
            Assert.That(PresentationSettings.Shake, Is.Zero);
            var saved = SettingsData.FromJson(storage.Entries[OutGameSaves.SettingsName], out _);
            Assert.That(saved.masterVolume, Is.EqualTo(40));
            Assert.That(saved.shake, Is.Zero);
            Assert.That(storage.Entries.ContainsKey(OutGameSaves.ProgressName), Is.False, "Settings never write progress.");

            settings.ResetEnvironment.Activate();
            Assert.That(game.OutGame.Dialog.Focused?.name ?? "reset-cancel", Is.EqualTo("reset-cancel"));
            game.OutGame.Dialog.Item("reset-confirm").Activate();
            Assert.That(game.Saves.Settings.masterVolume, Is.EqualTo(100));
            Assert.That(game.Saves.Settings.shake, Is.Zero, "The environment reset leaves presentation alone.");
            Assert.That(AudioListener.volume, Is.EqualTo(1f).Within(.001f));

            game.ReloadSaves(storage);
            Assert.That(game.Saves.Settings.shake, Is.Zero, "Kept values survive a restart.");
            yield return LoadTitle();
            game.OutGame.Title.Settings.Activate();
            Assert.That(game.OutGame.HandleBack(), Is.True);
            Assert.That(game.OutGame.IsOpen(settings), Is.False);
            Assert.That(game.OutGame.IsOpen(game.OutGame.Title), Is.True, "Closing returns to the title.");
        }

        [UnityTest] public IEnumerator DeletingProgressKeepsSettingsAndUpdatesTheTitle()
        {
            StoreProgress(1);
            storage.Set(OutGameSaves.SettingsName, new SettingsData { sfxVolume = 25 }.ToJson());
            game.ReloadSaves(storage);
            yield return LoadTitle();
            Assert.That(game.OutGame.Title.Continue.Available, Is.True);
            game.OutGame.Title.Settings.Activate();
            var settings = game.OutGame.Settings;
            settings.DeleteProgress.Activate();
            Assert.That(game.OutGame.IsOpen(game.OutGame.Dialog), Is.False, "A hidden tab cannot be used.");
            settings.Select(SettingsPanel.Tab.Data, true);
            settings.DeleteProgress.Activate();
            Assert.That(game.OutGame.Dialog.Message, Is.EqualTo(GameManager.DeleteProgressQuestion));
            game.OutGame.Dialog.Item("delete-confirm").Activate();
            Assert.That(storage.Entries.ContainsKey(OutGameSaves.ProgressName), Is.False);
            Assert.That(game.Saves.Settings.sfxVolume, Is.EqualTo(25));
            Assert.That(settings.DeleteProgress.Available, Is.False);
            Assert.That(game.OutGame.HandleBack(), Is.True);
            Assert.That(game.OutGame.Title.Continue.Available, Is.False);
        }

        [UnityTest] public IEnumerator UnreadableProgressIsReportedAndNeverOverwrittenByPlay()
        {
            storage.Set(OutGameSaves.ProgressName, "{ broken");
            game.ReloadSaves(storage);
            yield return LoadTitle();
            Assert.That(game.OutGame.Title.Continue.UnavailableReason, Is.EqualTo(ProgressRules.UnreadableReason));
            int version = game.StageVersion;
            yield return SceneManager.LoadSceneAsync("CoreLoopStage");
            yield return Until(() => game.StageVersion > version, "The stage should still be playable.");
            Assert.That(storage.Entries[OutGameSaves.ProgressName], Is.EqualTo("{ broken"), "Only a new game may replace it.");
        }

        [UnityTest] public IEnumerator SaveFailureDuringPlayIsShownAndTheRunStaysMarkedUnsaved()
        {
            yield return LoadStage();
            var driver = new Driver();
            game.Player.SetInputSource(driver);
            yield return new WaitForFixedUpdate();
            storage.FailWrites = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Progress could not be saved"));
            yield return WalkIntoRest(driver);
            var dialog = game.OutGame.Dialog;
            Assert.That(game.OutGame.IsOpen(dialog), Is.True);
            Assert.That(dialog.Title, Is.EqualTo("저장 실패"));
            Assert.That(game.IsPaused, Is.True, "The world holds while the warning is open.");
            dialog.Item("save-continue").Activate();
            Assert.That(game.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(game.Saves.ProgressUnsaved, Is.True);
            yield return null;
            Assert.That(game.OutGame.IsUnsavedVisible, Is.True);
            Assert.That(game.LeaveMessage("타이틀로 돌아가시겠습니까?"), Does.Contain("저장하지 못한"));
            storage.FailWrites = false;
        }

        [UnityTest] public IEnumerator KeyChangesApplySaveRefuseDuplicatesAndEscCancelsOnlyTheChange()
        {
            yield return LoadStage();
            Keyboard keyboard = null;
            using (new InputFocusScope())
            try
            {
                keyboard = InputSystem.AddDevice<Keyboard>();
                Assert.That(game.TryPause(), Is.True);
                game.PauseMenu.Item(PauseMenuView.SettingsName).Activate();
                var settings = game.OutGame.Settings;
                settings.Select(SettingsPanel.Tab.Keys, true);
                var keys = game.KeyBindings;
                int glitch = keys.Slots.ToList().FindIndex(s => s.Action == "Glitch");
                int jump = keys.Slots.ToList().FindIndex(s => s.Action == "Jump");

                settings.KeyItems[glitch].Activate();
                Assert.That(keys.IsRebinding, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K));
                yield return Until(() => !keys.IsRebinding, "Pressing a key ends the change.", 2f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(keys.EffectivePath(glitch), Is.EqualTo("<Keyboard>/k"));
                Assert.That(SettingsData.FromJson(storage.Entries[OutGameSaves.SettingsName], out _).bindingOverrides, Does.Contain("<Keyboard>/k"));
                Assert.That(game.ControlRows().Single(r => r.action == "글리치").keys, Does.StartWith("K"), "The guide shows the new key.");

                yield return new WaitForSecondsRealtime(.35f);
                settings.KeyItems[jump].Activate();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
                yield return Until(() => !keys.IsRebinding, "Pressing a key ends the change.", 2f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(keys.EffectivePath(jump), Is.EqualTo("<Keyboard>/space"), "A key another action uses is refused.");
                Assert.That(settings.HintText, Does.Contain("공격"), "The refusal names the action that has the key.");

                yield return new WaitForSecondsRealtime(.35f);
                settings.KeyItems[jump].Activate();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return Until(() => !keys.IsRebinding, "Esc cancels the change.", 2f);
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(keys.EffectivePath(jump), Is.EqualTo("<Keyboard>/space"));
                Assert.That(game.OutGame.IsOpen(settings), Is.True, "The Esc that cancelled the change does not close the page.");
                Assert.That(game.IsPaused, Is.True, "...or the pause.");

                Assert.That(game.OutGame.HandleBack(), Is.True);
                Assert.That(game.PauseMenu.IsVisible, Is.True);
                var glitchAction = game.Player.GetComponent<PlayerInput>().actions.FindAction("Player/Glitch");
                Assert.That(glitchAction.bindings.Any(b => b.effectivePath == "<Keyboard>/k"), Is.True, "The player uses the new key.");
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                game.KeyBindings.ResetAll();
            }
        }

        // Planning doc 10: the input that closes a menu must not also attack. The mouse button is still held when
        // 계속하기 resumes play (the strictest case), and the attack only follows a fresh press.
        [UnityTest] public IEnumerator ClosingThePauseWithTheAttackButtonHeldDoesNotAttack()
        {
            yield return LoadStage();
            Mouse mouse = null;
            Keyboard keyboard = null;
            using (new InputFocusScope())
            try
            {
                keyboard = InputSystem.AddDevice<Keyboard>();
                mouse = InputSystem.AddDevice<Mouse>();
                var input = game.Player.GetComponent<PlayerInput>();
                input.enabled = false; input.enabled = true;
                input.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
                game.Player.SetInputSource(null);
                yield return Until(() => game.Player.Motor.IsGrounded, "The player should stand on the floor.");
                Assert.That(game.TryPause(), Is.True);
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                yield return null;
                game.PauseMenu.Item(PauseMenuView.ContinueName).Activate();
                Assert.That(game.IsPaused, Is.False);
                Assert.That(input.inputIsActive, Is.True);
                for (int i = 0; i < 6; i++)
                {
                    yield return new WaitForFixedUpdate();
                    Assert.That(game.Player.AttackPhase, Is.EqualTo(PlayerAttackPhase.Ready), "The held click that closed the menu is not an attack.");
                }
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                yield return Until(() => game.Player.AttackPhase != PlayerAttackPhase.Ready, "A new click attacks as usual.", 2f);
                InputSystem.QueueStateEvent(mouse, new MouseState());
            }
            finally
            {
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            }
        }

        // Every shown text in the game UI and the cutscene UI uses the Galmuri11 bitmap asset at a whole multiple of 12 screen pixels.
        [UnityTest] public IEnumerator EveryShownUiTextUsesThePixelFontAtWholeMultiples()
        {
            yield return LoadStage();
            game.View.ShowCombo(1, 3);
            Assert.That(game.TryPause(), Is.True);
            game.PauseMenu.Item(PauseMenuView.SettingsName).Activate();
            yield return null;
            yield return null;
            int checkedTexts = 0;
            foreach (var document in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                foreach (var text in document.rootVisualElement.Query<TextElement>().ToList())
                {
                    if (!MenuKit.IsShown(text)) continue;
                    var font = text.resolvedStyle.unityFontDefinition;
                    Assert.That(font.fontAsset, Is.Not.Null, $"'{text.text}' ({text.name}) uses a plain font file.");
                    Assert.That(font.fontAsset.name, Is.EqualTo("Galmuri11-Bitmap"));
                    float screenPixels = text.resolvedStyle.fontSize * text.scaledPixelsPerPoint;
                    Assert.That(Mathf.Abs(screenPixels / PixelText.NativeSize - Mathf.Round(screenPixels / PixelText.NativeSize)), Is.LessThan(.01f),
                        $"'{text.text}' is drawn at {screenPixels} screen pixels, not a whole multiple of the 12px pixel font.");
                    checkedTexts++;
                }
            Assert.That(checkedTexts, Is.GreaterThan(10));
        }

        [UnityTest] public IEnumerator PresentationStrengthZeroRemovesShakeButKeepsGameplay()
        {
            yield return LoadStage();
            PresentationSettings.Set(0f, 0f, 0f);
            try
            {
                var player = game.Player;
                player.Feedback.PlayDamage(Vector2.right, Time.unscaledTimeAsDouble);
                yield return null;
                var offset = (Vector3)typeof(PlayerImpactFeedback).GetField("appliedOffset",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(player.Feedback);
                Assert.That(offset.sqrMagnitude, Is.Zero.Within(1e-8f), "Shake 0 turns the camera shake off.");
                Assert.That(player.Feedback.GlitchStrength(Time.unscaledTimeAsDouble), Is.Zero);
                Assert.That(player.Glitch != null, Is.True, "Glitch rules are untouched.");
            }
            finally { PresentationSettings.Reset(); }
        }
    }
}
