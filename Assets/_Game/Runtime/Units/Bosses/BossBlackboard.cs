using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    // Per-boss facts the selection tree reads, plus cooldown/history memory. Never stored in shared assets.
    public sealed class BossBlackboard
    {
        private const int HistoryLength = 8;
        private readonly List<BossPatternDefinition> history = new List<BossPatternDefinition>();
        private readonly Dictionary<BossPatternDefinition, float> readyAt = new Dictionary<BossPatternDefinition, float>();
        private float bossFacing = 1;

        public BossBlackboard(int? seed = null)
        {
            Random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        public float Clock { get; private set; }
        public void Advance(float dt) { if (dt > 0) Clock += dt; }

        public UnitHealth Target { get; set; }
        public bool HasTarget => Target != null;

        public Vector2 BossPosition { get; set; }
        public float BossFacing { get => bossFacing; set => bossFacing = value < 0 ? -1 : 1; }
        public Vector2 PlayerPosition { get; set; }
        public float DeltaX => PlayerPosition.x - BossPosition.x;
        public float DeltaY => PlayerPosition.y - BossPosition.y;
        // Positive when the player is in front of the boss; exactly 0 when level in X.
        public float Forward => DeltaX * BossFacing;
        public float Distance => (PlayerPosition - BossPosition).magnitude;

        // null means "not known": conditions on it fail, even when inverted.
        public bool? PlayerGrounded { get; set; }
        public bool? PlayerUnderground { get; set; }
        public bool? PlayerAttacking { get; set; }
        public bool? PlayerSeesBoss { get; set; }
        public float SinceGlitch { get; set; } = float.PositiveInfinity;

        // Line of sight to the (surface or ground-marker) point the boss judges recognition by.
        public bool TargetInSight { get; set; }
        // Sensor memory owned by BossSenses: the contracts of the sensed target and the last reposition version seen.
        internal UnitHealth SensedTarget;
        internal IPlayerActionState ActionState;
        internal ICharacterMotionState MotionState;
        // Public so tests can model a target with or without the contract; null makes RecentlyGlitched an unknown fact.
        public IUnitRepositionState RepositionState;
        internal IUnitViewSource ViewSource;
        internal bool HasSeenReposition;
        internal uint SeenRepositionVersion;

        public float HealthRatio { get; set; } = 1;
        public int Phase { get; set; }

        public System.Random Random { get; }
        public BossPatternDefinition LastPattern { get; private set; }

        public void RecordStart(BossPatternDefinition p)
        {
            if (p == null) return;
            LastPattern = p;
            history.Add(p);
            if (history.Count > HistoryLength) history.RemoveAt(0);
            readyAt[p] = Clock + Mathf.Max(0, p.Cooldown);
        }

        public bool IsCoolingDown(BossPatternDefinition p) => p != null && readyAt.TryGetValue(p, out var t) && Clock < t;

        public bool UsedWithin(BossPatternDefinition p, int lastCount)
        {
            if (p == null) return false;
            for (int i = history.Count - 1, seen = 0; i >= 0 && seen < lastCount; i--, seen++)
                if (history[i] == p) return true;
            return false;
        }

        public void ResetMemory() { history.Clear(); readyAt.Clear(); LastPattern = null; }
    }
}
