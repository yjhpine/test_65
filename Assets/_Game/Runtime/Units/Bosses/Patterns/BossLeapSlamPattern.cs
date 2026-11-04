using System.Collections.Generic;
using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    [CreateAssetMenu(menuName = "Action Platformer/Bosses/Patterns/Leap Slam", fileName = "BossLeapSlam")]
    public sealed class BossLeapSlamPattern : BossPatternDefinition
    {
        private const float GroundSearch = 60f;
        [Min(0)] [SerializeField] private float telegraphTime = .3f;
        [Min(.01f)] [SerializeField] private float riseTime = .4f;
        [Min(.1f)] [SerializeField] private float jumpHeight = 5;
        [Tooltip("The landing point follows the player for this long, then it is fixed.")]
        [Min(0)] [SerializeField] private float aimTime = .6f;
        [Min(.1f)] [SerializeField] private float airSpeed = 10;
        [Tooltip("Seconds from the moment the landing point is fixed until touchdown.")]
        [Min(0)] [SerializeField] private float landDelay = 1;
        [Min(.1f)] [SerializeField] private float dropSpeed = 30;
        [Min(.1f)] [SerializeField] private float shockwaveRadius = 3;
        [Min(0)] [SerializeField] private int damage = 15;
        [Min(0)] [SerializeField] private float landingTime = .15f;

        public float TelegraphTime => telegraphTime;
        public float RiseTime => riseTime;
        public float JumpHeight => jumpHeight;
        public float AimTime => aimTime;
        public float AirSpeed => airSpeed;
        public float LandDelay => landDelay;
        public float DropSpeed => dropSpeed;
        public float ShockwaveRadius => shockwaveRadius;
        public int Damage => damage;
        public float LandingTime => landingTime;
        public float ExecuteDuration => riseTime + aimTime + landDelay + landingTime;

        public override bool CanStart(BossSelection s) => s.Context != null && s.Context.Mover.IsGrounded && s.Board.HasTarget;

        public override BossPatternRun CreateRun(BossContext context) => new Run(this, context);

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error)) return false;
            if (!BossPatternChecks.Positive(riseTime) || !BossPatternChecks.Positive(jumpHeight) || !BossPatternChecks.Positive(airSpeed) ||
                !BossPatternChecks.Positive(dropSpeed) || !BossPatternChecks.Positive(shockwaveRadius))
            { error = $"{DisplayName}: riseTime, jumpHeight, airSpeed, dropSpeed, shockwaveRadius는 0보다 커야 합니다."; return false; }
            if (!BossPatternChecks.NonNegative(telegraphTime) || !BossPatternChecks.NonNegative(aimTime) ||
                !BossPatternChecks.NonNegative(landDelay) || !BossPatternChecks.NonNegative(landingTime))
            { error = $"{DisplayName}: telegraphTime, aimTime, landDelay, landingTime은 0 이상이어야 합니다."; return false; }
            if (damage < 0) { error = $"{DisplayName}: damage는 0 이상이어야 합니다."; return false; }
            error = null;
            return true;
        }

        private sealed class Run : BossPatternRun
        {
            private readonly BossLeapSlamPattern owner;
            private readonly HashSet<UnitHealth> struck = new HashSet<UnitHealth>();
            private float telegraphElapsed, clock;
            private bool telegraphing, executing, locked, landed;
            // center is the body collider center; the rigidbody is placed at center - pivotOffset.
            private Vector2 center, pivotOffset;
            private float startY, riseHeight, halfHeight, landingX, surfaceY, touchdownAt, dropStart, dropTime, dropFromY;

            public Run(BossLeapSlamPattern owner, BossContext context) : base(context) { this.owner = owner; }

            private float LandingCenterY => surfaceY + halfHeight + .02f;

            public override void BeginTelegraph() { telegraphElapsed = 0; telegraphing = true; executing = false; locked = false; landed = false; clock = 0; }
            public override bool TickTelegraph(float dt) { telegraphElapsed += dt; return telegraphElapsed >= owner.telegraphTime; }

            public override void BeginExecute()
            {
                telegraphing = false; executing = true; locked = false; landed = false; clock = 0;
                struck.Clear();
                var combat = Context.Combat;
                center = combat.Center;
                pivotOffset = center - Context.Mover.Position;
                halfHeight = combat.BodySize.y * .5f;
                startY = center.y;
                riseHeight = Mathf.Max(0f, combat.ClampVertical(center, center.y + owner.jumpHeight) - center.y);
                landingX = center.x;
                if (!combat.TryFindGround(landingX, center.y, GroundSearch, out surfaceY)) surfaceY = center.y - halfHeight - .02f;
                Context.Mover.BeginAirborne();
            }

            public override bool TickExecute(float dt)
            {
                if (!executing) return true;
                clock += dt;
                float riseEnd = owner.riseTime, aimEnd = riseEnd + owner.aimTime;
                if (!locked && clock >= riseEnd) TrackPlayer();
                if (!locked && clock >= aimEnd)
                {
                    locked = true;
                    touchdownAt = aimEnd + owner.landDelay;
                    dropFromY = startY + riseHeight;
                    dropTime = Mathf.Clamp(Mathf.Max(0f, dropFromY - LandingCenterY) / owner.dropSpeed, 0f, owner.landDelay);
                    dropStart = touchdownAt - dropTime;
                }

                if (clock < riseEnd) { float f = Mathf.Clamp01(clock / riseEnd); center.y = startY + riseHeight * (1f - (1f - f) * (1f - f)); }
                else center.y = startY + riseHeight;
                if (locked && clock >= dropStart)
                    center.y = Mathf.Lerp(dropFromY, LandingCenterY, dropTime > 0f ? Mathf.Clamp01((clock - dropStart) / dropTime) : 1f);
                if (clock >= riseEnd && !landed) MoveHorizontally(dt);

                if (locked && clock >= touchdownAt && !landed)
                {
                    landed = true;
                    // Where the body actually is wins over the planned point if something stopped it.
                    landingX = center.x; center.y = LandingCenterY;
                    Context.Mover.SetAirbornePosition(center - pivotOffset);
                    Context.Mover.EndAirborne();
                    Context.Combat.HitCircle(new Vector2(landingX, surfaceY + .5f), owner.shockwaveRadius, owner.damage, struck);
                }
                else if (!landed) Context.Mover.SetAirbornePosition(center - pivotOffset);
                if (!landed || clock < touchdownAt + owner.landingTime) return false;
                executing = false;
                return true;
            }

            // The landing point follows the player, but only onto ground the boss can reach from where it hangs.
            private void TrackPlayer()
            {
                var board = Context.Board;
                if (!board.HasTarget) return;
                var combat = Context.Combat;
                float x = combat.ClampHorizontal(center, board.PlayerPosition.x);
                if (!combat.TryFindGround(x, board.PlayerPosition.y + .1f, GroundSearch, out float y) || y + halfHeight + .02f > center.y + .01f) return;
                landingX = x; surfaceY = y;
            }

            private void MoveHorizontally(float dt)
            {
                float dx = landingX - center.x;
                if (Mathf.Abs(dx) < 1e-4f) return;
                float speed = owner.airSpeed;
                if (locked)
                {
                    // Arrive above the landing point before the drop (or before touchdown when there is no hover time).
                    float deadline = dropStart > touchdownAt - owner.landDelay + 1e-4f ? dropStart : touchdownAt;
                    float left = deadline - clock;
                    speed = left <= dt + 1e-4f ? Mathf.Abs(dx) / dt : Mathf.Max(speed, Mathf.Abs(dx) / left);
                }
                float step = Mathf.Min(Mathf.Abs(dx), speed * dt);
                center.x = Context.Combat.ClampHorizontal(center, center.x + Mathf.Sign(dx) * step);
            }

            public override void End() { Release(); }
            public override void Cancel() { Release(); }
            public override void OnBossStopped() { Release(); }

            // The boss falls with gravity from wherever it was.
            private void Release()
            {
                telegraphing = false; executing = false;
                if (Context.Mover.IsAirborne) Context.Mover.EndAirborne();
            }

            public override float Progress => telegraphing ? Mathf.Clamp01(telegraphElapsed / Mathf.Max(.0001f, owner.telegraphTime))
                : Mathf.Clamp01(clock / Mathf.Max(.0001f, owner.ExecuteDuration));
            public override BossTelegraph Telegraph => executing && !landed ? new BossTelegraph
            {
                Visible = true,
                Center = new Vector2(landingX, surfaceY + .15f),
                Size = new Vector2(owner.shockwaveRadius * 2f, .3f),
                Locked = locked
            } : default;
        }
    }
}
