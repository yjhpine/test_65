using System.Linq;
using ActionPlatformer.Flow;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ActionPlatformer.Tests
{
    public sealed class GameManagerEditModeTests
    {
        [Test] public void InstanceAndExistingAreNullOutsidePlayMode()
        {
            Assert.That(GameManager.Instance, Is.Null);
            Assert.That(GameManager.Existing, Is.Null);
        }

        [Test] public void GameManagerPrefabIsWired()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Resources/GameManager.prefab");
            Assert.That(prefab, Is.Not.Null);
            var document = prefab.GetComponent<UIDocument>();
            Assert.That(document.panelSettings, Is.SameAs(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/StagePanelSettings.asset")));
            // Same order as the scene Stage UI it replaces, so cutscene dialogue keeps its layering.
            Assert.That(document.sortingOrder, Is.EqualTo(100f));
            var view = prefab.GetComponent<StageFlowView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(new SerializedObject(view).FindProperty("document").objectReferenceValue, Is.SameAs(document));
            Assert.That(new SerializedObject(prefab.GetComponent<GameManager>()).FindProperty("view").objectReferenceValue, Is.SameAs(view));
        }

        [Test] public void PauseMenuAndPauseInputAreWired()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Resources/GameManager.prefab");
            var menu = prefab.GetComponent<PauseMenuView>();
            Assert.That(menu, Is.Not.Null);
            var menuData = new SerializedObject(menu);
            Assert.That(menuData.FindProperty("document").objectReferenceValue, Is.SameAs(prefab.GetComponent<UIDocument>()));
            Assert.That(menuData.FindProperty("fontAsset").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>("Assets/_Game/UI/Cinematics/Fonts/NotoSansKR-Regular-SDF.asset")));
            var managerData = new SerializedObject(prefab.GetComponent<GameManager>());
            Assert.That(managerData.FindProperty("pauseMenu").objectReferenceValue, Is.SameAs(menu));
            var controls = managerData.FindProperty("controls").objectReferenceValue as InputActionAsset;
            Assert.That(controls, Is.SameAs(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Game/Data/GameControls.inputactions")));
            var pause = controls.FindAction("Game/Pause");
            Assert.That(pause, Is.Not.Null);
            Assert.That(pause.type, Is.EqualTo(InputActionType.Button));
            Assert.That(pause.bindings.Select(b => b.path), Is.EquivalentTo(new[] { "<Keyboard>/escape", "<Gamepad>/start" }));
        }

        [Test] public void CoreLoopStageHasOneLayoutAndNoSceneHud()
        {
            const string path = "Assets/_Game/Scenes/CoreLoopStage.unity";
            var existing = SceneManager.GetSceneByPath(path);
            var scene = existing.isLoaded ? existing : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<StageLayout>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<StageFlowView>(true)), Is.Empty);
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<GameManager>(true)), Is.Empty);
            }
            finally { if (!existing.isLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
