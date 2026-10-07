using System;
using System.Collections;
using System.Linq;
using ActionPlatformer.Cinematics;
using ActionPlatformer.Flow;
using ActionPlatformer.Player;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace ActionPlatformer.Tests
{
    public sealed class CutsceneTests
    {
        private CutsceneRunner runner;
        private PlayerUnit player;
        private StageFlowController stage;
        private CutsceneTrigger trigger;
        private float scale, fixedDelta;
        private bool background;
        private int finished;
        private CutsceneEndReason reason;
        [UnitySetUp] public IEnumerator Setup()
        {
            finished = 0; reason = default;
            scale = Time.timeScale; fixedDelta = Time.fixedDeltaTime;
            background = Application.runInBackground; Application.runInBackground = true;
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("CoreLoopStage");
            yield return null;
            runner = Object.FindFirstObjectByType<CutsceneRunner>();
            player = runner.Player; stage = Object.FindFirstObjectByType<StageFlowController>();
            trigger = Object.FindFirstObjectByType<CutsceneTrigger>();
            Assert.That(stage.isActiveAndEnabled, Is.True);
            runner.Finished += OnFinished;
            yield return new WaitForFixedUpdate();
        }
        private void OnFinished(CutsceneEndReason value) { finished++; reason = value; }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (runner != null) { runner.Cancel(); runner.Finished -= OnFinished; }
            var old = SceneManager.GetSceneByName("CoreLoopStage");
            var empty = SceneManager.CreateScene("CutsceneTestCleanup");
            SceneManager.SetActiveScene(empty);
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            Time.timeScale = scale; Time.fixedDeltaTime = fixedDelta; Application.runInBackground = background;
        }
        private IEnumerator Until(Func<bool> predicate, float seconds = 5)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Timed out; state=" + (runner != null ? runner.State.ToString() : "destroyed"));
        }
        private void AssertRestored()
        {
            Assert.That(runner.State, Is.EqualTo(CutsceneState.Idle));
            Assert.That(runner.Coordinator.IsBusy, Is.False);
            Assert.That(runner.Coordinator.CameraRig.Brain.enabled, Is.False);
            Assert.That(player.IsControlLocked, Is.False);
            Assert.That(player.GetComponent<PlayerInput>().inputIsActive, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedDelta));
            Assert.That(runner.View.IsDialogueVisible, Is.False);
            Assert.That(Object.FindFirstObjectByType<StageCameraFollow>().enabled, Is.True);
            Assert.That(Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).All(u => u.Fsm == null || !u.Fsm.IsPaused), Is.True);
            Assert.That(Camera.main.orthographicSize, Is.EqualTo(6.5f).Within(.001f));
        }
        [UnityTest] public IEnumerator TriggerFreezesWorldTypesThreeSpeakersAndRestoresGameplay()
        {
            player.Motor.Teleport(new Vector2(6, .82f)); Physics2D.SyncTransforms();
            yield return Until(() => runner.IsRunning);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(player.IsControlLocked, Is.True);
            var enemies = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).Where(u => u != player).ToArray();
            var positions = enemies.Select(u => u.transform.position).ToArray();
            var states = enemies.Select(u => u.Fsm?.CurrentState).ToArray();
            Assert.That(enemies.All(u => u.Fsm == null || u.Fsm.IsPaused), Is.True);
            yield return Until(() => runner.State == CutsceneState.Waiting);
            double hold = runner.CurrentTime;
            Assert.That(Camera.main.transform.position.x, Is.EqualTo(7.5f).Within(.1f));
            Assert.That(Camera.main.orthographicSize, Is.EqualTo(4.5f).Within(.01f));
            for (int i = 0; i < enemies.Length; i++)
            { Assert.That(enemies[i].transform.position, Is.EqualTo(positions[i])); Assert.That(enemies[i].Fsm?.CurrentState, Is.SameAs(states[i])); }
            yield return new WaitForSecondsRealtime(.05f);
            Assert.That(runner.CurrentTime, Is.EqualTo(hold));
            runner.Confirm(); Assert.That(runner.Dialogue.IsFullyRevealed, Is.True);
            Assert.That(runner.View.VisibleText, Does.Contain("발판"));
            runner.Confirm(); Assert.That(runner.State, Is.EqualTo(CutsceneState.Waiting));
            Assert.That(runner.CurrentSpeaker, Is.SameAs(player.GetComponent<DialogueSpeaker>()));
            runner.Confirm(); Assert.That(runner.View.VisibleText, Does.Contain("출구"));
            runner.Confirm();
            Assert.That(runner.CurrentSpeaker.DisplayName, Is.EqualTo("안내자"));
            runner.Confirm(); Assert.That(runner.View.VisibleText, Does.Contain("모든 적"));
            runner.Confirm();
            yield return Until(() => !runner.IsRunning);
            Assert.That(finished, Is.EqualTo(1)); Assert.That(reason, Is.EqualTo(CutsceneEndReason.Completed));
            Assert.That(trigger.HasCompleted, Is.True); AssertRestored();
        }
        [UnityTest] public IEnumerator CameraFollowsCurrentPlayerAfterCompletionSkipAndCancel()
        {
            trigger.enabled = false;
            foreach (var end in new[] { CutsceneEndReason.Completed, CutsceneEndReason.Skipped, CutsceneEndReason.Cancelled })
            {
                Assert.That(runner.TryPlay(), Is.True);
                yield return Until(() => runner.State == CutsceneState.Waiting);
                if (end == CutsceneEndReason.Completed)
                {
                    while (runner.State == CutsceneState.Waiting) { runner.Dialogue.Reveal(); runner.Confirm(); }

                }
                else if (end == CutsceneEndReason.Skipped) runner.Skip();
                else runner.Cancel();
                yield return Until(() => !runner.IsRunning);
                Assert.That(reason, Is.EqualTo(end));
                AssertRestored();
                foreach (float x in new[] { 12f, 16f })
                {
                    player.Motor.Teleport(new Vector2(x, 5f));
                    yield return new WaitForFixedUpdate(); yield return null; yield return null;
                    Assert.That(Camera.main.transform.position.x, Is.EqualTo(player.Motor.Position.x).Within(.03f));
                    Assert.That(Camera.main.transform.localPosition.x, Is.EqualTo(0).Within(.001f));
                }
            }
        }
        [UnityTest] public IEnumerator SkipDuringMotionAndWaitingFinishesOnlyOnce()
        {
            Assert.That(runner.TryPlay(), Is.True); runner.Skip(); runner.Skip();
            yield return Until(() => !runner.IsRunning);
            Assert.That(finished, Is.EqualTo(1)); Assert.That(reason, Is.EqualTo(CutsceneEndReason.Skipped));
            AssertRestored();
            Assert.That(runner.TryPlay(), Is.True);
            yield return Until(() => runner.State == CutsceneState.Waiting);
            runner.Skip();
            yield return Until(() => !runner.IsRunning);
            Assert.That(finished, Is.EqualTo(2)); AssertRestored();
        }
        [UnityTest] public IEnumerator ContinuingWorldFallsAndDamageCancelsBeforeDamageFeedback()
        {
            trigger.enabled = false;
            runner.WorldMode = CutsceneWorldMode.Continue;
            player.Motor.Teleport(new Vector2(8, 6));
            Assert.That(runner.TryPlay(), Is.True);
            float initialY = player.Motor.Position.y;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(player.Motor.Position.y, Is.LessThan(initialY));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(player.GetComponent<UnitHealth>().ApplyDamage(1), Is.EqualTo(1));
            yield return Until(() => !runner.IsRunning);
            Assert.That(reason, Is.EqualTo(CutsceneEndReason.Damaged));
            Assert.That(player.Feedback.CameraEffectsSuppressed, Is.False);
            Assert.That(player.Feedback.IsDamageFeedbackActive(Time.unscaledTimeAsDouble), Is.True);
            yield return new WaitForSecondsRealtime(.6f);
            AssertRestored();
        }
        [UnityTest] public IEnumerator CancelRequiresExitBeforeTriggerRetryAndCompletedTriggerDoesNotReplay()
        {
            player.Motor.Teleport(new Vector2(6, .82f)); Physics2D.SyncTransforms();
            yield return Until(() => runner.IsRunning);
            runner.Cancel();
            yield return null;
            Assert.That(runner.IsRunning, Is.False); Assert.That(trigger.HasCompleted, Is.False);
            player.Motor.Teleport(new Vector2(3, .82f)); Physics2D.SyncTransforms(); yield return null;
            player.Motor.Teleport(new Vector2(6, .82f)); Physics2D.SyncTransforms();
            yield return Until(() => runner.IsRunning);
            runner.Skip(); yield return Until(() => !runner.IsRunning);
            player.Motor.Teleport(new Vector2(3, .82f)); Physics2D.SyncTransforms(); yield return null;
            player.Motor.Teleport(new Vector2(6, .82f)); Physics2D.SyncTransforms(); yield return null;
            Assert.That(runner.IsRunning, Is.False); AssertRestored();
        }
        [UnityTest] public IEnumerator DuplicateRunnerCannotAcquireAndDisableReleasesAllState()
        {
            Assert.That(runner.TryPlay(), Is.True);
            Assert.That(runner.TryPlay(), Is.False);
            var other = Object.Instantiate(runner.gameObject).GetComponent<CutsceneRunner>();
            Assert.That(other.TryPlay(), Is.False);
            Object.Destroy(other.gameObject);
            runner.enabled = false;
            Assert.That(reason, Is.EqualTo(CutsceneEndReason.Cancelled)); AssertRestored();
            yield return null;
        }
        [UnityTest] public IEnumerator ExternalTimeChangeAndInactiveInputAreNotOverwritten()
        {
            player.GetComponent<PlayerInput>().DeactivateInput();
            Assert.That(runner.TryPlay(), Is.True);
            Time.timeScale = .5f; Time.fixedDeltaTime = .03f;
            runner.Cancel();
            Assert.That(Time.timeScale, Is.EqualTo(.5f)); Assert.That(Time.fixedDeltaTime, Is.EqualTo(.03f).Within(.000001f));
            Assert.That(player.GetComponent<PlayerInput>().inputIsActive, Is.False);
            Assert.That(player.IsControlLocked, Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator SceneUnloadWhileWaitingRestoresTime()
        {
            Assert.That(runner.TryPlay(), Is.True);
            yield return Until(() => runner.State == CutsceneState.Waiting);
            var scene = runner.gameObject.scene;
            var empty = SceneManager.CreateScene("CutsceneUnloadTarget"); SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedDelta));
        }

        [UnityTest] public IEnumerator LethalDamageCancelsAndReloadsIntoPlayableStage()
        {
            trigger.enabled = false;
            runner.WorldMode = CutsceneWorldMode.Continue;
            Assert.That(runner.TryPlay(), Is.True);
            var previousStage = stage;
            var health = player.GetComponent<UnitHealth>();
            health.ApplyDamage(health.MaxHealth);
            yield return Until(() => !runner.IsRunning);
            Assert.That(reason, Is.EqualTo(CutsceneEndReason.Damaged));
            yield return Until(() => previousStage == null);
            yield return null;
            var next = Object.FindFirstObjectByType<StageFlowController>();
            Assert.That(next.State, Is.EqualTo(StageState.Playing));
            Assert.That(next.Player.IsControlLocked, Is.False);
            Assert.That(next.Player.GetComponent<PlayerInput>().inputIsActive, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator VirtualKeyboardConfirmsAndSkipsWithoutGameplayInput()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
#if UNITY_EDITOR
            var previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            try
            {
                Assert.That(runner.TryPlay(), Is.True);
                yield return Until(() => runner.State == CutsceneState.Waiting);
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.Enter));
                yield return null; yield return null;
                Assert.That(runner.Dialogue.IsFullyRevealed, Is.True);
                Assert.That(player.AttackPhase, Is.EqualTo(PlayerAttackPhase.Ready));
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.Escape));
                yield return Until(() => !runner.IsRunning);
                Assert.That(reason, Is.EqualTo(CutsceneEndReason.Skipped)); AssertRestored();
            }
            finally
            {
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
#endif
                InputSystem.RemoveDevice(keyboard);
            }
        }
        [UnityTest] public IEnumerator OverlappingDialogueFailsValidationWithoutLockingPlayer()
        {
            var tracks = ((TimelineAsset)runner.Director.playableAsset).GetOutputTracks().Where(CutsceneRunner.IsDialogueTrack);
            var clips = tracks.SelectMany(t => t.GetClips()).OrderBy(c => c.start).ToArray();
            double original = clips[1].start;
            try
            {
                clips[1].start = clips[0].start;
                Assert.That(runner.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("겹칠"));
                Assert.That(player.IsControlLocked, Is.False);
                Assert.That(runner.Coordinator.IsBusy, Is.False);
                yield return null;
            }
            finally { clips[1].start = original; }
        }


        [UnityTest] public IEnumerator SpeakerBindingsArePreservedAndMissingSpeakerIsRejected()
        {
            var track = ((TimelineAsset)runner.Director.playableAsset).GetOutputTracks().OfType<SpeechBubbleTrack>().First();
            var actor = runner.Director.GetGenericBinding(track);
            runner.BindOutputs();
            Assert.That(runner.Director.GetGenericBinding(track), Is.SameAs(actor));
            try
            {
                runner.Director.ClearGenericBinding(track);
                Assert.That(runner.TryValidate(out var error), Is.False);
                Assert.That(error, Does.Contain("화자"));
                Assert.That(player.IsControlLocked, Is.False);
            }
            finally { runner.Director.SetGenericBinding(track, actor); }
            yield return null;
        }

        [UnityTest] public IEnumerator DisabledSpeakerAndDestroyedAnchorCancelAndReleaseGameplay()
        {
            trigger.enabled = false;
            var guide = Object.FindObjectsByType<DialogueSpeaker>(FindObjectsSortMode.None).Single(s => s != player.GetComponent<DialogueSpeaker>());
            Assert.That(runner.TryPlay(), Is.True);
            yield return Until(() => runner.State == CutsceneState.Waiting);
            guide.gameObject.SetActive(false);
            yield return Until(() => !runner.IsRunning);
            Assert.That(reason, Is.EqualTo(CutsceneEndReason.Cancelled)); AssertRestored();
            guide.gameObject.SetActive(true);
            Assert.That(runner.TryPlay(), Is.True);
            yield return Until(() => runner.State == CutsceneState.Waiting);
            Object.Destroy(guide.Anchor.gameObject);
            yield return Until(() => !runner.IsRunning);
            Assert.That(reason, Is.EqualTo(CutsceneEndReason.Cancelled)); AssertRestored();
        }

        [UnityTest] public IEnumerator BubbleFollowsAnchorWhileWaitingAndKeepsLayoutDuringTypingAndZoom()
        {
            trigger.enabled = false;
            Assert.That(runner.TryPlay(), Is.True);
            yield return Until(() => runner.State == CutsceneState.Waiting);
            yield return null; yield return null;
            Assert.That(runner.View.IsBubbleVisible, Is.True);
            var original = runner.View.BubbleBounds;
            var actor = runner.CurrentSpeaker;
            actor.Anchor.localPosition += Vector3.right;
            yield return null; yield return null;
            Assert.That(runner.View.BubbleBounds.x, Is.GreaterThan(original.x));
            runner.Confirm();
            yield return null; yield return null;
            Assert.That(runner.View.BubbleBounds.size.x, Is.EqualTo(original.size.x).Within(.1f));
            Assert.That(runner.View.BubbleBounds.size.y, Is.EqualTo(original.size.y).Within(.1f));
            var camera = runner.transform.Find("Conversation Camera").GetComponent<Unity.Cinemachine.CinemachineCamera>();
            camera.Lens.OrthographicSize = 3.5f;
            yield return null; yield return null;
            Assert.That(runner.View.BubbleBounds.size.x, Is.EqualTo(original.size.x).Within(.1f));
            Assert.That(runner.View.BubbleBounds.size.y, Is.EqualTo(original.size.y).Within(.1f));
            actor.Anchor.position = new Vector3(-100, 100, 0);
            yield return null; yield return null;
            Assert.That(runner.View.BubbleBounds.x, Is.GreaterThanOrEqualTo(15.9f));
            Assert.That(runner.View.BubbleBounds.y, Is.GreaterThanOrEqualTo(15.9f));
            Assert.That(runner.State, Is.EqualTo(CutsceneState.Waiting));
            runner.Cancel(); AssertRestored();
        }

