using System.Collections.Generic;
using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    [CreateAssetMenu(menuName = "Action Platformer/Bosses/Patterns/Melee Area", fileName = "BossMeleeArea")]
    public sealed class BossMeleeAreaPattern : BossPatternDefinition
    {
        [Min(0)] [SerializeField] private float telegraphTime = .5f;
        [Min(1)] [SerializeField] private int hitCount = 1;
        [Min(0)] [SerializeField] private float hitInterval = .25f;
        [Min(.01f)] [SerializeField] private float activeTime = .15f;
        [Tooltip("Relative to the boss center; X is mirrored by the facing fixed at telegraph start.")]
        [SerializeField] private Vector2 boxOffset = new Vector2(1.5f, 0);
        [SerializeField] private Vector2 boxSize = new Vector2(2.5f, 2);
        [Min(0)] [SerializeField] private int damage = 10;

        public float TelegraphTime => telegraphTime;
        public int HitCount => hitCount;
        public float HitInterval => hitInterval;
        public float ActiveTime => activeTime;
        public Vector2 BoxOffset => boxOffset;
        public Vector2 BoxSize => boxSize;
        public int Damage => damage;
        public float ExecuteDuration => (hitCount - 1) * hitInterval + activeTime;

        public override BossPatternRun CreateRun(BossContext context) => new Run(this, context);

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error)) return false;
            if (!BossPatternChecks.NonNegative(telegraphTime) || !BossPatternChecks.NonNegative(hitInterval)) { error = $"{DisplayName}: telegraphTime과 hitInterval은 0 이상이어야 합니다."; return false; }
            if (!BossPatternChecks.Positive(activeTime)) { error = $"{DisplayName}: activeTime은 0보다 커야 합니다."; return false; }
            if (hitCount < 1) { error = $"{DisplayName}: hitCount는 1 이상이어야 합니다."; return false; }
            if (!BossPatternChecks.Positive(boxSize.x) || !BossPatternChecks.Positive(boxSize.y)) { error = $"{DisplayName}: boxSize는 0보다 커야 합니다."; return false; }
            if (damage < 0) { error = $"{DisplayName}: damage는 0 이상이어야 합니다."; return false; }
            error = null;
            return true;
        }

        private sealed class Run : BossPatternRun
        {
            private readonly BossMeleeAreaPattern owner;
            private readonly HashSet<UnitHealth> struck = new HashSet<UnitHealth>();
            private float elapsed;
            private int swings;
            private bool telegraphing;

            public Run(BossMeleeAreaPattern owner, BossContext context) : base(context) { this.owner = owner; }

            private Vector2 BoxCenter => Context.Combat.Center + new Vector2(owner.boxOffset.x * Context.Facing, owner.boxOffset.y);

            public override void BeginTelegraph() { elapsed = 0; telegraphing = true; }
            public override bool TickTelegraph(float dt) { elapsed += dt; return elapsed >= owner.telegraphTime; }
            public override void BeginExecute() { elapsed = 0; swings = 0; telegraphing = false; }

            public override bool TickExecute(float dt)
            {
                elapsed += dt;
                while (swings < owner.hitCount && elapsed >= swings * owner.hitInterval - 1e-5f)
                {
                    struck.Clear();
                    Context.Combat.HitBox(BoxCenter, owner.boxSize, owner.damage, struck);
                    swings++;
                }
                return elapsed >= owner.ExecuteDuration;
            }

            public override void End() { telegraphing = false; }
            public override void Cancel() { telegraphing = false; }
            public override float Progress => telegraphing ? Mathf.Clamp01(elapsed / Mathf.Max(.0001f, owner.telegraphTime))
                : Mathf.Clamp01(elapsed / Mathf.Max(.0001f, owner.ExecuteDuration));
            public override BossTelegraph Telegraph => telegraphing ? new BossTelegraph { Visible = true, Center = BoxCenter, Size = owner.boxSize, Urgency = Progress } : default;
        }
    }
}
