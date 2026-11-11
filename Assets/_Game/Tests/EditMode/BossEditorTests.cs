using ActionPlatformer.Editor;
using ActionPlatformer.Units.Bosses;
using NUnit.Framework;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class BossEditorTests
    {
        private static readonly Type[] Expected =
        {
            typeof(BossSelectorBranch), typeof(BossSequenceBranch), typeof(BossWeightedBranch), typeof(BossInverter),
            typeof(BossDistanceCondition), typeof(BossSideCondition), typeof(BossPlayerStateCondition),
            typeof(BossPhaseCondition), typeof(BossChanceCondition), typeof(BossPatternLeaf),
        };

        [Test] public void CatalogListsEveryConcreteNodeWithMenuPath()
        {
            foreach (var t in Expected)
            {
                CollectionAssert.Contains(BossNodeCatalog.NodeTypes.ToList(), t, t.Name);
                Assert.IsNotNull(Attribute.GetCustomAttribute(t, typeof(BossNodeMenuAttribute)), t.Name);
                Assert.IsFalse(BossNodeCatalog.MenuPath(t).StartsWith("기타/"), t.Name);
            }
            Assert.AreEqual("기타/Plain", BossNodeCatalog.MenuPath(typeof(Plain)));
        }

        [Test] public void CatalogExcludesNodesDeclaredInTestAssemblies()
        {
            var types = BossNodeCatalog.NodeTypes.ToList();
            CollectionAssert.DoesNotContain(types, typeof(Plain));
            CollectionAssert.DoesNotContain(types, typeof(Throwing));
            Assert.IsFalse(types.Any(t => t.Assembly == typeof(BossEditorTests).Assembly));
        }

        [Test] public void CatalogExcludesAbstractBases()
        {
            var types = BossNodeCatalog.NodeTypes.ToList();
            CollectionAssert.DoesNotContain(types, typeof(BossSelectorNode));
            CollectionAssert.DoesNotContain(types, typeof(BossCondition));
            CollectionAssert.DoesNotContain(types, typeof(BossBranch));
        }

        [Test] public void SettingManagedReferenceThroughSerializedObjectPersists()
        {
            var selector = ScriptableObject.CreateInstance<BossPatternSelector>();
            try
            {
                var so = new SerializedObject(selector);
                so.FindProperty("root").managedReferenceValue = Activator.CreateInstance(typeof(BossSelectorBranch));
                so.ApplyModifiedPropertiesWithoutUndo();

                so = new SerializedObject(selector);
                var children = so.FindProperty("root").FindPropertyRelative("children");
                children.arraySize = 1;
                children.GetArrayElementAtIndex(0).managedReferenceValue = new BossDistanceCondition(1, 4, -2, 2);
                so.ApplyModifiedPropertiesWithoutUndo();

                var root = new SerializedObject(selector).targetObject as BossPatternSelector;
                Assert.IsInstanceOf<BossSelectorBranch>(root.Root);
                var item = ((BossSelectorBranch)root.Root).Items.Single();
                Assert.IsInstanceOf<BossDistanceCondition>(item);
                StringAssert.Contains("4", item.Summary);
            }
            finally { UnityEngine.Object.DestroyImmediate(selector); }
        }

        [Test] public void NodeHeaderFallsBackToTypeName()
        {
            Assert.AreEqual("shown", BossNodeFieldDrawer.NodeHeader(new Plain(), "shown"));
            Assert.AreEqual(nameof(Plain), BossNodeFieldDrawer.NodeHeader(new Plain(), ""));
            Assert.AreEqual("", BossNodeFieldDrawer.NodeHeader(null, ""));
        }

        [Test] public void SafeSummaryNeverThrows()
        {
            Assert.AreEqual("", BossNodeFieldDrawer.SafeSummary(null));
            StringAssert.Contains("Boom", BossNodeFieldDrawer.SafeSummary(new Throwing()));
        }

        [Serializable] private sealed class Plain : BossCondition
        {
            public override string Summary => "plain";
            protected override bool Check(BossSelection s) => true;
        }

        [Serializable] private sealed class Throwing : BossCondition
        {
            public override string Summary => throw new InvalidOperationException("Boom");
            protected override bool Check(BossSelection s) => true;
        }
    }
}
