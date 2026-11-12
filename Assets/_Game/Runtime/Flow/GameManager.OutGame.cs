using System;
using System.Collections.Generic;
using ActionPlatformer.Audio;
using ActionPlatformer.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ActionPlatformer.Flow
{
    // Out-game flow (planning doc "치지직 아웃게임 기능 기획서" v0.1): title, new game and continue at rest-area
    // checkpoints, stage select, scene changes with a loading cover, settings, controls guide, key changes, saves and quit.
    public sealed partial class GameManager
    {
        // No music exists yet; its volume is stored and applied but the slider stays hidden until there is some.
        public const bool HasMusic = false;
        public const string NewGameQuestion = "새 게임을 시작하면 기존 진행 기록이 삭제됩니다. 설정은 유지됩니다. 시작하시겠습니까?";
        public const string DeleteProgressQuestion = "진행 기록을 삭제하시겠습니까? 삭제한 기록은 되돌릴 수 없습니다. 설정은 유지됩니다";
        // Tests replace the real quit so a click cannot end Play Mode.
        public static Action QuitHandler;

        private struct Entry
        {
            public bool Valid, NewGame;
            public string Id, Scene;
            public int Checkpoint;
        }

        private OutGameSaves saves;
        private SceneTransition transition;
        private KeyBindings keyBindings;
        private DisplayConfirmation displayConfirm;
        private TitleScreen title;
        private string stageId;
        private Entry pending;
        private AudioSource previewSource;
        private double lastPreviewAt = double.NegativeInfinity;
        private bool outGameStarted, displayApplied, coverClosedMenus, inputHeldForTransition, quitConfirmed, pauseOwnedByDialog;
        private Action retryTransition;
        private object wiredFor;

        public OutGameSaves Saves => saves;
        public SceneTransition Transition => transition;
        public KeyBindings KeyBindings => keyBindings;
        public OutGameView OutGame => outGame;
        public StageCatalog Catalog => catalog;
        public bool IsTitle => title != null;
        public TitleScreen Title => title;
        // Catalog id of the registered stage, or null when the scene is not a catalog stage.
        public string StageId => stageId;
        public int ResumedCheckpoint => progress?.ResumedFrom ?? ProgressData.NoCheckpoint;
        private bool OutGameAllowsPause => title == null && (transition == null || !transition.IsBusy) &&
            (outGame == null || !outGame.HasOpenPanel);

        private void EnableOutGame()
        {
            if (transition == null)
            {
                transition = new SceneTransition();
                transition.Finished += OnTransitionFinished;
                transition.Failed += OnTransitionFailed;
            }
            if (saves == null)
            {
                saves = new OutGameSaves(catalog, SaveStorage.Current);
                keyBindings = playerControls != null ? new KeyBindings(playerControls) : null;
                saves.Load();
            }
            if (previewSource == null)
            {
                previewSource = gameObject.AddComponent<AudioSource>();
                previewSource.playOnAwake = false;
                previewSource.ignoreListenerPause = true;
                previewSource.spatialBlend = 0f;
            }
            ApplyAllSettings();
            if (!displayApplied) { displayApplied = true; DisplayOptions.ApplySaved(saves.Settings); }
            if (pauseMenu != null)
            {
                pauseMenu.SettingsRequested += OpenSettingsFromPause;
                pauseMenu.ControlsRequested += OpenControlsFromPause;
                pauseMenu.RestartRequested += AskRestartFromPause;
                pauseMenu.TitleRequested += AskTitleFromPause;
            }
            if (outGame != null)
            {
                outGame.Built += WireMenus;
                if (outGame.IsConfigured) WireMenus();
            }
            Application.wantsToQuit += OnWantsToQuit;
            outGameStarted = true;
        }

        private void DisableOutGame()
        {
            if (!outGameStarted) return;
            outGameStarted = false;
            keyBindings?.Cancel();
            if (pauseMenu != null)
            {
                pauseMenu.SettingsRequested -= OpenSettingsFromPause;
                pauseMenu.ControlsRequested -= OpenControlsFromPause;
                pauseMenu.RestartRequested -= AskRestartFromPause;
                pauseMenu.TitleRequested -= AskTitleFromPause;
            }
            if (outGame != null) outGame.Built -= WireMenus;
            Application.wantsToQuit -= OnWantsToQuit;
        }

        private void ReleaseOutGame()
        {
            keyBindings?.Cancel();
            // Play Mode ends; the editor's copy of the actions asset must not keep this run's keys.
            keyBindings?.ResetAll();
        }

        // Replaces the save location and reloads every record (tests use memory storage).
        public void ReloadSaves(ISaveStorage storage)
        {
            if (saves == null) return;
            saves.UseStorage(storage);
            saves.Load();
            ApplyAllSettings();
            RefreshTitle();
        }

        private void ApplyAllSettings()
        {
            var s = saves.Settings;
            AudioVolumes.Set(s.masterVolume / 100f, s.musicVolume / 100f, s.sfxVolume / 100f);
            PresentationSettings.Set(s.shake / 100f, s.distortion / 100f, s.flash / 100f);
            keyBindings?.Load(s.bindingOverrides);
            if (view != null) view.SetTransferKey(GlitchKeyName());
        }

        private string GlitchKeyName()
        {
            if (keyBindings == null || !keyBindings.IsConfigured) return "SHIFT";
            int glitch = keyBindings.Slots.Count - 1;
            return keyBindings.EffectivePath(glitch) == keyBindings.Slots[glitch].DefaultPath ? "SHIFT" : keyBindings.Display(glitch);
        }

        // ---- Stage entry, checkpoints and results ----

        private Entry TakeEntry(Scene scene)
        {
            var entry = pending;
            pending = default;
            if (entry.Valid && entry.Scene == scene.name) return entry;
            int index = catalog != null ? catalog.IndexOfScene(scene.name) : -1;
            return new Entry { Valid = true, Id = index >= 0 ? catalog[index].Id : null, Scene = scene.name, Checkpoint = ProgressData.NoCheckpoint };
        }

        private void OnStageEntered(Entry entry)
        {
            stageId = entry.Id;
            bool changed = false;
            if (stageId != null && saves != null)
            {
                // A run with no record (first start, or the editor opening a stage directly) begins one; an unreadable
                // record is left alone unless the player chose a new game (planning doc 7.3).
                if (entry.NewGame || (saves.Progress == null && saves.ProgressLoad != SaveReadResult.Failed && catalog.IndexOfId(stageId) == 0))
                {
                    saves.StartNewProgress();
                    changed = true;
                }
                changed |= ProgressRules.OnStageEntered(saves.Progress, catalog, stageId, progress.ResumedFrom);
            }
            saves?.SetRetry(stageId, progress.ResumedFrom);
            if (changed) SaveProgressOrWarn();
            if (keyBindings != null && playerInput != null) keyBindings.ApplyTo(playerInput.actions);
            view.SetTransferKey(GlitchKeyName());
            // Play input waits until the loading cover has lifted.
            if (transition != null && transition.IsBusy && playerInput != null)
            {
                playerInput.DeactivateInput();
                inputHeldForTransition = true;
            }
        }

        private void OnRestReached(int encounterIndex)
        {
            saves?.SetRetry(stageId, encounterIndex);
            if (saves != null && stageId != null && ProgressRules.OnRestReached(saves.Progress, stageId, encounterIndex))
                SaveProgressOrWarn();
        }

        private string RetryDetail()
        {
            int checkpoint = saves != null ? saves.RetryCheckpointFor(stageId) : ProgressData.NoCheckpoint;
            return checkpoint >= 0 ? $"휴식 구역 {checkpoint + 1}부터" : "처음부터";
        }

        private void ShowClearResult()
        {
            bool finalClear = true, hasNext = false;
            string name = null;
            if (stageId != null && catalog != null)
            {
                name = catalog.Find(stageId)?.DisplayName;
                finalClear = catalog.IsLast(stageId);
                hasNext = !finalClear;
                if (saves != null && ProgressRules.OnStageCleared(saves.Progress, catalog, stageId)) SaveProgressOrWarn();
            }
            saves?.ClearRetry();
            view.ShowCompleted(finalClear, name, hasNext, catalog != null && catalog.HasStageSelect && saves?.Progress != null);
        }

        private void SubscribeStageView(bool on)
        {
            if (view == null) return;
            if (on)
            {
                view.RestartRequested += Restart;
                view.RetryRequested += Retry;
                view.NextRequested += NextStage;
                view.StageSelectRequested += OpenStageSelectFromResult;
                view.TitleRequested += ReturnToTitleFromResult;
            }
            else
            {
                view.RestartRequested -= Restart;
                view.RetryRequested -= Retry;
                view.NextRequested -= NextStage;
                view.StageSelectRequested -= OpenStageSelectFromResult;
                view.TitleRequested -= ReturnToTitleFromResult;
            }
        }

        public void NextStage()
        {
            if (stage == null || State != StageState.Completed || catalog == null) return;
            var next = catalog.Next(stageId);
            if (next != null && saves?.Progress != null && saves.Progress.IsUnlocked(next.Id))
                EnterStage(next.Id, next.Scene, ProgressData.NoCheckpoint, false, true);
        }

        private void OpenStageSelectFromResult() => OpenStageSelect(() => view.ResultItem("result-select")?.Focus());

        // ---- Scene changes ----

        private bool EnterStage(string id, string scene, int checkpoint, bool newGame, bool fromStart)
        {
            if (transition == null || transition.IsBusy || string.IsNullOrEmpty(scene)) return false;
            pending = new Entry { Valid = true, Id = id, Scene = scene, Checkpoint = fromStart ? ProgressData.NoCheckpoint : checkpoint, NewGame = newGame };
            int version = StageVersion;
            bool started = BeginTransition(scene, () => stage != null && initialized && StageVersion > version &&
                stage.gameObject.scene.name == scene, () => EnterStage(id, scene, checkpoint, newGame, fromStart));
            if (!started) pending = default;
            return started;
        }

        public void ReturnToTitle()
        {
            if (catalog == null) return;
            string scene = catalog.TitleScene;
            BeginTransition(scene, () => title != null && title.gameObject.scene.name == scene, ReturnToTitle);
        }

        private bool BeginTransition(string scene, Func<bool> ready, Action retry)
        {
            if (!transition.Begin(scene, ready)) return false;
            retryTransition = retry;
            coverClosedMenus = false;
            keyBindings?.Cancel();
            displayConfirm?.Revert();
            if (IsPaused)
            {
                CloseMenusOverPause();
                pause.Resume(restoreTimeAndInput: false);
                if (pauseMenu != null) pauseMenu.Show(false);
            }
            pauseOwnedByDialog = false;
            outGame?.SetInteractable(false);
            if (stage != null && initialized)
            {
                State = StageState.Restarting;
                view.SetRestartEnabled(false);
                if (playerInput != null) playerInput.DeactivateInput();
            }
            return true;
        }

        private void OnTransitionFinished()
        {
            outGame?.SetInteractable(true);
            if (inputHeldForTransition && playerInput != null && StagePlaying && !IsPaused) playerInput.ActivateInput();
            inputHeldForTransition = false;
            if (title != null && outGame != null && outGame.IsOpen(outGame.Title)) outGame.Title.FocusDefault(false);
        }

        private void OnTransitionFailed(string message)
        {
            outGame?.SetInteractable(true);
            inputHeldForTransition = false;
            pending = default;
            if (outGame == null) { transition.Acknowledge(); return; }
            var retry = retryTransition;
            bool toTitle = catalog != null && transition.TargetScene == catalog.TitleScene;
            transition.Acknowledge();
            var choices = new List<ConfirmDialog.Choice> { new ConfirmDialog.Choice("transition-retry", "다시 시도", () => retry?.Invoke()) };
            choices.Add(toTitle
                ? new ConfirmDialog.Choice("transition-quit", "게임 종료", QuitGame, true)
                : new ConfirmDialog.Choice("transition-title", "타이틀로", ReturnToTitle, true));
            outGame.ShowDialog("불러오기 실패", message + " 기존 진행 기록은 그대로 남아 있습니다.", choices.ToArray());
        }

        // ---- Title ----

        public void RegisterTitle(TitleScreen screen)
        {
            if (screen == null || title == screen) return;
            title = screen;
            if (IsPaused) Resume();
            if (outGame == null) return;
            outGame.Stack.CloseAll();
            RefreshTitle();
            outGame.Open(outGame.Title);
        }

        public void UnregisterTitle(TitleScreen screen)
        {
            if (screen == null || title != screen) return;
            title = null;
            keyBindings?.Cancel();
            displayConfirm?.Revert();
            outGame?.Stack.CloseAll();
        }

        private void RefreshTitle()
        {
            if (outGame == null || outGame.Title == null || saves == null) return;
            var panel = outGame.Title;
            bool canContinue = saves.CanContinue(out var reason);
            panel.Continue.SetAvailable(canContinue, reason);
            var p = saves.Progress;
            panel.Continue.Detail = canContinue && catalog != null
                ? (catalog.HasStageSelect ? catalog.Find(p.mainStage)?.DisplayName + " · " : "") +
                  (p.mainCheckpoint >= 0 ? $"휴식 구역 {p.mainCheckpoint + 1}부터" : "처음부터")
                : null;
            panel.StageSelect.style.display = catalog != null && catalog.HasStageSelect
                ? UnityEngine.UIElements.DisplayStyle.Flex : UnityEngine.UIElements.DisplayStyle.None;
            panel.StageSelect.SetAvailable(saves.Progress != null, ProgressRules.NoRecordReason);
        }

        public void StartNewGame()
        {
            if (catalog == null || catalog.Count == 0 || transition.IsBusy) return;
            var first = catalog[0];
            if (saves.HasAnyProgress)
                outGame.ShowDialog("새 게임", NewGameQuestion,
                    new ConfirmDialog.Choice("new-game-confirm", "시작", () => EnterStage(first.Id, first.Scene, ProgressData.NoCheckpoint, true, true)),
                    new ConfirmDialog.Choice("new-game-cancel", "취소", null, true));
            else EnterStage(first.Id, first.Scene, ProgressData.NoCheckpoint, true, true);
        }

        public void ContinueGame()
        {
            if (!saves.CanContinue(out _) || catalog == null) return;
            var entry = catalog.Find(saves.Progress.mainStage);
            if (entry != null) EnterStage(entry.Id, entry.Scene, saves.Progress.mainCheckpoint, false, false);
        }

        public void OpenStageSelect(Action onClosed)
        {
            if (outGame == null || catalog == null) return;
            var list = new List<(string, string, StageSelectPanel.Availability)>();
            for (int i = 0; i < catalog.Count; i++)
            {
                var e = catalog[i];
                var p = saves.Progress;
                var state = p != null && p.IsCleared(e.Id) ? StageSelectPanel.Availability.Cleared :
                    p != null && p.IsUnlocked(e.Id) ? StageSelectPanel.Availability.Open : StageSelectPanel.Availability.Locked;
                list.Add((e.Id, $"{i + 1}. {e.DisplayName}", state));
            }
            outGame.StageSelect.Rebuild(list);
            outGame.Open(outGame.StageSelect, onClosed);
        }

        private void EnterSelectedStage(string id)
        {
            var entry = catalog?.Find(id);
            if (entry == null || saves.Progress == null || !saves.Progress.IsUnlocked(id)) return;
            EnterStage(entry.Id, entry.Scene, ProgressData.NoCheckpoint, false, true);
        }

        public void QuitGame()
        {
            if (transition != null && transition.IsBusy) return;
            if (!quitConfirmed && QuitNeedsConfirm && outGame != null) { AskQuit(); return; }
            quitConfirmed = true;
            if (QuitHandler != null) { QuitHandler(); quitConfirmed = false; return; }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        // ---- Pause menu pages ----

        private void OpenSettingsFromPause()
        {
            if (!IsPaused || outGame == null) return;
            pauseMenu.Show(false);
            OpenSettings(false, () => { if (IsPaused) pauseMenu.Show(true, true); });
        }

        private void OpenControlsFromPause()
        {
            if (!IsPaused || outGame == null) return;
            pauseMenu.Show(false);
            OpenControls(() => { if (IsPaused) pauseMenu.Show(true, true); });
        }

        private void ReturnFocusToPause()
        {
            if (IsPaused && pauseMenu != null && pauseMenu.IsVisible && (outGame == null || !outGame.HasOpenPanel)) pauseMenu.Show(true, true);
        }

        private void AskRestartFromPause()
        {
            if (!IsPaused || outGame == null) return;
            bool savedPoint = saves?.Progress != null && saves.Progress.mainStage == stageId && saves.Progress.mainCheckpoint >= 0 &&
                !saves.Progress.IsCleared(stageId);
            outGame.ShowDialog("현재 스테이지 재시작",
                "스테이지를 처음부터 다시 시작합니다. 현재 시도의 진행이 초기화됩니다" +
                (savedPoint ? " (저장된 휴식 구역 기록도 처음으로 돌아갑니다)." : ".") + " 재시작하시겠습니까?",
                ReturnFocusToPause,
                new ConfirmDialog.Choice("restart-confirm", "재시작", Restart),
                new ConfirmDialog.Choice("restart-cancel", "취소", null, true));
        }

        private void AskTitleFromPause()
        {
            if (!IsPaused || outGame == null) return;
            outGame.ShowDialog("타이틀로", LeaveMessage("타이틀로 돌아가시겠습니까?"), ReturnFocusToPause,
                LeaveChoices("title-confirm", "타이틀로", ReturnToTitle, "title-cancel", AskTitleFromPause));
        }

        // The result and failure screens leave at once, unless a save failed: then the loss is explained again
        // and the save can be retried first (planning doc 7.3).
        private void ReturnToTitleFromResult()
        {
            if (!HasUnsavedRecords || outGame == null) { ReturnToTitle(); return; }
            string focus = State == StageState.Failed ? "fail-title" : "result-title";
            outGame.ShowDialog("타이틀로", LeaveMessage("타이틀로 돌아가시겠습니까?"), () => view.ResultItem(focus)?.Focus(),
                LeaveChoices("title-confirm", "타이틀로", ReturnToTitle, "title-cancel", ReturnToTitleFromResult));
        }

        public bool HasUnsavedRecords => saves != null && (saves.ProgressUnsaved || saves.SettingsUnsaved);

        // Leave / cancel, plus "다시 저장" while a record is unsaved. A retry that works leaves; one that fails asks again.
        private ConfirmDialog.Choice[] LeaveChoices(string confirmName, string confirmLabel, Action leave, string cancelName, Action askAgain)
        {
            var choices = new List<ConfirmDialog.Choice>();
            if (HasUnsavedRecords)
                choices.Add(new ConfirmDialog.Choice("leave-save-retry", "다시 저장", () =>
                {
                    if (RetryUnsavedRecords()) leave();
                    else askAgain?.Invoke();
                }));
            choices.Add(new ConfirmDialog.Choice(confirmName, confirmLabel, leave));
            choices.Add(new ConfirmDialog.Choice(cancelName, "취소", null, true));
            return choices.ToArray();
        }

        private bool RetryUnsavedRecords()
        {
            bool ok = true;
            if (saves.ProgressUnsaved) ok &= saves.SaveProgress();
            if (saves.SettingsUnsaved) ok &= saves.SaveSettings();
            if (!ok) Debug.LogWarning("Save retry failed: " + saves.LastError);
            return ok;
        }

        // What leaving now keeps and loses under the rest-area save policy (planning doc 6.1, 8.2).
        public string LeaveMessage(string question)
        {
            if (HasUnsavedRecords)
                return "저장하지 못한 기록이 있습니다. 저장하지 않은 채 게임을 끄면 그 기록은 사라집니다. " + question;
            var p = saves?.Progress;
            if (stage == null) return question;
            if (stageId != null && p != null && p.IsCleared(stageId)) return "다시 플레이 중인 진행은 저장되지 않습니다. " + question;
            if (stageId != null && p != null && p.mainStage == stageId && p.mainCheckpoint >= 0)
                return "마지막 휴식 구역 이후의 진행은 저장되지 않습니다. " + question;
            return "현재 스테이지 내 진행은 저장되지 않습니다. " + question;
        }

        private void CloseMenusOverPause()
        {
            if (title == null) outGame?.Stack.CloseAll();
        }

        private void CloseStageMenus()
        {
            if (title == null) outGame?.Stack.CloseAll();
        }

        // A scene loaded by anything but the current change (or a plain single load) leaves no page or cover behind.
        private void OnSceneLoadedOutGame(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || transition == null) return;
            if (transition.IsBusy && scene.name != transition.TargetScene)
            {
                transition.Cancel();
                pending = default;
                inputHeldForTransition = false;
                outGame?.SetInteractable(true);
                outGame?.SetCover(0f, false);
            }
            if (!transition.IsBusy) outGame?.Stack.CloseAll();
        }

        // ---- Esc and per-frame work ----

        private bool EscapeTakenByOutGame()
        {
            if (keyBindings != null && (keyBindings.IsRebinding || keyBindings.EndedFrame == Time.frameCount)) return true;
            if (transition != null && transition.IsBusy) return true;
            if (outGame != null && outGame.HandleBack()) return true;
            return title != null;
        }

        private void LateUpdateOutGame()
        {
            if (transition == null) return;
            float dt = Time.unscaledDeltaTime;
            transition.Tick(dt);
            if (transition.IsBusy && transition.Cover >= 1f && !coverClosedMenus)
            {
                // Pages close under the full cover, so the change never shows an empty frame.
                coverClosedMenus = true;
                outGame?.Stack.CloseAll();
            }
            bool loadingText = transition.Phase == TransitionPhase.Loading || transition.Phase == TransitionPhase.Preparing;
            outGame?.SetCover(transition.Cover, loadingText);
            if (displayConfirm != null && displayConfirm.Active)
            {
                if (outGame == null || !outGame.IsOpen(outGame.Dialog)) displayConfirm.Revert();
                else if (displayConfirm.Tick(dt)) outGame.Close(outGame.Dialog);
                else outGame.Dialog.SetMessage(DisplayQuestion());
            }
            outGame?.ShowUnsaved(saves != null && (saves.ProgressUnsaved || saves.SettingsUnsaved) && (stage != null || title != null));
        }

        // ---- Saving ----

        private void SaveProgressOrWarn()
        {
            if (saves.SaveProgress() || outGame == null) return;
            Debug.LogWarning("Progress could not be saved: " + saves.LastError);
            ShowBlockingDialog("저장 실패", "진행을 저장하지 못했습니다. 다시 시도하거나, 저장하지 않은 채로 계속할 수 있습니다.",
                new ConfirmDialog.Choice("save-retry", "다시 시도", SaveProgressOrWarn),
                new ConfirmDialog.Choice("save-continue", "계속하기", null, true));
        }

        private void SaveSettingsOrWarn()
        {
            if (saves.SaveSettings() || outGame == null) return;
            Debug.LogWarning("Settings could not be saved: " + saves.LastError);
            ShowBlockingDialog("저장 실패", "설정을 저장하지 못했습니다. 바꾼 값은 이번 실행에만 적용됩니다.",
                new ConfirmDialog.Choice("save-retry", "다시 시도", SaveSettingsOrWarn),
                new ConfirmDialog.Choice("save-continue", "닫기", null, true));
        }

        // During play a dialog holds the world still and takes the input, like the pause does.
        private void ShowBlockingDialog(string heading, string message, params ConfirmDialog.Choice[] choices)
        {
            bool holdWorld = stage != null && StagePlaying && !IsPaused;
            if (holdWorld) { pause.Pause(); pauseOwnedByDialog = true; }
            outGame.ShowDialog(heading, message, () =>
            {
                if (pauseOwnedByDialog && IsPaused && (pauseMenu == null || !pauseMenu.IsVisible) && !outGame.HasOpenPanel)
                {
                    pauseOwnedByDialog = false;
                    pause.Resume(restoreTimeAndInput: stage == null || State == StageState.Playing);
                }
                else ReturnFocusToPause();
            }, choices);
        }

        // ---- Settings ----

        private void WireMenus()
        {
            if (outGame == null || !outGame.IsConfigured || wiredFor == outGame.Settings) return;
            wiredFor = outGame.Settings;
            var t = outGame.Title;
            t.NewGame.SetAction(StartNewGame);
            t.Continue.SetAction(ContinueGame);
            t.StageSelect.SetAction(() => OpenStageSelect(null));
            t.Settings.SetAction(() => OpenSettings(true, RefreshTitle));
            t.Controls.SetAction(() => OpenControls(null));
            t.Quit.SetAction(QuitGame);
            outGame.StageSelect.Entered = EnterSelectedStage;

            var s = outGame.Settings;
            s.Master.Changed += v => { saves.Settings.masterVolume = v; ApplyAudio(); Preview(1f); };
            s.Music.Changed += v => { saves.Settings.musicVolume = v; ApplyAudio(); };
            s.Sfx.Changed += v => { saves.Settings.sfxVolume = v; ApplyAudio(); Preview(v / 100f); };
            s.Shake.Changed += v => { saves.Settings.shake = v; ApplyPresentation(); };
            s.Distortion.Changed += v => { saves.Settings.distortion = v; ApplyPresentation(); };
            s.Flash.Changed += v => { saves.Settings.flash = v; ApplyPresentation(); };
            foreach (var row in new[] { s.Master, s.Music, s.Sfx, s.Shake, s.Distortion, s.Flash })
                row.Committed += _ => SaveSettingsOrWarn();
            s.ApplyDisplay.SetAction(ApplyDisplayChoice);
            s.ResetEnvironment.SetAction(() => AskReset("환경 설정을 기본값으로 되돌리시겠습니까? 진행도와 다른 설정은 유지됩니다.", () =>
            {
                saves.Settings.ResetEnvironment();
                ApplyAudio();
                DisplayOptions.ApplyProjectDefault();
            }));
            s.ResetPresentation.SetAction(() => AskReset("연출 설정을 기본값으로 되돌리시겠습니까? 진행도와 다른 설정은 유지됩니다.", () =>
            {
                saves.Settings.ResetPresentation();
                ApplyPresentation();
            }));
            s.ResetKeys.SetAction(() => AskReset("변경한 조작 키를 기본 키로 되돌리시겠습니까? 진행도와 다른 설정은 유지됩니다.", () =>
            {
                keyBindings?.ResetAll();
                saves.Settings.ResetBindings();
                if (view != null) view.SetTransferKey(GlitchKeyName());
            }));
            s.DeleteProgress.SetAction(AskDeleteProgress);
            s.KeySelected = BeginRebind;
        }

        private void ApplyAudio()
        {
            var st = saves.Settings;
            AudioVolumes.Set(st.masterVolume / 100f, st.musicVolume / 100f, st.sfxVolume / 100f);
        }

        private void ApplyPresentation()
        {
            var st = saves.Settings;
            PresentationSettings.Set(st.shake / 100f, st.distortion / 100f, st.flash / 100f);
        }

        // A short sound at the new level, heard even while the game is paused.
        private void Preview(float scale)
        {
            if (previewClip == null || previewSource == null) return;
            double now = Time.unscaledTimeAsDouble;
            if (now - lastPreviewAt < .12) return;
            lastPreviewAt = now;
            previewSource.PlayOneShot(previewClip, Mathf.Clamp01(scale) * .5f);
        }

        public void OpenSettings(bool fromTitle, Action onClosed)
        {
            if (outGame == null || saves == null) return;
            var panel = outGame.Settings;
            panel.SetDataTabVisible(fromTitle);
            panel.SetMusicVisible(HasMusic);
            panel.Select(SettingsPanel.Tab.Environment, false);
            PopulateSettings();
            outGame.Open(panel, () =>
            {
                keyBindings?.Cancel();
                SaveSettingsOrWarn();
                onClosed?.Invoke();
            });
        }

        private void PopulateSettings()
        {
            var panel = outGame.Settings;
            var st = saves.Settings;
            panel.Master.SetValue(st.masterVolume);
            panel.Music.SetValue(st.musicVolume);
            panel.Sfx.SetValue(st.sfxVolume);
            panel.Shake.SetValue(st.shake);
            panel.Distortion.SetValue(st.distortion);
            panel.Flash.SetValue(st.flash);
            panel.ScreenMode.SetOptions(DisplayOptions.ModeNames, DisplayOptions.CurrentFullScreen ? 1 : 0);
            displaySizes = DisplayOptions.Resolutions();
            var names = new string[displaySizes.Count];
            for (int i = 0; i < names.Length; i++) names[i] = DisplayOptions.Name(displaySizes[i]);
            panel.Resolution.SetOptions(names, Mathf.Max(0, displaySizes.IndexOf(DisplayOptions.Current)));
            RefreshKeyRows();
            panel.DeleteProgress.SetAvailable(saves.HasAnyProgress, "삭제할 진행 기록이 없습니다");
        }

        private List<Vector2Int> displaySizes = new List<Vector2Int>();

        private void RefreshKeyRows()
        {
            var rows = new List<(string, string)>();
            if (keyBindings != null)
                for (int i = 0; i < keyBindings.Slots.Count; i++) rows.Add((keyBindings.Slots[i].Label, keyBindings.Display(i)));
            if (outGame.Settings.KeyItems.Count == rows.Count)
                for (int i = 0; i < rows.Count; i++) outGame.Settings.SetKeyText(i, rows[i].Item1, rows[i].Item2);
            else outGame.Settings.RebuildKeys(rows);
            if (view != null) view.SetTransferKey(GlitchKeyName());
        }

        private void AskReset(string question, Action reset)
        {
            outGame.ShowDialog("기본값 복원", question,
                new ConfirmDialog.Choice("reset-confirm", "복원", () =>
                {
                    reset();
                    SaveSettingsOrWarn();
                    PopulateSettings();
                }),
                new ConfirmDialog.Choice("reset-cancel", "취소", null, true));
        }

        private void AskDeleteProgress()
        {
            outGame.ShowDialog("진행도 삭제", DeleteProgressQuestion,
                new ConfirmDialog.Choice("delete-confirm", "삭제", DeleteProgressNow),
                new ConfirmDialog.Choice("delete-cancel", "취소", null, true));
        }

        private void DeleteProgressNow()
        {
            if (saves.DeleteProgress())
            {
                outGame.Settings.Notify("진행 기록을 삭제했습니다");
                PopulateSettings();
                RefreshTitle();
                return;
            }
            outGame.ShowDialog("삭제 실패", "진행 기록을 삭제하지 못했습니다. 기록은 그대로 남아 있습니다.",
                new ConfirmDialog.Choice("delete-retry", "다시 시도", DeleteProgressNow),
                new ConfirmDialog.Choice("delete-close", "닫기", null, true));
        }

        private void BeginRebind(int index)
        {
            if (keyBindings == null || keyBindings.IsRebinding || Time.unscaledTimeAsDouble - keyBindings.EndedAt < .3) return;
            var slot = keyBindings.Slots[index];
            if (!keyBindings.StartRebind(index, (outcome, conflict) =>
                {
                    switch (outcome)
                    {
                        case RebindOutcome.Applied:
                            saves.Settings.bindingOverrides = keyBindings.Save();
                            SaveSettingsOrWarn();
                            outGame.Settings.Notify($"{slot.Label}: {keyBindings.Display(index)}");
                            break;
                        case RebindOutcome.Conflict:
                            outGame.Settings.Notify($"'{conflict}'에 이미 쓰는 키입니다. 바꾸지 않았습니다.");
                            break;
                        default:
                            outGame.Settings.Notify("키 변경을 취소했습니다");
                            break;
                    }
                    RefreshKeyRows();
                })) return;
            outGame.Settings.SetKeyText(index, slot.Label, "키를 누르세요 · ESC 취소");
            outGame.Settings.Notify("바꿀 키나 마우스 버튼을 누르세요. ESC를 누르면 취소합니다.");
        }

        private string DisplayQuestion() =>
            $"화면 설정을 유지하시겠습니까? {displayConfirm.SecondsLeft}초 안에 선택하지 않으면 이전 설정으로 돌아갑니다.";

        private void ApplyDisplayChoice()
        {
            var panel = outGame.Settings;
            bool previousFull = DisplayOptions.CurrentFullScreen;
            var previousSize = DisplayOptions.Current;
            bool full = panel.ScreenMode.Index == 1;
            var size = displaySizes.Count > 0 ? displaySizes[Mathf.Clamp(panel.Resolution.Index, 0, displaySizes.Count - 1)] : previousSize;
            DisplayOptions.Apply(full, size);
            displayConfirm = new DisplayConfirmation(
                keep: () =>
                {
                    saves.Settings.screenMode = (int)(full ? ScreenModeSetting.FullScreen : ScreenModeSetting.Windowed);
                    saves.Settings.resolutionWidth = size.x;
                    saves.Settings.resolutionHeight = size.y;
                    SaveSettingsOrWarn();
                },
                revert: () =>
                {
                    DisplayOptions.Apply(previousFull, previousSize);
                    if (outGame != null && outGame.IsOpen(outGame.Settings))
                    {
                        panel.ScreenMode.Select(previousFull ? 1 : 0, false);
                        int i = displaySizes.IndexOf(previousSize);
                        if (i >= 0) panel.Resolution.Select(i, false);
                        panel.Notify("이전 화면 설정으로 되돌렸습니다");
                    }
                });
            displayConfirm.Begin();
            outGame.ShowDialog("화면 설정 확인", DisplayQuestion(),
                new ConfirmDialog.Choice("display-keep", "유지", () => displayConfirm.Keep()),
                new ConfirmDialog.Choice("display-revert", "되돌리기", () => displayConfirm.Revert(), true));
        }

        // ---- Controls guide ----

        public void OpenControls(Action onClosed)
        {
            if (outGame == null) return;
            outGame.Controls.Rebuild(ControlRows());
            outGame.Open(outGame.Controls, onClosed);
        }

        public IReadOnlyList<(string action, string keys)> ControlRows()
        {
            var rows = new List<(string, string)>();
            if (keyBindings != null && keyBindings.IsConfigured)
            {
                rows.Add(("왼쪽 이동", keyBindings.DisplayAll("Move", "left")));
                rows.Add(("오른쪽 이동", keyBindings.DisplayAll("Move", "right")));
                rows.Add(("점프 · 잠복 취소", keyBindings.DisplayAll("Jump")));
                rows.Add(("공격", keyBindings.DisplayAll("Attack")));
                rows.Add(("글리치", keyBindings.DisplayAll("Glitch")));
            }
            rows.Add(("조준", "마우스"));
            rows.Add(("일시정지", "Esc"));
            rows.Add(("메뉴 이동 · 선택", "방향키 · Enter · 마우스"));
            rows.Add(("메뉴 닫기 · 취소", "Esc"));
            return rows;
        }

        // ---- Quit ----

        // Leaving a stage mid-run or with a failed save asks first (planning doc 8.2); otherwise the program just ends.
        private bool QuitNeedsConfirm =>
            (stage != null && initialized && (State == StageState.Playing || State == StageState.Dying)) ||
            HasUnsavedRecords;

        private bool OnWantsToQuit()
        {
            if (quitConfirmed || !QuitNeedsConfirm || outGame == null || instance != this) return true;
            AskQuit();
            return false;
        }

        private void AskQuit()
        {
            if (stage != null && State == StageState.Playing && !IsPaused) TryPause();
            outGame.ShowDialog("게임 종료", LeaveMessage("종료하시겠습니까?"), ReturnFocusToPause,
                LeaveChoices("quit-confirm", "종료", () => { quitConfirmed = true; QuitGame(); }, "quit-cancel", AskQuit));
        }
    }
}
