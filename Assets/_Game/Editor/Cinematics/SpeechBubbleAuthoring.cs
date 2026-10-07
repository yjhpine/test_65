using System;
using System.Linq;
using ActionPlatformer.Cinematics;
using ActionPlatformer.Units;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace ActionPlatformer.Editor
{
    public static class SpeechBubbleAuthoring
    {
        public static SpeechBubbleTrack AddSpeakerTrack(CutsceneRunner runner, DialogueSpeaker speaker)
        {
            if (Application.isPlaying || runner == null || speaker == null || speaker.gameObject.scene != runner.gameObject.scene)
                throw new InvalidOperationException("같은 씬의 연출과 화자를 지정하세요.");
            var timeline = runner.Director.playableAsset as TimelineAsset;
            if (timeline == null) throw new InvalidOperationException("Timeline이 필요합니다.");
            Undo.RegisterCompleteObjectUndo(timeline, "Add Speaker Track");
            Undo.RecordObject(runner.Director, "Bind Speaker");
            var track = timeline.CreateTrack<SpeechBubbleTrack>(null, speaker.DisplayName + " · 말풍선");
            Undo.RegisterCreatedObjectUndo(track, "Add Speaker Track");
            double start = timeline.GetOutputTracks().Where(CutsceneRunner.IsDialogueTrack)
                .SelectMany(t => t.GetClips()).Select(c => c.end).DefaultIfEmpty(0).Max();
            var clip = track.CreateClip<DialogueClip>();
            Undo.RegisterCreatedObjectUndo(clip.asset, "Add Dialogue");
            clip.start = start; clip.duration = .4; clip.displayName = "새 대사";
            runner.Director.SetGenericBinding(track, speaker);
            EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(runner.Director);
            if (PrefabUtility.IsPartOfPrefabInstance(runner.Director))
                PrefabUtility.RecordPrefabInstancePropertyModifications(runner.Director);
            EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
            return track;
        }

        // Explicit one-time installation. Never called on load or when creating a generic template.
        public static void InstallStageConversation()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Game/Scenes/CoreLoopStage.unity");
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open CoreLoopStage first.");
            var runner = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CutsceneRunner>(true)).Single();
            const string folder = "Assets/_Game/Cinematics";
            const string timelinePath = folder + "/IntroConversation.playable";
            if (AssetDatabase.LoadMainAssetAtPath(timelinePath) != null)
                throw new InvalidOperationException("Conversation example already installed.");

            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            var settings = new SerializedObject(definition);
            settings.FindProperty("unitId").stringValue = "npc.guide";
            settings.FindProperty("displayName").stringValue = "안내자";
            settings.FindProperty("kind").enumValueIndex = (int)UnitKind.Npc;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, folder + "/GuideDefinition.asset");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/GuideIdle.controller");
            controller.layers[0].stateMachine.AddState("Idle").motion =
                AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Game/Animations/Player/Idle.anim");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab")
                .GetComponentInChildren<SpriteRenderer>();
            var npcRoot = new GameObject("Guide");
            try
            {
                var unit = npcRoot.AddComponent<Unit>(); Set(unit, "definition", definition);
                var visual = new GameObject("Visual", typeof(SpriteRenderer), typeof(Animator));
                visual.transform.SetParent(npcRoot.transform, false);
                visual.transform.localPosition = source.transform.localPosition;
                visual.transform.localScale = source.transform.localScale;
                var renderer = visual.GetComponent<SpriteRenderer>();
                renderer.sprite = source.sprite; renderer.color = new Color(.55f, .9f, 1f, 1);
                renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder;
                renderer.flipX = true;
                visual.GetComponent<Animator>().runtimeAnimatorController = controller;
                AddSpeaker(npcRoot, "안내자");
                var prefab = PrefabUtility.SaveAsPrefabAsset(npcRoot, folder + "/Guide.prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add Guide");
                instance.transform.position = new Vector3(9, .82f, 0);
            }
            finally { Object.DestroyImmediate(npcRoot); }
            var guide = scene.GetRootGameObjects().Single(g => g.name == "Guide").GetComponent<DialogueSpeaker>();
            var playerSpeaker = AddSpeaker(runner.Player.gameObject, "플레이어");
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(runner.Director.playableAsset), timelinePath);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            foreach (var track in timeline.GetOutputTracks().OfType<DialogueTrack>().ToArray()) timeline.DeleteTrack(track);
            Undo.RecordObject(runner.Director, "Use Conversation Timeline");
            runner.Director.playableAsset = timeline;
            var guideTrack = timeline.CreateTrack<SpeechBubbleTrack>(null, "안내자 · 말풍선");
            var playerTrack = timeline.CreateTrack<SpeechBubbleTrack>(null, "플레이어 · 말풍선");
            AddLine(guideTrack, 1.85, "앞쪽 발판을 따라 이동해 보세요.");
            AddLine(playerTrack, 2.25, "출구는 어떻게 열죠?");
            AddLine(guideTrack, 2.65, "모든 적을 쓰러뜨리면 오른쪽 출구가 열립니다.");
            runner.Director.SetGenericBinding(guideTrack, guide);
            runner.Director.SetGenericBinding(playerTrack, playerSpeaker);
            var animation = timeline.GetOutputTracks().OfType<AnimationTrack>().Single();
            runner.Director.SetGenericBinding(animation, runner.transform.Find("Shot Rig").GetComponent<Animator>());
            animation.GetClips().Single().duration = 1.85;

            var talkObject = new GameObject("Conversation Camera", typeof(CinemachineCamera));
            talkObject.transform.SetParent(runner.transform, false);
            Undo.RegisterCreatedObjectUndo(talkObject, "Add Conversation Camera");
            talkObject.transform.position = new Vector3(7.5f, 2.8f, -10);
            var talkCamera = talkObject.GetComponent<CinemachineCamera>();
            talkCamera.Priority = -100;
            talkCamera.Lens.OrthographicSize = 4.5f;
            talkCamera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            var cameras = timeline.GetOutputTracks().OfType<CinemachineTrack>().Single();
            cameras.GetClips().OrderByDescending(c => c.duration).First().duration = 1.85;
            var talk = cameras.CreateClip<CinemachineShot>();
            talk.start = 1.5; talk.duration = 1.75; talk.displayName = "플레이어와 안내자";
            var shot = (CinemachineShot)talk.asset;
            shot.VirtualCamera.exposedName = new PropertyName(GUID.Generate().ToString());
            runner.Director.SetReferenceValue(shot.VirtualCamera.exposedName, talkCamera);
            runner.BindOutputs();
            PrefabUtility.RecordPrefabInstancePropertyModifications(runner.Director);
            EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(runner.Director);
            AssetDatabase.SaveAssets();
            if (!runner.TryValidate(out string error)) throw new InvalidOperationException(error);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        private static DialogueSpeaker AddSpeaker(GameObject actor, string name)
        {
            var speaker = Undo.AddComponent<DialogueSpeaker>(actor);
            var anchor = new GameObject("SpeechAnchor");
            Undo.RegisterCreatedObjectUndo(anchor, "Add Speech Anchor");
            anchor.transform.SetParent(actor.transform, false);
            anchor.transform.localPosition = new Vector3(0, 1.25f, 0);
            var serialized = new SerializedObject(speaker);
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("anchor").objectReferenceValue = anchor.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return speaker;
        }
        private static void AddLine(TrackAsset track, double time, string text)
        {
            var clip = track.CreateClip<DialogueClip>();
            clip.start = time; clip.duration = .4; clip.displayName = text;
            ((DialogueClip)clip.asset).Text = text;
        }
        private static void Set(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
