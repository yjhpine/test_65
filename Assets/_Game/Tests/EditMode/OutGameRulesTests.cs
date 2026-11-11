using System;
using System.IO;
using System.Linq;
using ActionPlatformer.Flow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ActionPlatformer.Tests
{
    // Rules of planning doc "치지직 아웃게임 기능 기획서" v0.1 that need no running game.
    public sealed class OutGameRulesTests
    {
        private static StageCatalog TwoStages() => StageCatalog.Create("Title",
            new StageCatalog.Entry("a", "A", "SceneA"), new StageCatalog.Entry("b", "B", "SceneB"));

        // ---- 7.2 save points ----

        [Test] public void NewGameStartsAtTheFirstStageWithItUnlocked()
        {
            var catalog = TwoStages();
            var data = ProgressRules.NewGame(catalog);
            Assert.That(data.mainStage, Is.EqualTo("a"));
            Assert.That(data.mainCheckpoint, Is.EqualTo(ProgressData.NoCheckpoint));
            Assert.That(data.unlocked, Is.EqualTo(new[] { "a" }));
            Assert.That(data.cleared, Is.Empty);
            Assert.That(ProgressRules.CanContinue(data, SaveReadResult.Loaded, out _), Is.True);
            Object.DestroyImmediate(catalog);
        }

        [Test] public void RestAreasMoveTheMainCheckpointOnlyForTheMainUnclearedStage()
        {
            var catalog = TwoStages();
            var data = ProgressRules.NewGame(catalog);
            Assert.That(ProgressRules.OnRestReached(data, "a", 0), Is.True);
            Assert.That(data.mainCheckpoint, Is.Zero);
            Assert.That(ProgressRules.OnRestReached(data, "a", 0), Is.False, "Reaching the same rest again changes nothing.");
            Assert.That(ProgressRules.OnRestReached(data, "b", 1), Is.False, "Another stage is not the main one.");
            Assert.That(ProgressRules.OnStageEntered(data, catalog, "a", ProgressData.NoCheckpoint), Is.True, "A restart starts over.");
            Assert.That(data.mainCheckpoint, Is.EqualTo(ProgressData.NoCheckpoint));
            Object.DestroyImmediate(catalog);
        }

        [Test] public void ClearingUnlocksTheNextStageAndReplaysNeverMoveContinueBack()
        {
            var catalog = TwoStages();
            var data = ProgressRules.NewGame(catalog);
            ProgressRules.OnRestReached(data, "a", 1);
            Assert.That(ProgressRules.OnStageCleared(data, catalog, "a"), Is.True);
            Assert.That(data.cleared, Is.EqualTo(new[] { "a" }));
            Assert.That(data.unlocked, Is.EquivalentTo(new[] { "a", "b" }));
            Assert.That(data.mainStage, Is.EqualTo("b"));
            Assert.That(data.mainCheckpoint, Is.EqualTo(ProgressData.NoCheckpoint));
            Assert.That(ProgressRules.OnStageCleared(data, catalog, "a"), Is.False, "A clear is recorded once.");

            ProgressRules.OnRestReached(data, "b", 0);
            Assert.That(ProgressRules.OnStageEntered(data, catalog, "a", ProgressData.NoCheckpoint), Is.False, "Replaying a cleared stage...");
            Assert.That(ProgressRules.OnRestReached(data, "a", 0), Is.False);
            Assert.That(data.mainStage, Is.EqualTo("b"), "...keeps the continue point.");
            Assert.That(data.mainCheckpoint, Is.Zero);
            Assert.That(ProgressRules.OnStageEntered(data, catalog, "x", 0), Is.False, "Unknown stages are ignored.");
            Object.DestroyImmediate(catalog);
        }

        [Test] public void ClearingTheLastStageEndsContinue()
        {
            var catalog = TwoStages();
            var data = ProgressRules.NewGame(catalog);
            ProgressRules.OnStageCleared(data, catalog, "a");
            Assert.That(ProgressRules.OnStageCleared(data, catalog, "b"), Is.True);
            Assert.That(data.allCleared, Is.True);
            Assert.That(ProgressRules.CanContinue(data, SaveReadResult.Loaded, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(ProgressRules.AllClearedReason));
            Assert.That(ProgressRules.CanContinue(null, SaveReadResult.Missing, out reason), Is.False);
            Assert.That(reason, Is.EqualTo(ProgressRules.NoRecordReason));
            Assert.That(ProgressRules.CanContinue(null, SaveReadResult.Failed, out reason), Is.False);
            Assert.That(reason, Is.EqualTo(ProgressRules.UnreadableReason));
            Object.DestroyImmediate(catalog);
        }

        [Test] public void ProgressRecordsThatDoNotFitTheCatalogAreRejected()
        {
            var catalog = TwoStages();
            Assert.That(ProgressData.FromJson("{ not json", catalog), Is.Null);
            Assert.That(ProgressData.FromJson("{\"mainStage\":\"zzz\",\"unlocked\":[\"zzz\"],\"cleared\":[]}", catalog), Is.Null);
            var ok = ProgressData.FromJson("{\"mainStage\":\"a\",\"mainCheckpoint\":1,\"unlocked\":[\"a\",\"gone\"],\"cleared\":[]}", catalog);
            Assert.That(ok, Is.Not.Null);
            Assert.That(ok.unlocked, Is.EqualTo(new[] { "a" }), "Ids no longer in the catalog are dropped.");
            Assert.That(ok.mainCheckpoint, Is.EqualTo(1));
            Object.DestroyImmediate(catalog);
        }

        // ---- 7.1 / 7.3 settings ----

        [Test] public void OnlyBadSettingFieldsFallBackToDefaults()
        {
            var data = SettingsData.FromJson("{\"masterVolume\":40,\"sfxVolume\":250,\"shake\":-3,\"flash\":10,\"screenMode\":7,\"resolutionWidth\":1280}", out int fixedFields);
            Assert.That(data.masterVolume, Is.EqualTo(40));
            Assert.That(data.flash, Is.EqualTo(10));
            Assert.That(data.sfxVolume, Is.EqualTo(SettingsData.DefaultVolume));
            Assert.That(data.shake, Is.EqualTo(SettingsData.DefaultIntensity));
            Assert.That(data.screenMode, Is.EqualTo((int)ScreenModeSetting.ProjectDefault));
            Assert.That(data.resolutionWidth, Is.Zero, "A width without a height is dropped.");
            Assert.That(data.musicVolume, Is.EqualTo(SettingsData.DefaultVolume), "Missing fields take defaults.");
            Assert.That(fixedFields, Is.EqualTo(4));
            var broken = SettingsData.FromJson("{ nope", out fixedFields);
            Assert.That(fixedFields, Is.EqualTo(-1));
            Assert.That(broken.masterVolume, Is.EqualTo(SettingsData.DefaultVolume));
        }

        [Test] public void EachSettingsTabResetsOnlyItsOwnValues()
        {
            var data = new SettingsData { masterVolume = 10, shake = 20, bindingOverrides = "x" };
            data.ResetEnvironment();
            Assert.That(data.masterVolume, Is.EqualTo(100));
            Assert.That(data.shake, Is.EqualTo(20));
            Assert.That(data.bindingOverrides, Is.EqualTo("x"));
            data.ResetPresentation();
            Assert.That(data.shake, Is.EqualTo(100));
            Assert.That(data.bindingOverrides, Is.EqualTo("x"));
            data.ResetBindings();
            Assert.That(data.bindingOverrides, Is.Empty);
        }

        [Test] public void FileSavesReplaceWholeRecordsAndNeverDeleteUnreadableOnes()
        {
            string directory = Path.Combine(Path.GetTempPath(), "GlitchSaveTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                var files = new FileSaveStorage(directory);
                Assert.That(files.Read("progress", out _, out _), Is.EqualTo(SaveReadResult.Missing));
                Assert.That(files.Write("progress", "one", out _), Is.True);
                Assert.That(files.Write("progress", "two", out _), Is.True);
                Assert.That(files.Read("progress", out var text, out _), Is.EqualTo(SaveReadResult.Loaded));
                Assert.That(text, Is.EqualTo("two"));
                // A crash mid-write leaves only a temporary file beside the last good record.
                File.WriteAllText(Path.Combine(directory, "progress.json.tmp"), "half");
                Assert.That(files.Read("progress", out text, out _), Is.EqualTo(SaveReadResult.Loaded));
                Assert.That(text, Is.EqualTo("two"));

                var catalog = TwoStages();
                File.WriteAllText(Path.Combine(directory, "progress.json"), "{ broken");
                var saves = new OutGameSaves(catalog, files);
                saves.Load();
                Assert.That(saves.ProgressLoad, Is.EqualTo(SaveReadResult.Failed));
                Assert.That(saves.HasAnyProgress, Is.True, "A new game still asks before replacing it.");
                Assert.That(File.ReadAllText(Path.Combine(directory, "progress.json")), Is.EqualTo("{ broken"), "Never deleted on a failed read.");
                Assert.That(saves.SaveProgress(), Is.True, "With nothing loaded there is nothing to write over it.");
                Assert.That(File.ReadAllText(Path.Combine(directory, "progress.json")), Is.EqualTo("{ broken"));
                Assert.That(saves.DeleteProgress(), Is.True);
                Assert.That(File.Exists(Path.Combine(directory, "progress.json")), Is.False);
                Object.DestroyImmediate(catalog);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test] public void FailedWritesKeepTheRunMarkedUnsaved()
        {
            var catalog = TwoStages();
            var memory = new MemorySaveStorage();
            var saves = new OutGameSaves(catalog, memory);
            saves.Load();
            saves.StartNewProgress();
            memory.FailWrites = true;
            Assert.That(saves.SaveProgress(), Is.False);
            Assert.That(saves.ProgressUnsaved, Is.True);
            Assert.That(saves.SaveSettings(), Is.False);
            Assert.That(saves.SettingsUnsaved, Is.True);
            memory.FailWrites = false;
            Assert.That(saves.SaveProgress(), Is.True);
            Assert.That(saves.ProgressUnsaved, Is.False);
            Object.DestroyImmediate(catalog);
        }

        // ---- 5.1 display confirmation ----

        [Test] public void DisplayChangesRevertAfterFifteenSecondsUnlessKept()
        {
            int kept = 0, reverted = 0;
            var confirm = new DisplayConfirmation(() => kept++, () => reverted++);
            confirm.Begin();
            Assert.That(confirm.SecondsLeft, Is.EqualTo(15));
            Assert.That(confirm.Tick(14.9f), Is.False);
            Assert.That(confirm.Tick(.2f), Is.True);
            Assert.That(reverted, Is.EqualTo(1));
            Assert.That(confirm.Active, Is.False);
            confirm.Begin();
            confirm.Keep();
            confirm.Tick(20f);
            Assert.That(kept, Is.EqualTo(1));
            Assert.That(reverted, Is.EqualTo(1));
        }

        // ---- 6.5 scene changes ----

        [Test] public void SceneChangesIgnoreRequestsWhileBusyAndReportFailures()
        {
            int loads = 0;
            var transition = new SceneTransition(_ => { loads++; return null; });
            Assert.That(transition.Begin("A", () => true), Is.True);
            Assert.That(transition.Begin("B", () => true), Is.False, "One change at a time.");
            string failure = null;
            transition.Failed += message => failure = message;
            for (int i = 0; i < 20 && transition.Phase != TransitionPhase.Failed; i++) transition.Tick(.05f);
            Assert.That(loads, Is.EqualTo(1));
            Assert.That(failure, Is.EqualTo(SceneTransition.LoadFailedMessage), "A scene that cannot load is reported.");
            Assert.That(transition.IsBusy, Is.True, "The failure waits to be acknowledged.");
            transition.Acknowledge();
            Assert.That(transition.IsBusy, Is.False);
            Assert.That(transition.Cover, Is.Zero);
        }

        // ---- 3.1 menus ----

        private sealed class Page : MenuPanel
        {
            public readonly MenuEntry A, B;
            public Page(string name, bool dialog = false) : base(name, Color.black)
            {
                isDialog = dialog;
                A = Track(new MenuEntry(name + "-a", "A", null));
                B = Track(new MenuEntry(name + "-b", "B", null));
                root.Add(A); root.Add(B);
            }
            private readonly bool isDialog;
            public override bool IsDialog => isDialog;
        }

        [Test] public void EscClosesOnlyTheTopPageAndDialogsKeepThePageBelow()
        {
            var stack = new MenuStack();
            var settings = new Page("settings");
            var guide = new Page("guide");
            var dialog = new Page("dialog", true);
            int closed = 0;
            stack.Push(settings, () => closed++);
            stack.Push(guide);
            Assert.That(settings.Root.style.display.value, Is.EqualTo(DisplayStyle.None), "A page hides the page below.");
            stack.Push(dialog);
            Assert.That(guide.Root.style.display.value, Is.EqualTo(DisplayStyle.Flex), "A dialog keeps it visible.");
            Assert.That(stack.Back(), Is.True);
            Assert.That(stack.Top, Is.SameAs(guide));
            Assert.That(stack.Back(), Is.True);
            Assert.That(stack.Top, Is.SameAs(settings));
            Assert.That(settings.Root.style.display.value, Is.EqualTo(DisplayStyle.Flex), "Closing returns to the caller.");
            Assert.That(stack.Back(), Is.True);
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(stack.Back(), Is.False, "An empty stack leaves Esc to the pause.");
        }

        [Test] public void UnavailableItemsExplainInsteadOfActing()
        {
            int used = 0, refused = 0;
            var item = new MenuEntry("x", "X", () => used++);
            item.Refused += _ => refused++;
            item.SetAvailable(false, "이유");
            item.Activate();
            Assert.That(used, Is.Zero);
            Assert.That(refused, Is.EqualTo(1));
            Assert.That(item.UnavailableReason, Is.EqualTo("이유"));
            item.SetAvailable(true);
            item.Activate();
            Assert.That(used, Is.EqualTo(1));
        }

        [Test] public void ConfirmDialogsSelectTheCancellingAnswerAndEscChoosesIt()
        {
            var stack = new MenuStack();
            var dialog = new ConfirmDialog();
            int confirmed = 0, cancelled = 0;
            dialog.Setup("제목", "질문", new ConfirmDialog.Choice("ok", "확인", () => confirmed++),
                new ConfirmDialog.Choice("no", "취소", () => cancelled++, true));
            stack.Push(dialog);
            Assert.That(stack.Back(), Is.True);
            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(confirmed, Is.Zero);
            Assert.That(stack.Count, Is.Zero);
        }

        // ---- 5.4 keys ----

        [Test] public void KeySlotsFindTheirPrimaryKeysAndSpotDuplicates()
        {
            var asset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Game/Data/PrototypeControls.inputactions"));
            try
            {
                var keys = new KeyBindings(asset);
                Assert.That(keys.IsConfigured, Is.True);
                Assert.That(keys.Slots.Select(s => s.Label), Is.EqualTo(new[] { "왼쪽 이동", "오른쪽 이동", "점프", "공격", "글리치" }));
                int jump = keys.Slots.ToList().FindIndex(s => s.Action == "Jump");
                Assert.That(keys.FindConflict("<Keyboard>/j", jump), Is.EqualTo("공격"));
                Assert.That(keys.FindConflict("<Keyboard>/space", jump), Is.Null, "The slot's own key is no conflict.");
                Assert.That(keys.FindConflict("<Keyboard>/w", jump), Is.EqualTo("위 이동"));
                Assert.That(keys.FindConflict("<Keyboard>/k", jump), Is.Null);
                Assert.That(keys.Save(), Is.Empty);
                var glitch = asset.FindAction("Player/Glitch");
                glitch.ApplyBindingOverride(keys.Slots[4].BindingIndex, "<Keyboard>/k");
                string json = keys.Save();
                Assert.That(json, Does.Contain("<Keyboard>/k"));
                keys.ResetAll();
                Assert.That(keys.EffectivePath(4), Is.EqualTo("<Keyboard>/leftShift"));
                keys.Load(json);
                Assert.That(keys.EffectivePath(4), Is.EqualTo("<Keyboard>/k"));
                keys.Load("not json");
                Assert.That(keys.EffectivePath(4), Is.EqualTo("<Keyboard>/leftShift"), "Unreadable key settings fall back to defaults.");
            }
            finally { Object.DestroyImmediate(asset); }
        }

        // ---- assets ----

        [Test] public void CatalogAndBuildListStartAtTheTitle()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>("Assets/_Game/Data/Flow/StageCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out var error), Is.True, error);
            Assert.That(catalog.TitleScene, Is.EqualTo("Title"));
            Assert.That(catalog.Count, Is.EqualTo(1));
            Assert.That(catalog[0].Scene, Is.EqualTo("CoreLoopStage"));
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).ToList();
            Assert.That(scenes.First(), Is.EqualTo("Title"), "The game starts at the title.");
            for (int i = 0; i < catalog.Count; i++) Assert.That(scenes, Does.Contain(catalog[i].Scene));
        }

        [Test] public void GameManagerPrefabWiresTheOutGameLayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Resources/GameManager.prefab");
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/UI/Cinematics/Fonts/NotoSansKR-Regular.otf");
            var outGame = prefab.GetComponent<OutGameView>();
            Assert.That(outGame, Is.Not.Null);
            var outData = new SerializedObject(outGame);
            Assert.That(outData.FindProperty("document").objectReferenceValue, Is.SameAs(prefab.GetComponent<UIDocument>()));
            Assert.That(outData.FindProperty("font").objectReferenceValue, Is.SameAs(font));
            Assert.That(new SerializedObject(prefab.GetComponent<StageFlowView>()).FindProperty("font").objectReferenceValue, Is.SameAs(font));
            var manager = new SerializedObject(prefab.GetComponent<GameManager>());
            Assert.That(manager.FindProperty("outGame").objectReferenceValue, Is.SameAs(outGame));
            Assert.That(manager.FindProperty("catalog").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<StageCatalog>("Assets/_Game/Data/Flow/StageCatalog.asset")));
            Assert.That(manager.FindProperty("playerControls").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Game/Data/PrototypeControls.inputactions")));
            Assert.That(manager.FindProperty("previewClip").objectReferenceValue, Is.Not.Null);
            var components = prefab.GetComponents<MonoBehaviour>().Select(c => c.GetType()).ToList();
            Assert.That(components.IndexOf(typeof(OutGameView)), Is.GreaterThan(components.IndexOf(typeof(StageFlowView))),
                "The out-game sections are added after the stage HUD.");
        }

        [Test] public void TitleSceneHasOneTitleMarkerAndACamera()
        {
            const string path = "Assets/_Game/Scenes/Title.unity";
            var existing = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            var scene = existing.isLoaded ? existing : UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,
                UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<TitleScreen>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<AudioListener>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<StageLayout>(true)), Is.Empty);
            }
            finally
            {
                if (!existing.isLoaded) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
