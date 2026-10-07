using System;
using System.Collections;
using System.Linq;
using ActionPlatformer.Cinematics;
using ActionPlatformer.Units.Fsm;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace ActionPlatformer.Tests
{
    public sealed class CutsceneAuthoringTests
    {
        private sealed class State : IFsmState
        {
            public int Enters, Ticks, Exits;
            public void Enter() { Enters++; }
            public IFsmState Tick(float delta) { Ticks++; return null; }
            public void Exit() { Exits++; }
        }
        [Test] public void NestedPauseHandlesPreserveFsmAndDisposeIdempotently()
        {
            var state = new State(); var fsm = new FsmRuntime(state); fsm.Start();
            var a = fsm.Pause(); var b = fsm.Pause();
            fsm.Tick(1); a.Dispose(); a.Dispose(); fsm.Tick(1);
            Assert.That(state.Ticks, Is.Zero); Assert.That(state.Enters, Is.EqualTo(1)); Assert.That(state.Exits, Is.Zero);
            b.Dispose(); fsm.Tick(.1f);
            Assert.That(state.Ticks, Is.EqualTo(1)); Assert.That(fsm.CurrentState, Is.SameAs(state));
        }
        [UnityTest] public IEnumerator EndingTimelinePreviewImmediatelyRestoresCameraOwnership()
        {
            const string path = "Assets/_Game/Scenes/CoreLoopStage.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            var runner = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CutsceneRunner>(true)).Single();
            var follow = runner.Coordinator.CameraRig.Output.transform.parent.GetComponent<ActionPlatformer.Flow.StageCameraFollow>();
            var window = UnityEditor.Timeline.TimelineEditor.GetOrCreateWindow();
            bool followEnabled = follow.enabled, brainEnabled = runner.Coordinator.CameraRig.Brain.enabled;
            try
            {
                window.SetTimeline(runner.Director);
                window.playbackControls.Play(); window.playbackControls.Pause();
                window.playbackControls.SetCurrentTime(1.9);
                // The custom preview runs on the next Editor update.
                for (int i = 0; i < 10 && !runner.Coordinator.CameraRig.IsAcquired; i++) yield return null;
                Assert.That(runner.Coordinator.CameraRig.IsAcquired, Is.True);
                Assert.That(follow.enabled, Is.False);
                yield return null; yield return null;
                Assert.That(runner.View.IsBubbleVisible, Is.True);
                Assert.That(runner.View.VisibleText, Is.EqualTo("앞쪽 발판을 따라 이동해 보세요."));
                Assert.That(runner.State, Is.EqualTo(CutsceneState.Idle));
                window.playbackControls.SetCurrentTime(2.3);
                yield return null; yield return null;
                Assert.That(runner.CurrentSpeaker.DisplayName, Is.EqualTo("플레이어"));
                Assert.That(runner.View.VisibleText, Is.EqualTo("출구는 어떻게 열죠?"));
                window.playbackControls.SetCurrentTime(.15);
                yield return null; yield return null;
                Assert.That(runner.View.IsDialogueVisible, Is.False);
                window.playbackControls.SetCurrentTime(1.9);
                yield return null; yield return null;
                Assert.That(runner.View.VisibleText, Is.EqualTo("앞쪽 발판을 따라 이동해 보세요."));
                window.ClearTimeline();
                // No extra Editor update or manual End call may be needed to restore ownership.
                Assert.That(follow.enabled, Is.EqualTo(followEnabled));
                Assert.That(runner.Coordinator.CameraRig.Brain.enabled, Is.EqualTo(brainEnabled));
            }
            finally
            {
                window.ClearTimeline(); ActionPlatformer.Editor.CutscenePreview.End();
                if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }

        [UnityTest] public IEnumerator InspectorAddsBoundSpeakerTrackAfterExistingDialogue()
        {
            const string folder = "Assets/_Game/Tests/SpeakerAuthoringGeneratedTest";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var prefab = ActionPlatformer.Editor.CutsceneAuthoring.CreateTemplate(folder + "/Template.prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var runner = instance.GetComponent<CutsceneRunner>();
                var actor = new GameObject("Test Speaker", typeof(DialogueSpeaker));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, scene);
                var speaker = actor.GetComponent<DialogueSpeaker>();
                var anchor = new GameObject("SpeechAnchor"); anchor.transform.SetParent(actor.transform);
                var settings = new SerializedObject(speaker);
                settings.FindProperty("anchor").objectReferenceValue = anchor.transform;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var timeline = (TimelineAsset)runner.Director.playableAsset;
                double end = timeline.GetOutputTracks().OfType<DialogueTrack>().SelectMany(t => t.GetClips()).Max(c => c.end);
                var track = ActionPlatformer.Editor.SpeechBubbleAuthoring.AddSpeakerTrack(runner, speaker);
                runner.BindOutputs();
                Assert.That(runner.Director.GetGenericBinding(track), Is.SameAs(speaker));
                Assert.That(track.GetClips().Single().start, Is.EqualTo(end));
                Assert.That(track.GetClips().Single().asset, Is.TypeOf<DialogueClip>());
                Assert.That(timeline.GetOutputTracks().OfType<DialogueTrack>().Count(), Is.EqualTo(1));
                yield return null;
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [UnityTest] public IEnumerator TemplateCreatesIndependentTimelineAndLocalBindings()
        {
            const string folder = "Assets/_Game/Tests/CutsceneGeneratedTest";
            Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
            try
            {
                var a = ActionPlatformer.Editor.CutsceneAuthoring.CreateTemplate(folder + "/A.prefab");
                var b = ActionPlatformer.Editor.CutsceneAuthoring.CreateTemplate(folder + "/B.prefab");
                var first = a.GetComponent<CutsceneRunner>(); var second = b.GetComponent<CutsceneRunner>();
                Assert.That(first.Director.playableAsset, Is.Not.SameAs(second.Director.playableAsset));
                var tracks = ((TimelineAsset)first.Director.playableAsset).GetOutputTracks().ToArray();
                Assert.That(tracks.Count(t => t is DialogueTrack), Is.EqualTo(1));
                Assert.That(tracks.Count(t => t is FadeTrack), Is.EqualTo(1));
                var track = tracks.OfType<DialogueTrack>().Single();
                Assert.That(first.Director.GetGenericBinding(track), Is.SameAs(first.View));
                Assert.That(a.GetComponentsInChildren<CutsceneTrigger>(true).Length, Is.EqualTo(1));
                yield return null;
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }
    }
}
