using ActionPlatformer.Editor;
using ActionPlatformer.Units.Bosses;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class BossNodeDrawerTests
    {
        private static BossPatternSelector TwoChildSelector()
        {
            var selector = ScriptableObject.CreateInstance<BossPatternSelector>();
            selector.SetRoot(new BossSelectorBranch(new BossDistanceCondition(0, 3, -1, 1), new BossSideCondition(BossSide.Front)));
            return selector;
        }

        [Test] public void ClearDuplicateReferencesNullsACopiedListElement()
        {
            var selector = TwoChildSelector();
            try
            {
                var so = new SerializedObject(selector);
                var children = so.FindProperty("root").FindPropertyRelative("children");
                children.InsertArrayElementAtIndex(0); // what a duplicating "+" does
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();
                Assert.AreEqual(children.GetArrayElementAtIndex(0).managedReferenceId, children.GetArrayElementAtIndex(1).managedReferenceId, "precondition: Insert shares the node");

                Assert.IsTrue(BossNodeFieldDrawer.ClearDuplicateReferences(so));
                so = new SerializedObject(selector);
                children = so.FindProperty("root").FindPropertyRelative("children");
                Assert.AreEqual(3, children.arraySize);
                Assert.IsNotNull(children.GetArrayElementAtIndex(0).managedReferenceValue);
                Assert.IsNull(children.GetArrayElementAtIndex(1).managedReferenceValue);
                Assert.IsInstanceOf<BossSideCondition>(children.GetArrayElementAtIndex(2).managedReferenceValue);
                Assert.IsFalse(BossNodeFieldDrawer.ClearDuplicateReferences(so), "A tree without copies is left alone.");
            }
            finally { Object.DestroyImmediate(selector); }
        }

        [Test] public void AppendedElementIsAlreadyEmpty()
        {
            var selector = TwoChildSelector();
            try
            {
                var so = new SerializedObject(selector);
                var children = so.FindProperty("root").FindPropertyRelative("children");
                children.arraySize++;
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();
                Assert.IsNull(children.GetArrayElementAtIndex(2).managedReferenceValue);
                Assert.IsFalse(BossNodeFieldDrawer.ClearDuplicateReferences(so));
            }
            finally { Object.DestroyImmediate(selector); }
        }

        [Test] public void MissingNodeClassIsNotShownAsEmpty()
        {
            const string path = "Assets/_Game/Tests/TmpBossMissingType.asset";
            string guid = AssetDatabase.AssetPathToGUID("Assets/_Game/Runtime/Units/Bosses/Selection/BossPatternSelector.cs");
            string yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: " + guid
                + ", type: 3}\n  m_Name: TmpBossMissingType\n  m_EditorClassIdentifier: \n  root:\n    rid: 1000\n  references:\n    version: 2\n    RefIds:\n    - rid: 1000\n      type: {class: GoneNode, ns: ActionPlatformer.Units.Bosses, asm: ActionPlatformer.Runtime}\n      data:\n        x: 1\n";
            try
            {
                System.IO.File.WriteAllText(path, yaml);
                AssetDatabase.ImportAsset(path);
                var asset = AssetDatabase.LoadAssetAtPath<BossPatternSelector>(path);
                Assert.IsNotNull(asset);
                string header = BossNodeFieldDrawer.EmptyHeader(new SerializedObject(asset).FindProperty("root"));
                StringAssert.Contains("missing type", header);
                StringAssert.Contains("GoneNode", header);
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [Test] public void GenuinelyEmptySlotKeepsTheAddPrompt()
        {
            var selector = ScriptableObject.CreateInstance<BossPatternSelector>();
            try
            {
                string header = BossNodeFieldDrawer.EmptyHeader(new SerializedObject(selector).FindProperty("root"));
                StringAssert.Contains("비어 있음", header);
            }
            finally { Object.DestroyImmediate(selector); }
        }
    }
}
