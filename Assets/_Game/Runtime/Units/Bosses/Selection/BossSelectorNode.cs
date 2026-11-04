using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    // Marks a [SerializeReference] BossSelectorNode field so the editor draws the node picker.
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class BossNodeFieldAttribute : PropertyAttribute { }

    // Menu path ("group/name") shown when adding this node type in the editor.
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BossNodeMenuAttribute : Attribute
    {
        public BossNodeMenuAttribute(string path) { Path = path; }
        public string Path { get; }
    }

    [Serializable]
    public abstract class BossSelectorNode
    {
        private static readonly BossSelectorNode[] NoChildren = new BossSelectorNode[0];

        public abstract string Summary { get; }
        // May contain null entries; callers skip them.
        public virtual IEnumerable<BossSelectorNode> Children => NoChildren;

        public bool Evaluate(BossSelection s, int depth)
        {
            int slot = s.Reserve(depth);
            bool passed = OnEvaluate(s, depth);
            s.Fill(slot, depth, passed, Summary);
            return passed;
        }

        protected abstract bool OnEvaluate(BossSelection s, int depth);

        public virtual bool TryValidate(out string error) { error = null; return true; }

        protected string Fail(string message) => $"[{Summary}] {message}";
    }

    // Tests the blackboard only; never picks a pattern.
    [Serializable]
    public abstract class BossCondition : BossSelectorNode
    {
        protected sealed override bool OnEvaluate(BossSelection s, int depth) => IsKnown(s) && Check(s);

        protected abstract bool Check(BossSelection s);

        // False when the fact is unknown. Inverter treats unknown as failure instead of flipping it.
        public virtual bool IsKnown(BossSelection s) => s.Board.HasTarget;
    }
}
