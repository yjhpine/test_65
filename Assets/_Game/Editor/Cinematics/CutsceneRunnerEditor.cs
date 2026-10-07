using ActionPlatformer.Cinematics;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;

namespace ActionPlatformer.Editor
{
    [CustomEditor(typeof(CutsceneRunner))]
    public sealed class CutsceneRunnerEditor : UnityEditor.Editor
    {
        private DialogueSpeaker newSpeaker;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(Application.isPlaying)) DrawPropertiesExcluding(serializedObject, "m_Script");
            if (serializedObject.ApplyModifiedProperties())
            {
                var runner = (CutsceneRunner)target;
                Undo.RecordObject(runner.Director, "Bind Cutscene");
                runner.BindOutputs();
                EditorUtility.SetDirty(runner.Director);
                if (PrefabUtility.IsPartOfPrefabInstance(runner.Director))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(runner.Director);
            }
            var cutscene = (CutsceneRunner)target;
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                newSpeaker = (DialogueSpeaker)EditorGUILayout.ObjectField("추가할 화자", newSpeaker, typeof(DialogueSpeaker), true);
                using (new EditorGUI.DisabledScope(newSpeaker == null || cutscene.Director == null ||
                    !(cutscene.Director.playableAsset is UnityEngine.Timeline.TimelineAsset) || newSpeaker.gameObject.scene != cutscene.gameObject.scene))
                    if (GUILayout.Button("말풍선 트랙 추가")) SpeechBubbleAuthoring.AddSpeakerTrack(cutscene, newSpeaker);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("실행 상태", cutscene.State.ToString());
            if (GUILayout.Button("Timeline 열기")) TimelineEditor.GetOrCreateWindow().SetTimeline(cutscene.Director);
            if (GUILayout.Button("연결 검사"))
            {
                cutscene.BindOutputs();
                if (cutscene.TryValidate(out string error)) Debug.Log("연출 연결 정상", cutscene);
                else Debug.LogError(error, cutscene);
            }
            if (!cutscene.TryValidate(out string problem)) EditorGUILayout.HelpBox(problem, MessageType.Warning);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("재생")) cutscene.TryPlay();
                if (GUILayout.Button("다음 대사")) cutscene.Confirm();
                if (GUILayout.Button("스킵")) cutscene.Skip();
                if (GUILayout.Button("중단")) cutscene.Cancel();
            }
        }
    }
    [CustomEditor(typeof(CutsceneTrigger))]
    public sealed class CutsceneTriggerEditor : UnityEditor.Editor
    {

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Entry Area의 BoxCollider2D 크기와 위치를 편집하세요. 피격/취소 후에는 나갔다가 다시 진입하면 재생합니다.", MessageType.Info);
        }
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawArea(CutsceneTrigger trigger, GizmoType type)
        {
            var box = trigger.GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.color = new Color(.15f, .85f, .75f, .5f);
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
}
