using System;
using System.Globalization;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    public enum BossSide { Front, Behind }
    public enum BossPlayerState { Grounded, Airborne, Underground, Attacking, RecentlyGlitched, WatchingBoss }

    internal static class BossNodeText
    {
        public static string N(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    [Serializable, BossNodeMenu("조건/거리")]
    public sealed class BossDistanceCondition : BossCondition
    {
        [SerializeField] private float minHorizontal;
        [SerializeField] private float maxHorizontal = 3;
        [SerializeField] private float minVertical = -1.5f;
        [SerializeField] private float maxVertical = 1.5f;

        public BossDistanceCondition() { }
        public BossDistanceCondition(float minHorizontal, float maxHorizontal, float minVertical, float maxVertical)
        {
            this.minHorizontal = minHorizontal; this.maxHorizontal = maxHorizontal;
            this.minVertical = minVertical; this.maxVertical = maxVertical;
        }

        public override string Summary =>
            $"거리 {BossNodeText.N(minHorizontal)}~{BossNodeText.N(maxHorizontal)} / 높이 {BossNodeText.N(minVertical)}~{BossNodeText.N(maxVertical)}";

        protected override bool Check(BossSelection s)
        {
            float dx = Mathf.Abs(s.Board.DeltaX), dy = s.Board.DeltaY;
            return dx >= minHorizontal && dx <= maxHorizontal && dy >= minVertical && dy <= maxVertical;
        }

        public override bool TryValidate(out string error)
        {
            if (minHorizontal > maxHorizontal) { error = Fail("수평 거리 범위가 뒤집혔습니다 (min > max)."); return false; }
            if (minVertical > maxVertical) { error = Fail("높이 범위가 뒤집혔습니다 (min > max)."); return false; }
            error = null;
            return true;
        }
    }

    [Serializable, BossNodeMenu("조건/앞뒤")]
    public sealed class BossSideCondition : BossCondition
    {
        [SerializeField] private BossSide side;

        public BossSideCondition() { }
        public BossSideCondition(BossSide side) { this.side = side; }

        public override string Summary => side == BossSide.Front ? "플레이어가 앞" : "플레이어가 뒤";

        // Level in X is neither in front nor behind.
        protected override bool Check(BossSelection s) => side == BossSide.Front ? s.Board.Forward > 0 : s.Board.Forward < 0;
    }

    [Serializable, BossNodeMenu("조건/플레이어 상태")]
    public sealed class BossPlayerStateCondition : BossCondition
    {
        [SerializeField] private BossPlayerState state;
        [Min(0)] [SerializeField] private float withinSeconds = 1;

        public BossPlayerStateCondition() { }
        public BossPlayerStateCondition(BossPlayerState state, float withinSeconds = 1) { this.state = state; this.withinSeconds = withinSeconds; }

        public override string Summary => state switch
        {
            BossPlayerState.Grounded => "플레이어 지상",
            BossPlayerState.Airborne => "플레이어 공중",
            BossPlayerState.Underground => "플레이어 잠복",
            BossPlayerState.Attacking => "플레이어 공격 중",
            BossPlayerState.RecentlyGlitched => $"플레이어 글리치 {BossNodeText.N(withinSeconds)}초 이내",
            _ => "플레이어가 보스를 봄",
        };

        public override bool IsKnown(BossSelection s)
        {
            if (!s.Board.HasTarget) return false;
            var b = s.Board;
            switch (state)
            {
                case BossPlayerState.Grounded:
                case BossPlayerState.Airborne: return b.PlayerGrounded.HasValue;
                case BossPlayerState.Underground: return b.PlayerUnderground.HasValue;
                case BossPlayerState.Attacking: return b.PlayerAttacking.HasValue;
                case BossPlayerState.WatchingBoss: return b.PlayerSeesBoss.HasValue;
                default: return b.RepositionState != null;
            }
        }

        protected override bool Check(BossSelection s)
        {
            var b = s.Board;
            switch (state)
            {
                case BossPlayerState.Grounded: return b.PlayerGrounded == true;
                case BossPlayerState.Airborne: return b.PlayerGrounded == false;
                case BossPlayerState.Underground: return b.PlayerUnderground == true;
                case BossPlayerState.Attacking: return b.PlayerAttacking == true;
                case BossPlayerState.WatchingBoss: return b.PlayerSeesBoss == true;
                default: return b.SinceGlitch <= withinSeconds;
            }
        }
    }

    [Serializable, BossNodeMenu("조건/체력 구간")]
    public sealed class BossPhaseCondition : BossCondition
    {
        [SerializeField] private int minPhase;
        [SerializeField] private int maxPhase = 9;

        public BossPhaseCondition() { }
        public BossPhaseCondition(int minPhase, int maxPhase) { this.minPhase = minPhase; this.maxPhase = maxPhase; }

        public override string Summary => $"체력 구간 {minPhase}~{maxPhase}";
        public override bool IsKnown(BossSelection s) => true;
        protected override bool Check(BossSelection s) => s.Board.Phase >= minPhase && s.Board.Phase <= maxPhase;

        public override bool TryValidate(out string error)
        {
            if (minPhase > maxPhase) { error = Fail("체력 구간 범위가 뒤집혔습니다 (min > max)."); return false; }
            error = null;
            return true;
        }
    }

    [Serializable, BossNodeMenu("조건/확률")]
    public sealed class BossChanceCondition : BossCondition
    {
        [Range(0, 1)] [SerializeField] private float chance = 0.5f;

        public BossChanceCondition() { }
        public BossChanceCondition(float chance) { this.chance = chance; }

        public override string Summary => $"확률 {BossNodeText.N(chance * 100)}%";
        public override bool IsKnown(BossSelection s) => true;
        protected override bool Check(BossSelection s) => s.Board.Random.NextDouble() < chance;
    }
}
