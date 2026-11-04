using ActionPlatformer.Units.Features;
using ActionPlatformer.Units.Fsm;
using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    // Authored boss settings. Each boss gets its own BossBrain; nothing here changes at runtime.
    [CreateAssetMenu(menuName = "Action Platformer/Bosses/Boss FSM Definition")]
    public sealed class BossFsmDefinition : FsmDefinition
    {
        [Header("Selection")]
        [SerializeField] private BossPatternSelector selector;
        [Min(.1f)] [SerializeField] private float recognitionRange = 14;
        [Min(.1f)] [SerializeField] private float recognitionHeight = 4;
        [Tooltip("Seconds the player may stay unrecognized before the boss gives up and goes back to dormant.")]
        [Min(.01f)] [SerializeField] private float loseTargetTime = 3;
        [Min(.01f)] [SerializeField] private float decisionInterval = .2f;
        [Header("Movement")]
        [Min(.01f)] [SerializeField] private float approachSpeed = 3;
        [Min(.01f)] [SerializeField] private float preferredDistance = 2.5f;
        [Header("Stagger")]
        [Min(.01f)] [SerializeField] private float staggerDuration = .6f;
        [Tooltip("After a stagger ends, further interrupting hits are ignored for this long.")]
        [Min(0)] [SerializeField] private float staggerImmunity = 2;
        [Header("Phases")]
        [Tooltip("Health ratios, strictly descending, each between 0 and 1. Crossing one starts the next phase.")]
        [SerializeField] private float[] phaseThresholds = { .5f };
        [Min(0)] [SerializeField] private float phaseShiftDuration = 1.5f;
        [SerializeField] private bool invulnerableDuringPhaseShift = true;
        [Header("Death")]
        [Min(.01f)] [SerializeField] private float deathDuration = 1.2f;
        [Header("Animation states")]
        [SerializeField] private string idleAnimation = "Idle";
        [SerializeField] private string moveAnimation = "Run";
        [SerializeField] private string staggerAnimation = "Hit";
        [SerializeField] private string phaseShiftAnimation = "PhaseShift";
        [SerializeField] private string deathAnimation = "Death";
        [Header("Randomness")]
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int seed;

        public BossPatternSelector Selector => selector;
        public float RecognitionRange => recognitionRange;
        public float RecognitionHeight => recognitionHeight;
        public float LoseTargetTime => loseTargetTime;
        public float DecisionInterval => decisionInterval;
        public float ApproachSpeed => approachSpeed;
        public float PreferredDistance => preferredDistance;
        public float StaggerDuration => staggerDuration;
        public float StaggerImmunity => staggerImmunity;
        // A copy, so callers cannot change the shared asset.
        public IReadOnlyList<float> PhaseThresholds => System.Array.AsReadOnly(phaseThresholds != null ? (float[])phaseThresholds.Clone() : System.Array.Empty<float>());
        public float PhaseShiftDuration => phaseShiftDuration;
        public bool InvulnerableDuringPhaseShift => invulnerableDuringPhaseShift;
        public float DeathDuration => deathDuration;
        public string IdleAnimation => idleAnimation;
        public string MoveAnimation => moveAnimation;
        public string StaggerAnimation => staggerAnimation;
        public string PhaseShiftAnimation => phaseShiftAnimation;
        public string DeathAnimation => deathAnimation;
        public bool UseFixedSeed => useFixedSeed;
        public int Seed => seed;

        // The number of thresholds the health ratio has reached or passed.
        public int PhaseFor(float healthRatio)
        {
            int phase = 0;
            if (phaseThresholds == null) return phase;
            foreach (float threshold in phaseThresholds) if (healthRatio <= threshold) phase++;
            return phase;
        }

        public bool TryValidate(out string error)
        {
            float[] positive = { recognitionRange, recognitionHeight, loseTargetTime, decisionInterval, approachSpeed, preferredDistance, staggerDuration, deathDuration };
            float[] nonnegative = { staggerImmunity, phaseShiftDuration };
            foreach (float v in positive)
                if (!(v > 0f) || float.IsInfinity(v)) { error = $"{name}: 거리·시간 값은 0보다 큰 유한한 수여야 합니다."; return false; }
            foreach (float v in nonnegative)
                if (!(v >= 0f) || float.IsInfinity(v)) { error = $"{name}: 경직 면역·구간 전환 시간은 0 이상의 유한한 수여야 합니다."; return false; }
            if (phaseThresholds != null)
                for (int i = 0; i < phaseThresholds.Length; i++)
                {
                    float t = phaseThresholds[i];
                    if (!(t > 0f && t < 1f)) { error = $"{name}: 구간 경계는 0보다 크고 1보다 작아야 합니다."; return false; }
                    if (i > 0 && !(t < phaseThresholds[i - 1])) { error = $"{name}: 구간 경계는 엄격한 내림차순이어야 합니다."; return false; }
                }
            if (selector == null) { error = $"{name}: 패턴 선택 트리가 지정되지 않았습니다."; return false; }
            if (!selector.TryValidate(out error)) return false;
            error = null;
            return true;
        }

        protected override IFsmState CreateInitialState(Unit owner)
        {
            if (!TryValidate(out string error)) throw new System.InvalidOperationException(error);
            if (!owner.TryGetComponent<UnitHealth>(out _) || !owner.TryGetComponent<BossCombat2D>(out _) || !owner.TryGetComponent<GroundMovement2D>(out _))
                throw new System.InvalidOperationException("Boss FSM requires UnitHealth, BossCombat2D and GroundMovement2D.");
            return new BossBrain(owner, this).InitialState;
        }
    }
}
