using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    [Serializable]
    public abstract class BossBranch : BossSelectorNode
    {
        [SerializeReference, BossNodeField] private List<BossSelectorNode> children = new List<BossSelectorNode>();

        protected BossBranch() { }
        protected BossBranch(IEnumerable<BossSelectorNode> nodes) { if (nodes != null) children.AddRange(nodes); }

        public IReadOnlyList<BossSelectorNode> Items => children;
        public override IEnumerable<BossSelectorNode> Children => children;

        public override bool TryValidate(out string error)
        {
            if (children.Count == 0) { error = Fail("묶음에 자식이 없습니다."); return false; }
            for (int i = 0; i < children.Count; i++)
                if (children[i] == null) { error = Fail($"자식 {i + 1}번이 비어 있습니다."); return false; }
            error = null;
            return true;
        }
    }

    [Serializable, BossNodeMenu("묶음/Selector")]
    public sealed class BossSelectorBranch : BossBranch
    {
        public BossSelectorBranch() { }
        public BossSelectorBranch(params BossSelectorNode[] nodes) : base(nodes) { }

        public override string Summary => $"Selector ({Items.Count})";

        protected override bool OnEvaluate(BossSelection s, int depth)
        {
            var before = s.Pattern;
            foreach (var child in Items)
                if (child != null && child.Evaluate(s, depth + 1)) return true;
            s.Pattern = before;
            return false;
        }
    }

    [Serializable, BossNodeMenu("묶음/Sequence")]
    public sealed class BossSequenceBranch : BossBranch
    {
        public BossSequenceBranch() { }
        public BossSequenceBranch(params BossSelectorNode[] nodes) : base(nodes) { }

        public override string Summary => $"Sequence ({Items.Count})";

        protected override bool OnEvaluate(BossSelection s, int depth)
        {
            var before = s.Pattern;
            foreach (var child in Items)
            {
                if (child != null && child.Evaluate(s, depth + 1)) continue;
                s.Pattern = before;
                return false;
            }
            return Items.Count > 0;
        }
    }

    [Serializable]
    public sealed class BossWeightedChild
    {
        public float weight = 1;
        [SerializeReference, BossNodeField] public BossSelectorNode node;

        public BossWeightedChild() { }
        public BossWeightedChild(float weight, BossSelectorNode node) { this.weight = weight; this.node = node; }
    }

    [Serializable, BossNodeMenu("묶음/Weighted Random")]
    public sealed class BossWeightedBranch : BossSelectorNode
    {
        [SerializeField] private List<BossWeightedChild> children = new List<BossWeightedChild>();

        public BossWeightedBranch() { }
        public BossWeightedBranch(params BossWeightedChild[] nodes) { if (nodes != null) children.AddRange(nodes); }

        public IReadOnlyList<BossWeightedChild> Items => children;
        public override string Summary => $"Weighted Random ({children.Count})";

        public override IEnumerable<BossSelectorNode> Children
        {
            get { foreach (var c in children) yield return c?.node; }
        }

        protected override bool OnEvaluate(BossSelection s, int depth)
        {
            // Pre-evaluate every weighted child on its own selection so only the drawn one leaves a trace or a pattern.
            var passed = new List<(BossWeightedChild child, BossSelection selection)>();
            float total = 0;
            foreach (var c in children)
            {
                if (c == null || c.node == null || !(c.weight > 0)) continue;
                var temp = new BossSelection(s.Board, s.Context) { Pattern = s.Pattern };
                if (!c.node.Evaluate(temp, depth + 1)) continue;
                passed.Add((c, temp));
                total += c.weight;
            }
            if (passed.Count == 0) return false;

            double roll = s.Board.Random.NextDouble() * total;
            var chosen = passed[passed.Count - 1];
            foreach (var entry in passed)
            {
                roll -= entry.child.weight;
                if (roll < 0) { chosen = entry; break; }
            }
            s.Pattern = chosen.selection.Pattern;
            s.AppendTrace(chosen.selection);
            return true;
        }

        public override bool TryValidate(out string error)
        {
            if (children.Count == 0) { error = Fail("묶음에 자식이 없습니다."); return false; }
            float total = 0;
            for (int i = 0; i < children.Count; i++)
            {
                var c = children[i];
                if (c == null || c.node == null) { error = Fail($"자식 {i + 1}번이 비어 있습니다."); return false; }
                if (c.weight < 0 || float.IsNaN(c.weight)) { error = Fail($"자식 {i + 1}번의 가중치가 음수입니다."); return false; }
                total += c.weight;
            }
            if (!(total > 0)) { error = Fail("가중치가 모두 0입니다."); return false; }
            error = null;
            return true;
        }
    }

    [Serializable, BossNodeMenu("장식/Inverter")]
    public sealed class BossInverter : BossSelectorNode
    {
        [SerializeReference, BossNodeField] private BossSelectorNode condition;

        public BossInverter() { }
        public BossInverter(BossSelectorNode condition) { this.condition = condition; }

        public override string Summary => "Inverter";
        public override IEnumerable<BossSelectorNode> Children { get { yield return condition; } }

        protected override bool OnEvaluate(BossSelection s, int depth)
        {
            if (condition == null) return false;
            var before = s.Pattern;
            bool result = condition.Evaluate(s, depth + 1);
            s.Pattern = before;
            // Unknown facts stay failures instead of becoming successes.
            if (condition is BossCondition c && !c.IsKnown(s)) return false;
            return !result;
        }

        public override bool TryValidate(out string error)
        {
            if (condition == null) { error = Fail("반전할 조건이 비어 있습니다."); return false; }
            if (!(condition is BossCondition)) { error = Fail("Inverter의 자식은 조건이어야 합니다."); return false; }
            error = null;
            return true;
        }
    }
}
