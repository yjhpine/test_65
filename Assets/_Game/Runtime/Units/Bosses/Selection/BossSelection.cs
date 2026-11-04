using System.Collections.Generic;

namespace ActionPlatformer.Units.Bosses
{
    // One evaluation pass over a selector tree: the chosen pattern plus a readable trace.
    public sealed class BossSelection
    {
        private readonly List<string> lines = new List<string>();

        public BossSelection(BossBlackboard board, BossContext context = null)
        {
            Board = board;
            Context = context;
        }

        public BossBlackboard Board { get; }
        public BossContext Context { get; }
        public BossPatternDefinition Pattern { get; set; }
        public string TraceText => string.Join("\n", lines);

        public void Trace(int depth, bool passed, string summary) => lines.Add(Format(depth, passed, summary));

        // Nodes reserve their line before evaluating children so the trace reads parent-first.
        internal int Reserve(int depth) { lines.Add(Format(depth, false, "")); return lines.Count - 1; }
        internal void Fill(int slot, int depth, bool passed, string summary) => lines[slot] = Format(depth, passed, summary);
        internal void AppendTrace(BossSelection other) => lines.AddRange(other.lines);

        private static string Format(int depth, bool passed, string summary) =>
            new string(' ', System.Math.Max(0, depth) * 2) + (passed ? "✓ " : "✗ ") + summary;
    }
}
