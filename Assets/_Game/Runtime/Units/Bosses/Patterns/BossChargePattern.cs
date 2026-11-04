using System.Collections.Generic;
using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    [CreateAssetMenu(menuName = "Action Platformer/Bosses/Patterns/Charge", fileName = "BossCharge")]
    public sealed class BossChargePattern : BossPatternDefinition
    {
        private const float SafetyTime = .5f;
        [Min(0)] [SerializeField] private float telegraphTime = .6f;
        [Min(.1f)] [SerializeField] private float distance = 7;
        [Min(.1f)] [SerializeField] private float speed = 16;
        [Min(0)] [SerializeField] private int damage = 12;

        public float TelegraphTime => telegraphTime;
        public float Distance => distance;
        public float Speed => speed;
        public int Damage => damage;

        public override BossPatternRun CreateRun(BossContext context) => new Run(this, context);

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error)) return false;
            if (!BossPatternChecks.NonNegative(telegraphTime)) { error = $"{DisplayName}: telegraphTime은 0 이상이어야 합니다."; return false; }
            if (!BossPatternChecks.Positive(distance) || !BossPatternChecks.Positive(speed)) { error = $"{DisplayName}: distance와 speed는 0보다 커야 합니다."; return false; }
            if (damage < 0) { error = $"{DisplayName}: damage는 0 이상이어야 합니다."; return false; }
            error = null;
            return true;
        }

        private sealed class Run : BossPatternRun
        {
            private readonly BossChargePattern owner;
            private readonly HashSet<UnitHealth> struck = new HashSet<UnitHealth>();
            private float elapsed;
            private Vector2 previous;
            private bool telegraphing;

            public Run(BossChargePattern owner, BossContext context) : base(context) { this.owner = owner; }

            public override void BeginTelegraph() { elapsed = 0; telegraphing = true; }
            public override bool TickTelegraph(float dt) { elapsed += dt; return elapsed >= owner.telegraphTime; }

            public override void BeginExecute()
            {
                elapsed = 0; telegraphing = false;
                struck.Clear();
                previous = Context.Combat.Center;
                Context.Mover.MoveTo(Context.Mover.Position.x + Context.Facing * owner.distance, owner.speed);
            }

            public override bool TickExecute(float dt)
            {
                elapsed += dt;
                Vector2 now = Context.Combat.Center;
                Context.Combat.HitSweep(previous, now, owner.damage, struck);
                previous = now;
                var mover = Context.Mover;
                if (!(mover.HasArrived || mover.IsBlocked || !mover.IsGrounded || elapsed >= owner.distance / owner.speed + SafetyTime)) return false;
                mover.Stop();
                return true;
            }

            public override void End() { telegraphing = false; }
            public override void Cancel() { telegraphing = false; }
            public override float Progress => telegraphing ? Mathf.Clamp01(elapsed / Mathf.Max(.0001f, owner.telegraphTime))
                : Mathf.Clamp01(elapsed / (owner.distance / owner.speed + SafetyTime));
            public override BossTelegraph Telegraph => telegraphing ? new BossTelegraph
            {
                Visible = true,
                Center = Context.Combat.Center + Vector2.right * Context.Facing * owner.distance * .5f,
                // Same box HitSweep uses over the full travel: the body at both ends.
                Size = new Vector2(owner.distance + Context.Combat.BodySize.x, Context.Combat.BodySize.y)
            } : default;
        }
    }
}
