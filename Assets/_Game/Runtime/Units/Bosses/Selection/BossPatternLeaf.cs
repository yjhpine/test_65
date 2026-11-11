using System;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    [Serializable, BossNodeMenu("패턴")]
    public sealed class BossPatternLeaf : BossSelectorNode
    {
        [SerializeField] private BossPatternDefinition pattern;
        [Min(0)] [SerializeField] private int avoidLast;

        public BossPatternLeaf() { }
        public BossPatternLeaf(BossPatternDefinition pattern, int avoidLast = 0) { this.pattern = pattern; this.avoidLast = avoidLast; }

        public BossPatternDefinition Pattern => pattern;

        public override string Summary
        {
            get
            {
                string name = pattern != null ? pattern.DisplayName : "(없음)";
                return avoidLast > 0 ? $"패턴: {name} (최근 {avoidLast}회 제외)" : $"패턴: {name}";
            }
        }

        protected override bool OnEvaluate(BossSelection s, int depth)
        {
            if (pattern == null) return false;
            var board = s.Board;
            if (board.IsCoolingDown(pattern) || board.UsedWithin(pattern, avoidLast) || !pattern.CanStart(s)) return false;
            s.Pattern = pattern;
            return true;
        }

        public override bool TryValidate(out string error)
        {
            if (pattern == null) { error = Fail("패턴이 지정되지 않았습니다."); return false; }
            if (avoidLast < 0) { error = Fail("제외할 최근 횟수는 0 이상이어야 합니다."); return false; }
            if (!pattern.TryValidate(out var inner)) { error = Fail(inner); return false; }
            error = null;
            return true;
        }
    }
}
