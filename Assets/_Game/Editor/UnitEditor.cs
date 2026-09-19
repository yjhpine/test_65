using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using ActionPlatformer.Units.Fsm;
using UnityEditor;
using UnityEngine;

namespace ActionPlatformer.Editor
{
    [CustomEditor(typeof(Unit), true), CanEditMultipleObjects]
    public sealed class UnitEditor : UnityEditor.Editor
    {
        private int testDamage = 5;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying || targets.Length != 1) return;
            var unit = (Unit)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("FSM State", unit.Fsm?.CurrentState?.GetType().Name ?? "None");
            if (unit.TryGetComponent<UnitCombat2D>(out var combat))
                EditorGUILayout.LabelField("Target", combat.Target == null ? "None" : combat.Target.name);
            if (!unit.TryGetComponent<UnitHealth>(out var health)) return;
            EditorGUILayout.LabelField("Health", health.CurrentHealth + " / " + health.MaxHealth);
            using (new EditorGUI.DisabledScope(!health.IsAlive || !unit.isActiveAndEnabled))
            {
                testDamage = Mathf.Max(1, EditorGUILayout.IntField("Test Damage", testDamage));
                if (GUILayout.Button("Apply Test Damage")) health.ApplyDamage(testDamage);
            }
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }

    [CustomEditor(typeof(GroundUnitFsmDefinition)), CanEditMultipleObjects]
    public sealed class GroundUnitFsmDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            foreach (var item in targets)
                if (!((GroundUnitFsmDefinition)item).TryValidate(out string error))
                    EditorGUILayout.HelpBox(item.name + ": " + error, MessageType.Error);
        }
    }
}
