using ActionPlatformer.Units.Bosses;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class BossSelectionTests
    {
        private BossBlackboard board;
        private GameObject targetObject;
        private StubBossPattern a, b;
        private readonly List<Object> created = new List<Object>();

        [SetUp] public void SetUp()
        {
            board = new BossBlackboard(seed: 7);
            targetObject = new GameObject("Dummy target");
            board.Target = targetObject.AddComponent<UnitHealth>();
            board.BossFacing = 1;
            a = Pattern("A"); b = Pattern("B");
        }

        [TearDown] public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Object.DestroyImmediate(targetObject);
        }

        private StubBossPattern Pattern(string name)
        {
            var p = ScriptableObject.CreateInstance<StubBossPattern>();
            p.name = name; created.Add(p); return p;
        }

        private BossPatternSelector Tree(BossSelectorNode root)
        {
            var s = ScriptableObject.CreateInstance<BossPatternSelector>();
            s.SetRoot(root); created.Add(s); return s;
        }

        private BossPatternDefinition Select(BossPatternSelector tree, out BossSelection selection)
        {
            selection = new BossSelection(board);
            return tree.Select(selection);
        }

        private static BossSelectorNode Distance(float maxH) => new BossDistanceCondition(0, maxH, -1.5f, 1.5f);
        private static BossSelectorNode Seq(params BossSelectorNode[] n) => new BossSequenceBranch(n);

        [Test] public void SelectorPicksFirstSucceedingBranch()
        {
            var tree = Tree(new BossSelectorBranch(Seq(Distance(3), new BossPatternLeaf(a)), Seq(Distance(10), new BossPatternLeaf(b))));
            board.PlayerPosition = new Vector2(5, 0);
            Assert.That(Select(tree, out _), Is.SameAs(b));
            board.PlayerPosition = new Vector2(2, 0);
            Assert.That(Select(tree, out _), Is.SameAs(a));
        }

        [Test] public void SequenceFailsWhenAnyConditionFailsAndRestoresSelection()
        {
            var failing = new BossPhaseCondition(5, 9);
            var tree = Tree(new BossSelectorBranch(Seq(new BossPatternLeaf(a), failing), new BossPatternLeaf(b)));
            Assert.That(Select(tree, out var s), Is.SameAs(b));
            Assert.That(s.Pattern, Is.SameAs(b));
            var onlyFailing = Tree(Seq(new BossPatternLeaf(a), failing));
            Assert.That(Select(onlyFailing, out var s2), Is.Null);
            Assert.That(s2.Pattern, Is.Null);
        }

        [TestCase(2f, 0f, true)]
        [TestCase(-2f, 0f, true)]
        [TestCase(4f, 0f, false)]
        [TestCase(2f, 2f, false)]
        public void DistanceConditionUsesHorizontalAndVerticalRanges(float dx, float dy, bool expected)
        {
            board.PlayerPosition = new Vector2(dx, dy);
            Assert.That(Distance(3).Evaluate(new BossSelection(board), 0), Is.EqualTo(expected));
        }

        private sealed class StubReposition : MonoBehaviour, IUnitRepositionState { public uint RepositionVersion => 0; }

        [Test] public void RecentlyGlitchedIsUnknownWithoutRepositionContractEvenInverted()
        {
            board.SinceGlitch = 0.5f;
            var glitched = new BossPlayerStateCondition(BossPlayerState.RecentlyGlitched);
            Assert.That(glitched.Evaluate(new BossSelection(board), 0), Is.False);
            Assert.That(new BossInverter(glitched).Evaluate(new BossSelection(board), 0), Is.False);
            board.RepositionState = targetObject.AddComponent<StubReposition>();
            Assert.That(glitched.Evaluate(new BossSelection(board), 0), Is.True);
            Assert.That(new BossInverter(glitched).Evaluate(new BossSelection(board), 0), Is.False);
            board.SinceGlitch = float.PositiveInfinity;
            Assert.That(new BossInverter(glitched).Evaluate(new BossSelection(board), 0), Is.True);
        }

        [Test] public void PlayerStateConditionFailsWhenUnknownEvenInverted()
        {
            board.PlayerGrounded = null;
            var grounded = new BossPlayerStateCondition(BossPlayerState.Grounded);
            Assert.That(grounded.Evaluate(new BossSelection(board), 0), Is.False);
            Assert.That(new BossInverter(grounded).Evaluate(new BossSelection(board), 0), Is.False);
            board.PlayerGrounded = false;
            Assert.That(grounded.Evaluate(new BossSelection(board), 0), Is.False);
            Assert.That(new BossInverter(grounded).Evaluate(new BossSelection(board), 0), Is.True);
        }

        private int[] Roll(BossBlackboard bb, BossPatternSelector tree)
        {
            var counts = new int[2];
            for (int i = 0; i < 100; i++)
            {
                var p = tree.Select(new BossSelection(bb));
                counts[p == a ? 0 : 1]++;
            }
            return counts;
        }

        [Test] public void WeightedRandomIsDeterministicForSeedAndSkipsUnavailable()
        {
            var tree = Tree(new BossWeightedBranch(new BossWeightedChild(1, new BossPatternLeaf(a)), new BossWeightedChild(1, new BossPatternLeaf(b))));
            var first = Roll(board, tree);
            var second = Roll(new BossBlackboard(seed: 7) { Target = board.Target }, tree);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first[0], Is.GreaterThan(0).And.LessThan(100));

            b.SetCooldown(5f);
            var cooled = new BossBlackboard(seed: 7) { Target = board.Target };
            cooled.RecordStart(b);
            Assert.That(Roll(cooled, tree)[0], Is.EqualTo(100));
        }

        [Test] public void PatternLeafSkipsCoolingDownAndRecentlyUsed()
        {
            a.SetCooldown(2f);
            var tree = Tree(new BossPatternLeaf(a));
            board.RecordStart(a);
            board.Advance(1);
            Assert.That(Select(tree, out _), Is.Null);
            board.Advance(1.01f);
            Assert.That(Select(tree, out _), Is.SameAs(a));

            a.SetCooldown(0f);
            board.ResetMemory(); board.RecordStart(a);
            var avoid = Tree(new BossPatternLeaf(a, 1));
            Assert.That(Select(avoid, out _), Is.Null);
            board.RecordStart(b);
            Assert.That(Select(avoid, out _), Is.SameAs(a));
        }

        [Test] public void NoTargetSelectsNothing()
        {
            var tree = Tree(Seq(Distance(10), new BossPatternLeaf(a)));
            Assert.That(Select(tree, out _), Is.SameAs(a));
            board.Target = null;
            Assert.That(Select(tree, out _), Is.Null);
        }

        [Test] public void NullChildFailsWithoutThrowing()
        {
            var tree = Tree(new BossSelectorBranch(null, new BossPatternLeaf(a)));
            Assert.That(Select(tree, out _), Is.SameAs(a));
            Assert.That(Tree(null).Select(new BossSelection(board)), Is.Null);
        }

        [Test] public void SameXKeepsFacingAndMeasuresZeroDistance()
        {
            board.BossPosition = new Vector2(3, 1); board.PlayerPosition = new Vector2(3, 1);
            Assert.That(board.Forward, Is.Zero);
            Assert.That(float.IsFinite(board.Distance), Is.True);
            Assert.That(Distance(3).Evaluate(new BossSelection(board), 0), Is.True);
            Assert.That(new BossSideCondition(BossSide.Front).Evaluate(new BossSelection(board), 0), Is.False);
        }

        private BossSelectorNode Broken(int i)
        {
            var leaf = new BossPatternLeaf(a);
            switch (i)
            {
                case 0: return null;
                case 1: return new BossSelectorBranch();
                case 2: return new BossPatternLeaf(null);
                case 3: return new BossDistanceCondition(5, 1, 0, 0);
                case 4: return new BossInverter(leaf);
                case 5: return new BossSelectorBranch(leaf, leaf);
                default: return new BossWeightedBranch(new BossWeightedChild(0, leaf), new BossWeightedChild(0, new BossPatternLeaf(b)));
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void ValidationRejectsBrokenTrees(int i)
        {
            Assert.That(Tree(Broken(i)).TryValidate(out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test] public void ValidationErrorsNameTheOffendingNode()
        {
            Assert.That(Tree(new BossDistanceCondition(5, 1, 0, 0)).TryValidate(out var error), Is.False);
            Assert.That(error, Does.Contain("거리 5~1"));
        }

        [Test] public void ValidationAcceptsGoodTreeAndRejectsDeepOnes()
        {
            Assert.That(Tree(new BossSelectorBranch(Seq(Distance(3), new BossPatternLeaf(a)), new BossPatternLeaf(b))).TryValidate(out var error), Is.True, error);
            BossSelectorNode deep = new BossPatternLeaf(a);
            for (int i = 0; i < 33; i++) deep = new BossSelectorBranch(deep);
            Assert.That(Tree(deep).TryValidate(out error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test] public void TraceListsEvaluatedNodesInOrder()
        {
            board.PlayerPosition = new Vector2(5, 0);
            var tree = Tree(new BossSelectorBranch(Seq(Distance(3), new BossPatternLeaf(a)), Seq(Distance(10), new BossPatternLeaf(b))));
            Select(tree, out var s);
            var text = s.TraceText;
            int fail = text.IndexOf('✗'), pass = text.LastIndexOf('✓');
            Assert.That(fail, Is.GreaterThanOrEqualTo(0));
            Assert.That(pass, Is.GreaterThan(fail));
            Assert.That(text, Does.Contain("B"));
        }

        [Test] public void TreeSurvivesInstantiateRoundTrip()
        {
            var tree = Tree(new BossSelectorBranch(
                Seq(Distance(3), new BossPlayerStateCondition(BossPlayerState.RecentlyGlitched), new BossPatternLeaf(a, 2)),
                new BossWeightedBranch(new BossWeightedChild(2, new BossPatternLeaf(b)), new BossWeightedChild(0.5f, new BossInverter(new BossSideCondition(BossSide.Behind))))));
            var copy = Object.Instantiate(tree); created.Add(copy);
            Assert.That(copy.Root.GetType(), Is.EqualTo(tree.Root.GetType()));
            Assert.That(copy.Root, Is.Not.SameAs(tree.Root));
            var originalSummaries = new List<string>(); var copySummaries = new List<string>();
            Collect(tree.Root, originalSummaries); Collect(copy.Root, copySummaries);
            Assert.That(copySummaries, Is.EqualTo(originalSummaries));
            board.RepositionState = targetObject.AddComponent<StubReposition>();
            board.PlayerPosition = new Vector2(2, 0); board.SinceGlitch = 0.5f;
            Assert.That(copy.Select(new BossSelection(board)), Is.SameAs(tree.Select(new BossSelection(board))));
            Assert.That(copy.TryValidate(out var error), Is.True, error);
        }

        private static void Collect(BossSelectorNode n, List<string> into)
        {
            if (n == null) { into.Add("null"); return; }
            into.Add(n.GetType().Name + ":" + n.Summary);
            foreach (var c in n.Children) Collect(c, into);
        }
    }
}
