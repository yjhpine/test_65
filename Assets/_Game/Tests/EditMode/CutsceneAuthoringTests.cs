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
            var runner = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CutsceneTrigger>(true)).Single()
                .GetComponentInParent<CutsceneRunner>();
            var follow = runner.Coordinator.CameraRig.Output.transform.parent.GetComponent<ActionPlatformer.Flow.StageCameraFollow>();
            var window = UnityEditor.Timeline.TimelineEditor.GetOrCreateWindow();
            bool followEnabled = follow.enabled, brainEnabled = runner.Coordinator.CameraRig.Brain.enabled;
            float cameraSize = runner.Coordinator.CameraRig.Output.orthographicSize;
            var timeline = (TimelineAsset)runner.Director.playableAsset;
            var dialogueClips = timeline.GetOutputTracks().OfType<SpeechBubbleTrack>()
                .Where(t => !t.muted).SelectMany(t => t.GetClips().Select(c => new {
                    Clip = c, Speaker = runner.Director.GetGenericBinding(t) as DialogueSpeaker
                })).OrderBy(c => c.Clip.start).ToArray();
            Assert.That(dialogueClips.Length, Is.GreaterThanOrEqualTo(2));
            var first = dialogueClips[0]; var second = dialogueClips[1];
            Assert.That(first.Speaker, Is.Not.Null); Assert.That(second.Speaker, Is.Not.Null);
            Assert.That(second.Speaker, Is.Not.SameAs(first.Speaker));
            string firstText = ((DialogueClip)first.Clip.asset).Text;
            string secondText = ((DialogueClip)second.Clip.asset).Text;
            // Sample inside the authored clips, not obsolete absolute timestamps or blend boundaries.
            double firstTime = first.Clip.start + first.Clip.duration * .5;
            double secondTime = second.Clip.start + second.Clip.duration * .5;
            Assert.That(first.Clip.start, Is.GreaterThan(0), "The intro must have a pre-dialogue interval.");
            IEnumerator Scrub(double time, Func<bool> ready)
            {
                window.playbackControls.SetCurrentTime(time);
                double deadline = EditorApplication.timeSinceStartup + 2;
                do { yield return null; }
                while (!ready() && EditorApplication.timeSinceStartup < deadline);
                Assert.That(ready(), Is.True, "Timeline preview did not present the authored clip at " + time);
            }
            try
            {
                window.SetTimeline(runner.Director);
                window.playbackControls.Play(); window.playbackControls.Pause();
                yield return Scrub(firstTime, () => runner.Coordinator.CameraRig.IsAcquired &&
                    runner.View.IsBubbleVisible && runner.View.VisibleText == firstText);
                Assert.That(follow.enabled, Is.False);
                Assert.That(runner.CurrentSpeaker, Is.SameAs(first.Speaker));
                Assert.That(runner.State, Is.EqualTo(CutsceneState.Idle));
                yield return Scrub(secondTime, () => runner.View.IsBubbleVisible && runner.View.VisibleText == secondText);
                Assert.That(runner.CurrentSpeaker, Is.SameAs(second.Speaker));
                yield return Scrub(first.Clip.start * .5, () => !runner.View.IsDialogueVisible);
                yield return Scrub(firstTime, () => runner.View.IsBubbleVisible && runner.View.VisibleText == firstText);
                window.ClearTimeline();
                // No extra Editor update or manual End call may be needed to restore ownership.
                Assert.That(follow.enabled, Is.EqualTo(followEnabled));
                Assert.That(runner.Coordinator.CameraRig.Brain.enabled, Is.EqualTo(brainEnabled));
                Assert.That(runner.Coordinator.CameraRig.Output.orthographicSize, Is.EqualTo(cameraSize).Within(.001f));
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
