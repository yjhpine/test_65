using System.Collections;
using ActionPlatformer.Flow;
using NUnit.Framework;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    // Every Play Mode test starts from memory saves and default settings, so a developer's own save files
    // (volumes, key changes, progress) never leak into a run, and a quit button cannot end Play Mode.
    [SetUpFixture]
    public sealed class OutGameTestSetup
    {
        [OneTimeSetUp]
        public void UseMemorySaves()
        {
            SaveStorage.Use(new MemorySaveStorage());
            GameManager.QuitHandler = () => { };
            GameManager.Instance?.ReloadSaves(SaveStorage.Current);
        }

        [OneTimeTearDown]
        public void Restore()
        {
            GameManager.QuitHandler = null;
            GameManager.Existing?.KeyBindings?.ResetAll();
        }
    }

    public static class OutGameTestSupport
    {
        // After a death the failure screen waits for 재도전; tests press it as a player would.
        public static IEnumerator RetryAfterDeath(GameManager game, float timeout = 6f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (game.State != StageState.Failed && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.State, Is.EqualTo(StageState.Failed), "The failure screen should follow the death presentation.");
            Assert.That(game.View.IsFailedVisible, Is.True);
            game.View.ResultItem("fail-retry").Activate();
        }

        // Play input returns only after the loading cover has lifted.
        public static IEnumerator WaitForTransition(GameManager game, float timeout = 8f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (game.Transition.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.Transition.IsBusy, Is.False, "The scene change should finish.");
        }

        // Fresh, empty saves for one test.
        public static MemorySaveStorage FreshSaves(GameManager game)
        {
            var storage = new MemorySaveStorage();
            SaveStorage.Use(storage);
            game.ReloadSaves(storage);
            return storage;
        }
    }
}
