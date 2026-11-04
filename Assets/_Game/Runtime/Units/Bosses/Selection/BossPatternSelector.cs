using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    [CreateAssetMenu(menuName = "Action Platformer/Bosses/Pattern Selector")]
    public sealed class BossPatternSelector : ScriptableObject
    {
        public const int MaxDepth = 32;

        [SerializeReference, BossNodeField] private BossSelectorNode root;

        public BossSelectorNode Root => root;

        public void SetRoot(BossSelectorNode node) { root = node; }

        // Returns the chosen pattern, or null when nothing in the tree can start.
        public BossPatternDefinition Select(BossSelection s)
        {
            s.Pattern = null;
            if (root == null || !root.Evaluate(s, 0)) { s.Pattern = null; return null; }
            return s.Pattern;
        }

        public bool TryValidate(out string error)
        {
            if (root == null) { error = $"{name}: 선택 트리의 루트가 비어 있습니다."; return false; }
            return Walk(root, 0, new HashSet<BossSelectorNode>(), out error);
        }

        private static bool Walk(BossSelectorNode node, int depth, HashSet<BossSelectorNode> visited, out string error)
        {
            if (depth > MaxDepth) { error = $"[{node.Summary}] 트리 깊이가 {MaxDepth}를 넘었습니다."; return false; }
            if (!visited.Add(node)) { error = $"[{node.Summary}] 같은 노드가 두 번 연결되었습니다."; return false; }
            if (!node.TryValidate(out error)) return false;
            foreach (var child in node.Children)
                if (child != null && !Walk(child, depth + 1, visited, out error)) return false;
            error = null;
            return true;
        }
    }
}
