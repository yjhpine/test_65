using System;
using System.IO;
using System.Linq;
using ActionPlatformer.Cinematics;
using ActionPlatformer.Flow;
using ActionPlatformer.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ActionPlatformer.Editor
{
    public static class CutsceneAuthoring
    {
        public const string DefaultFolder = "Assets/_Game/Cinematics";
        [MenuItem("Game/Cinematics/Create Cutscene")]
        public static void CreateMenu()
        {
            if (Application.isPlaying) return;
            string path = EditorUtility.SaveFilePanelInProject("연출 템플릿 생성", "NewCutscene", "prefab", "새 프리팹 저장 위치", DefaultFolder);
            if (string.IsNullOrEmpty(path)) return;
            var prefab = CreateTemplate(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Create Cutscene");
            Selection.activeGameObject = instance;
            Debug.Log("연출을 만들었습니다. Inspector에서 씬의 Coordinator, Player, Stage를 연결하세요.", instance);
        }

        // Also used by integration tests and the initial stage setup. Never overwrite authored assets.
        public static GameObject CreateTemplate(string prefabPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(prefabPath) != null)
                throw new InvalidOperationException("An asset already exists at " + prefabPath);
            string directory = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            Directory.CreateDirectory(directory);
            EnsureSharedAssets();
            AssetDatabase.Refresh();
            string timelinePath = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(prefabPath, ".playable"));
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, timelinePath);
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var director = root.AddComponent<PlayableDirector>();
                director.playOnAwake = false; director.timeUpdateMode = DirectorUpdateMode.Manual;
                director.extrapolationMode = DirectorWrapMode.Hold; director.playableAsset = timeline;
                var runner = root.AddComponent<CutsceneRunner>();
                var ui = new GameObject("Dialogue UI", typeof(UIDocument), typeof(CutsceneView));
                ui.transform.SetParent(root.transform, false);
                var document = ui.GetComponent<UIDocument>();
                document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(DefaultFolder + "/CutscenePanelSettings.asset");
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Game/UI/Cinematics/Cutscene.uxml");
                document.sortingOrder = 20;
                Set(ui.GetComponent<CutsceneView>(), "document", document);
                Set(runner, "director", director); Set(runner, "view", ui.GetComponent<CutsceneView>());
                Set(runner, "cutsceneInput", AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultFolder + "/CutsceneControls.inputactions"));

                var shotRig = new GameObject("Shot Rig", typeof(Animator));
                shotRig.transform.SetParent(root.transform, false);
                var cameraObject = new GameObject("Shot Camera", typeof(CinemachineCamera));
                cameraObject.transform.SetParent(shotRig.transform, false);
                cameraObject.transform.localPosition = new Vector3(11.56f, 3.4f, -10);
                var camera = cameraObject.GetComponent<CinemachineCamera>();
                camera.Lens.OrthographicSize = 6.5f;
                camera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
                camera.Priority = -100;
                var cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "카메라");
                var entryClip = cameraTrack.CreateClip<CinemachineShot>();
                entryClip.displayName = "현재 플레이 구도"; entryClip.start = 0; entryClip.duration = .4;
                ((CinemachineShot)entryClip.asset).VirtualCamera.exposedName = new PropertyName("CutsceneEntryCamera");
                var shotClip = cameraTrack.CreateClip<CinemachineShot>();
                shotClip.start = 0; shotClip.duration = 2.5; shotClip.displayName = "전방 발판 소개";
                var shot = (CinemachineShot)shotClip.asset;
                shot.VirtualCamera.exposedName = new PropertyName(GUID.Generate().ToString());
                director.SetReferenceValue(shot.VirtualCamera.exposedName, camera);

                var animationTrack = timeline.CreateTrack<AnimationTrack>(null, "이동 · 줌");
                animationTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
                var animation = new AnimationClip { name = "CameraMoveZoom", frameRate = 30 };
                AssetDatabase.AddObjectToAsset(animation, timeline);
                SetCurve(animation, typeof(Transform), "m_LocalPosition.x", 11.56f, 20f);
                SetCurve(animation, typeof(Transform), "m_LocalPosition.y", 3.4f, 3.4f);
                SetCurve(animation, typeof(Transform), "m_LocalPosition.z", -10, -10);
                SetCurve(animation, typeof(CinemachineCamera), "Lens.OrthographicSize", 6.5f, 5.5f);
                var animationClip = animationTrack.CreateClip(animation);
                animationClip.start = 0; animationClip.duration = 2.5;
                director.SetGenericBinding(animationTrack, shotRig.GetComponent<Animator>());

                var dialogue = timeline.CreateTrack<DialogueTrack>(null, "대화");
                AddDialogue(dialogue, 1.5, "앞쪽 발판을 따라 이동해 보세요.");
                AddDialogue(dialogue, 1.9, "모든 적을 쓰러뜨리면 오른쪽 출구가 열립니다.");
                var fade = timeline.CreateTrack<FadeTrack>(null, "페이드");
                var fadeClip = fade.CreateClip<FadeClip>();
                fadeClip.start = 0; fadeClip.duration = .3; fadeClip.displayName = "짧은 암전 해제";
                var triggerObject = new GameObject("Entry Area", typeof(BoxCollider2D), typeof(CutsceneTrigger));
                triggerObject.transform.SetParent(root.transform, false);
                triggerObject.transform.localPosition = new Vector3(6, 1.5f, 0);
                var area = triggerObject.GetComponent<BoxCollider2D>(); area.isTrigger = true; area.size = new Vector2(1.5f, 3);
                Set(triggerObject.GetComponent<CutsceneTrigger>(), "runner", runner);
                Set(triggerObject.GetComponent<CutsceneTrigger>(), "area", area);
                runner.BindOutputs();
                EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(director);
                var result = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                return result;
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void SetCurve(AnimationClip clip, Type type, string property, float from, float to)
            => AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Shot Camera", type, property), AnimationCurve.EaseInOut(0, from, 1.5f, to));
        private static void AddDialogue(DialogueTrack track, double start, string text)
        {
            var clip = track.CreateClip<DialogueClip>();
            clip.start = start; clip.duration = .4; clip.displayName = text;
            ((DialogueClip)clip.asset).Text = text;
        }
        private static void EnsureSharedAssets()
        {
            Directory.CreateDirectory(DefaultFolder);
            AssetDatabase.Refresh();
            string panelPath = DefaultFolder + "/CutscenePanelSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath) == null)
            {
                var original = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/StagePanelSettings.asset");
                var panel = Object.Instantiate(original);
                panel.name = "CutscenePanelSettings"; panel.sortingOrder = 20;
                AssetDatabase.CreateAsset(panel, panelPath);
            }
            string inputPath = DefaultFolder + "/CutsceneControls.inputactions";
            if (!File.Exists(inputPath))
            {
                var actions = ScriptableObject.CreateInstance<InputActionAsset>();
                var map = actions.AddActionMap("Cutscene");
                var confirm = map.AddAction("Confirm", InputActionType.Button);
                confirm.AddBinding("<Keyboard>/enter"); confirm.AddBinding("<Keyboard>/space");
                confirm.AddBinding("<Mouse>/leftButton"); confirm.AddBinding("<Gamepad>/buttonSouth");
                var skip = map.AddAction("Skip", InputActionType.Button);
                skip.AddBinding("<Keyboard>/escape"); skip.AddBinding("<Gamepad>/start");
                File.WriteAllText(inputPath, actions.ToJson());
                Object.DestroyImmediate(actions);
                AssetDatabase.ImportAsset(inputPath);
            }
        }
        public static void InstallStageExample()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Game/Scenes/CoreLoopStage.unity");
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open CoreLoopStage first.");
            if (InScene<CutsceneCoordinator>(scene) != null) throw new InvalidOperationException("Stage already has cinematics.");
            var flow = InScene<StageFlowController>(scene);
            var player = InScene<PlayerUnit>(scene);
            var follow = InScene<StageCameraFollow>(scene);
            if (flow == null || player == null || follow == null) throw new InvalidOperationException("Stage dependencies missing.");
            var output = follow.GetComponentInChildren<Camera>();
            var brain = output.GetComponent<CinemachineBrain>();
            if (brain == null) brain = Undo.AddComponent<CinemachineBrain>(output.gameObject);
            brain.enabled = false;
            string path = DefaultFolder + "/IntroCutscene.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? CreateTemplate(path);
            var context = new GameObject("Cinematics", typeof(CutsceneCoordinator), typeof(CutsceneCameraRig));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(context, scene);
            Undo.RegisterCreatedObjectUndo(context, "Install Cinematics");
            var rig = context.GetComponent<CutsceneCameraRig>();
            var coordinator = context.GetComponent<CutsceneCoordinator>();
            var entry = new GameObject("Entry Camera", typeof(CinemachineCamera));
            entry.transform.SetParent(context.transform, false);
            var returning = new GameObject("Return Camera", typeof(CinemachineCamera));
            returning.transform.SetParent(context.transform, false);
            entry.GetComponent<CinemachineCamera>().Priority = -200;
            returning.GetComponent<CinemachineCamera>().Priority = -200;
            Set(rig, "output", output); Set(rig, "brain", brain); Set(rig, "follow", follow);
            Set(rig, "entryCamera", entry.GetComponent<CinemachineCamera>());
            Set(rig, "returnCamera", returning.GetComponent<CinemachineCamera>());
            Set(coordinator, "cameraRig", rig);
            Set(flow, "cinematics", coordinator);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Install Intro Cutscene");
            var runner = instance.GetComponent<CutsceneRunner>();
            Set(runner, "coordinator", coordinator); Set(runner, "player", player); Set(runner, "stage", flow);
            runner.BindOutputs();
            PrefabUtility.RecordPrefabInstancePropertyModifications(runner.Director);
            if (!runner.TryValidate(out string error)) throw new InvalidOperationException(error);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = instance;
        }
        private static T InScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).FirstOrDefault();
        private static void Set(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
