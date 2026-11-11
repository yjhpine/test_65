using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    [CreateAssetMenu(menuName = "Action Platformer/Bosses/Patterns/Reposition", fileName = "BossReposition")]
    public sealed class BossRepositionPattern : BossPatternDefinition
    {
        [Min(0)] [SerializeField] private float telegraphTime;
        [Min(.1f)] [SerializeField] private float desiredDistance = 6;
        [Min(.1f)] [SerializeField] private float speed = 6;
        [Min(.1f)] [SerializeField] private float maxTime = 1.5f;

        public float TelegraphTime => telegraphTime;
        public float DesiredDistance => desiredDistance;
        public float Speed => speed;
        public float MaxTime => maxTime;

        public override BossPatternRun CreateRun(BossContext context) => new Run(this, context);

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error)) return false;
            if (!BossPatternChecks.NonNegative(telegraphTime)) { error = $"{DisplayName}: telegraphTime은 0 이상이어야 합니다."; return false; }
            if (!BossPatternChecks.Positive(desiredDistance) || !BossPatternChecks.Positive(speed) || !BossPatternChecks.Positive(maxTime))
            { error = $"{DisplayName}: desiredDistance, speed, maxTime은 0보다 커야 합니다."; return false; }
            error = null;
            return true;
        }

        private sealed class Run : BossPatternRun
        {
            private readonly BossRepositionPattern owner;
            private float elapsed;
            private bool telegraphing, finished;

            public Run(BossRepositionPattern owner, BossContext context) : base(context) { this.owner = owner; }

            public override void BeginTelegraph() { elapsed = 0; telegraphing = true; }
            public override bool TickTelegraph(float dt) { elapsed += dt; return elapsed >= owner.telegraphTime; }

            public override void BeginExecute()
            {
                elapsed = 0; telegraphing = false; finished = false;
                var board = Context.Board;
                // Without a target there is nowhere to keep distance from.
                if (!board.HasTarget) { finished = true; return; }
                // Standing exactly on the player: it counts as being on the side the boss faces, so the boss backs off the other way.
                float away = Mathf.Abs(board.DeltaX) > .01f ? Mathf.Sign(board.DeltaX) : Context.Facing;
                // DeltaX is measured from the collider center, MoveTo takes the body's x (same compensation as the brain's Approach).
                Context.Mover.MoveTo(Context.Mover.Position.x + board.DeltaX - away * owner.desiredDistance, owner.speed);
            }

            public override bool TickExecute(float dt)
            {
                elapsed += dt;
                var mover = Context.Mover;
                if (!finished && !(mover.HasArrived || mover.IsBlocked || elapsed >= owner.maxTime)) return false;
                mover.Stop();
                return true;
            }

            public override void End() { telegraphing = false; }
            public override void Cancel() { telegraphing = false; }
            public override float Progress => telegraphing ? Mathf.Clamp01(elapsed / Mathf.Max(.0001f, owner.telegraphTime))
                : Mathf.Clamp01(elapsed / owner.maxTime);
        }
    }
}
