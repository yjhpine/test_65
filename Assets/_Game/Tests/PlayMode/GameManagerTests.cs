using System;
using System.Collections;
using ActionPlatformer.Flow;
using ActionPlatformer.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ActionPlatformer.Tests
{
    public sealed class GameManagerTests
    {
        private const string CleanupSceneName = "GameManagerTestCleanup";
        private float savedScale, savedFixed;

        [SetUp] public void SaveTime()
        {
            savedScale = Time.timeScale;
            savedFixed = Time.fixedDeltaTime;
            Time.timeScale = 1f;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
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
        }

        private static IEnumerator LoadSceneWithoutStage()
        {
            yield return SceneManager.LoadSceneAsync("MovementLab");
            yield return null;
        }

        [UnityTest] public IEnumerator InstanceIsCreatedOnceAndLivesInDontDestroyOnLoad()
        {
            var game = GameManager.Instance;
            Assert.That(game, Is.Not.Null);
            Assert.That(GameManager.Existing, Is.SameAs(game));
            Assert.That(game.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            Assert.That(Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest] public IEnumerator ReloadKeepsTheSameManagerAndBindsTheNewStage()
        {
            var game = GameManager.Instance;
            PlayerUnit previousPlayer = null;
            for (int load = 0; load < 2; load++)
            {
                yield return LoadStage();
                Assert.That(GameManager.Instance, Is.SameAs(game));
                Assert.That(Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
                if (load > 0) Assert.That(previousPlayer == null, Is.True, "The previous stage's player must be destroyed.");
                Assert.That(game.Stage.Player.gameObject.scene, Is.EqualTo(SceneManager.GetActiveScene()));
                Assert.That(game.Player, Is.SameAs(game.Stage.Player));
                Assert.That(game.State, Is.EqualTo(StageState.Playing));
                Assert.That(game.View.IsStageVisible, Is.True);
                previousPlayer = game.Player;
            }
        }

        [UnityTest] public IEnumerator SceneWithoutStageHidesStageHudAndLeavesTimeAlone()
        {
            yield return LoadSceneWithoutStage();
            var game = GameManager.Instance;
            Assert.That(game.HasStage, Is.False);
            Assert.That(game.View.IsStageVisible, Is.False);
            Time.timeScale = .5f;
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(.5f));
        }

        [UnityTest] public IEnumerator UnloadingTheStageRestoresTimeAndHidesHud()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            Time.timeScale = .3f;
            Time.fixedDeltaTime = .01f;
            yield return LoadSceneWithoutStage();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(savedFixed).Within(.00001f));
            Assert.That(game.HasStage, Is.False);
            Assert.That(game.View.IsStageVisible, Is.False);
        }

        [UnityTest] public IEnumerator SecondStageLayoutIsRejected()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            var stage = game.Stage;
            int version = game.StageVersion;
            LogAssert.Expect(LogType.Error, GameManager.DuplicateStageMessage);
            new GameObject("Extra stage").AddComponent<StageLayout>();
            yield return null;
            yield return null;
            Assert.That(game.Stage, Is.SameAs(stage));
            Assert.That(game.StageVersion, Is.EqualTo(version));
        }

        [UnityTest] public IEnumerator StageThatFailsValidationIsNotRegistered()
        {
            yield return LoadSceneWithoutStage();
            var game = GameManager.Instance;
            int version = game.StageVersion;
            LogAssert.Expect(LogType.Error, GameManager.ConfigurationErrorMessage);
            new GameObject("Invalid stage").AddComponent<StageLayout>();
            yield return null;
            Assert.That(game.HasStage, Is.False);
            Assert.That(game.StageVersion, Is.EqualTo(version));
            Assert.That(game.View.IsStageVisible, Is.False);
        }

        [UnityTest] public IEnumerator StageViewKeepsOtherSectionsWhenReenabled()
        {
            var game = GameManager.Instance;
            var view = game.View;
            var root = view.GetComponent<UIDocument>().rootVisualElement;
            var sibling = new VisualElement { name = "test-sibling-section" };
            root.Add(sibling);
            try
            {
                view.enabled = false;
                view.enabled = true;
                yield return null;
                root = view.GetComponent<UIDocument>().rootVisualElement;
                Assert.That(sibling.parent, Is.SameAs(root), "A sibling section must survive re-enabling the view.");
                Assert.That(root.Query(name: "stage-hud").ToList().Count, Is.EqualTo(1));
            }
            finally { sibling.RemoveFromHierarchy(); }
        }

        [UnityTest] public IEnumerator DisablingTheManagerReleasesTheStage()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            float scale = Time.timeScale, fixedStep = Time.fixedDeltaTime;
            try
            {
                game.enabled = false;
                Assert.That(game.HasStage, Is.False);
                Assert.That(game.View.IsStageVisible, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(scale));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(.00001f));
            }
            finally { game.enabled = true; }
        }

        [UnityTest] public IEnumerator SetupErrorDuringPlayUnregistersAndRestoresTime()
        {
            yield return LoadStage();
            var game = GameManager.Instance;
            float scale = Time.timeScale, fixedStep = Time.fixedDeltaTime;
            Time.timeScale = .4f;
            LogAssert.Expect(LogType.Error, GameManager.ConfigurationErrorMessage);
            Object.Destroy(game.ActiveEnemies[0].gameObject);
            yield return null;
            yield return null;
            Assert.That(game.HasStage, Is.False);
            Assert.That(game.View.IsStageVisible, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(scale));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(.00001f));
            Assert.That(game.CanRequestTransfer, Is.False);
        }
    }
}