#if UNITY_EDITOR
        [UnityTest] public IEnumerator ExistingBottomDialogueTimelineStillPlaysWithoutAssetMigration()
        {
            trigger.enabled = false;
            var conversation = runner.Director.playableAsset;
            try
            {
                runner.Director.playableAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TimelineAsset>("Assets/_Game/Cinematics/IntroCutscene.playable");
                runner.BindOutputs();
                Assert.That(runner.TryPlay(), Is.True);
                yield return Until(() => runner.State == CutsceneState.Waiting);
                Assert.That(runner.View.IsDialogueVisible, Is.True);
                Assert.That(runner.View.IsBubbleVisible, Is.False);
                runner.Confirm(); Assert.That(runner.View.VisibleText, Does.Contain("발판"));
                runner.Confirm();
                runner.Confirm(); Assert.That(runner.View.VisibleText, Does.Contain("출구"));
                runner.Confirm(); yield return Until(() => !runner.IsRunning);
                Assert.That(reason, Is.EqualTo(CutsceneEndReason.Completed)); AssertRestored();
            }
            finally { runner.Cancel(); runner.Director.playableAsset = conversation; runner.BindOutputs(); }
        }
#endif

        [UnityTest] public IEnumerator TimedDialogueAdvancesWithoutConfirmation()
        {
            trigger.enabled = false;
            var clips = ((TimelineAsset)runner.Director.playableAsset).GetOutputTracks().Where(CutsceneRunner.IsDialogueTrack)
                .SelectMany(t => t.GetClips()).Select(c => (DialogueClip)c.asset).ToArray();
            var modes = clips.Select(c => c.Progress).ToArray();
            try
            {
                foreach (var clip in clips) clip.Progress = DialogueProgress.Timed;
                Assert.That(runner.TryPlay(), Is.True);
                yield return Until(() => !runner.IsRunning);
                Assert.That(reason, Is.EqualTo(CutsceneEndReason.Completed)); AssertRestored();
            }
            finally { for (int i = 0; i < clips.Length; i++) clips[i].Progress = modes[i]; }
        }
    }
}
