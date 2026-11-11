using ActionPlatformer.Units.Bosses;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ActionPlatformer.Editor
{
    // Header row (summary + add/replace + delete) over the node's own fields. Applies to single fields and to each list element.
    [CustomPropertyDrawer(typeof(BossNodeFieldAttribute))]
    public sealed class BossNodeFieldDrawer : PropertyDrawer
    {
        private const string EmptyText = "(비어 있음 — 노드 추가)";
        private const float ReplaceWidth = 44f, DeleteWidth = 44f, Gap = 2f;

        public static string SafeSummary(BossSelectorNode node)
        {
            if (node == null) return "";
            try { return node.Summary ?? ""; }
            catch (Exception e) { return "(요약 오류: " + e.Message + ")"; }
        }

        // A node whose summary is empty still shows what it is: its type name.
        public static string NodeHeader(BossSelectorNode node, string summary) =>
            !string.IsNullOrEmpty(summary) ? summary : node != null ? node.GetType().Name : "";

        // A null slot in an asset whose saved data names a class that no longer exists is not "empty": the node was lost.
        // Unity reports such a slot as null with no type name, so the missing class names come from the asset as a whole.
        public static string EmptyHeader(SerializedProperty property)
        {
            var asset = property.serializedObject.targetObject;
            if (asset != null && SerializationUtility.HasManagedReferencesWithMissingTypes(asset))
            {
                var names = new List<string>();
                foreach (var missing in SerializationUtility.GetManagedReferencesWithMissingTypes(asset))
                    if (!names.Contains(missing.className)) names.Add(missing.className);
                return "(missing type " + string.Join(", ", names) + " — 교체 또는 삭제)";
            }
            return EmptyText;
        }

        // The list "+" button can leave a new element pointing at the same node object as its neighbour. The first
        // occurrence keeps the node; every later copy becomes null so the author has to pick a node for it.
        public static bool ClearDuplicateReferences(SerializedObject so)
        {
            var seen = new HashSet<long>();
            var duplicates = new List<string>();
            var it = so.GetIterator();
            bool enter = true;
            while (it.Next(enter))
            {
                enter = true;
                if (it.propertyType != SerializedPropertyType.ManagedReference) continue;
                long id = it.managedReferenceId;
                if (id <= 0) continue; // -2 is null, -1 is unknown; real ids are positive
                // The children of a duplicate go away with it, so they are not visited.
                if (!seen.Add(id)) { duplicates.Add(it.propertyPath); enter = false; }
            }
            if (duplicates.Count == 0) return false;
            foreach (var path in duplicates) so.FindProperty(path).managedReferenceValue = null;
            so.ApplyModifiedProperties();
            return true;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!IsNode(property) || !HasValue(property) || !property.isExpanded) return height;
            foreach (var child in Children(property))
                height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!IsNode(property))
            {
                EditorGUI.HelpBox(position, "BossNodeField는 [SerializeReference] BossSelectorNode 필드에만 쓸 수 있습니다.", MessageType.Error);
                return;
            }
            EditorGUI.BeginProperty(position, label, property);
            bool hasValue = HasValue(property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var delete = new Rect(line.xMax - DeleteWidth, line.y, DeleteWidth, line.height);
            var replace = new Rect(delete.x - Gap - ReplaceWidth, line.y, ReplaceWidth, line.height);
            var head = new Rect(line.x, line.y, replace.x - Gap - line.x, line.height);

            string summary = SafeSummary(property.managedReferenceValue as BossSelectorNode);
            string text = hasValue ? NodeHeader(property.managedReferenceValue as BossSelectorNode, summary) : EmptyHeader(property);
            if (hasValue) property.isExpanded = EditorGUI.Foldout(head, property.isExpanded, text, true);
            else EditorGUI.LabelField(head, text, EditorStyles.miniLabel);

            if (GUI.Button(replace, hasValue ? "교체" : "추가", EditorStyles.miniButton)) ShowMenu(property);
            using (new EditorGUI.DisabledScope(!hasValue))
                if (GUI.Button(delete, "삭제", EditorStyles.miniButton))
                {
                    Assign(property, null);
                    // The tree changed under the running layout pass; leave it. Tests call Assign directly, with no GUI event.
                    if (Event.current != null) GUIUtility.ExitGUI();
                }

            if (hasValue && property.isExpanded)
            {
                float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.indentLevel++;
                foreach (var child in Children(property))
                {
                    float h = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, true);
                    y += h + EditorGUIUtility.standardVerticalSpacing;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        private static bool IsNode(SerializedProperty p) => p.propertyType == SerializedPropertyType.ManagedReference;
        private static bool HasValue(SerializedProperty p) => p.managedReferenceValue != null;

        // Direct children only; drawing the node property itself again would recurse into this drawer.
        private static IEnumerable<SerializedProperty> Children(SerializedProperty property)
        {
            var it = property.Copy();
            var end = property.GetEndProperty();
            bool enter = true;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                enter = false;
                yield return it;
            }
        }

        private static void ShowMenu(SerializedProperty property)
        {
            var targets = property.serializedObject.targetObjects;
            string path = property.propertyPath;
            var current = property.managedReferenceValue?.GetType();
            var menu = new GenericMenu();
            foreach (var type in BossNodeCatalog.NodeTypes)
            {
                var chosen = type;
                menu.AddItem(new GUIContent(BossNodeCatalog.MenuPath(type)), type == current, () =>
                {
                    var so = new SerializedObject(targets);
                    var p = so.FindProperty(path);
                    if (p != null) Assign(p, Activator.CreateInstance(chosen));
                });
            }
            menu.ShowAsContext();
        }

        private static void Assign(SerializedProperty property, object value)
        {
            var so = property.serializedObject;
            so.Update();
            property.managedReferenceValue = value;
            property.isExpanded = value != null;
            so.ApplyModifiedProperties(); // records Undo
        }
    }
}
