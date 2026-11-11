using ActionPlatformer.Units.Bosses;
using UnityEditor;

namespace ActionPlatformer.Editor
{
    internal static class BossValidationBox
    {
        public delegate bool Validator(object target, out string error);

        public static void Draw(UnityEngine.Object[] targets, Validator validate)
        {
            foreach (var item in targets)
                if (item != null && !validate(item, out string error))
                    EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }

    [CustomEditor(typeof(BossPatternSelector)), CanEditMultipleObjects]
    public sealed class BossPatternSelectorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            BossNodeFieldDrawer.ClearDuplicateReferences(serializedObject);
            DrawDefaultInspector();
            BossValidationBox.Draw(targets, (object t, out string e) => ((BossPatternSelector)t).TryValidate(out e));
        }
    }

    [CustomEditor(typeof(BossFsmDefinition)), CanEditMultipleObjects]
    public sealed class BossFsmDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            BossValidationBox.Draw(targets, (object t, out string e) => ((BossFsmDefinition)t).TryValidate(out e));
        }
    }

    [CustomEditor(typeof(BossPatternDefinition), true), CanEditMultipleObjects]
    public sealed class BossPatternDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            BossValidationBox.Draw(targets, (object t, out string e) => ((BossPatternDefinition)t).TryValidate(out e));
        }
    }
}
