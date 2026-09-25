using System.Collections.Generic;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Player
{
    public sealed class PlayerGlitch
    {
        private readonly Unit owner;
        private readonly UnitHealth health;
        private readonly CharacterMotor2D motor;
        private readonly CapsuleCollider2D body;
        private readonly GlitchTuning tuning;
        private readonly List<Collider2D> overlaps = new List<Collider2D>(32);
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
        private BoxCollider2D floor;
        private bool previousDamageEnabled;
        private double nextUseAt;
        private double undergroundEndsAt;
        public bool IsUnderground { get; private set; }
        public UnitHealth ArrivalTarget { get; private set; }
        public Vector2 UndergroundPosition { get; private set; }
        public Vector2 GroundMarkerPosition { get; private set; }
        public uint SuccessVersion { get; private set; }
        public float CooldownRemaining(double now) => tuning.Cooldown == 0f ? 0f : (float)System.Math.Max(0, nextUseAt - now);

        public PlayerGlitch(Unit owner, UnitHealth health, CharacterMotor2D motor, CapsuleCollider2D body, GlitchTuning tuning)
        {
            this.owner = owner; this.health = health; this.motor = motor; this.body = body; this.tuning = tuning;
        }

        public GlitchRequest Select(Vector2 cursor) => GlitchUtility.Select(owner, motor.Position, cursor, tuning, overlaps, hits);

        public GlitchPreview Preview(GlitchRequest request, double now) => Evaluate(request, now, out _);

        // Preview and execution share the same read-only placement checks.
        private GlitchPreview Evaluate(GlitchRequest request, double now, out BoxCollider2D support)
        {
            support = null;
            if (IsUnderground || !UnitPhysics2D.IsAvailable(health) ||
                !GlitchUtility.ValidTarget(owner, request.Target, motor.Position, tuning, hits)) return default;
            var target = UnitPhysics2D.Body(request.Target).bounds;
            Vector2 destination = GlitchUtility.Placement(body.bounds, motor.Position, target, request.Direction, tuning.Clearance);
            var preview = new GlitchPreview
            {
                Visible = true,
                Destination = destination,
                Bounds = new Bounds(destination + (Vector2)body.bounds.center - motor.Position, body.bounds.size)
            };
            bool free = GlitchUtility.SpaceFree(body, motor.Position, destination,
                tuning.ObstacleMask.value | tuning.TargetMask.value, overlaps);
            if (!free && request.Direction == GlitchDirection.Down && tuning.UndergroundEnabled &&
                !GlitchUtility.SpaceFree(body, motor.Position, destination, tuning.ObstacleMask, overlaps) &&
                GlitchUtility.TryFloor(target, tuning.ObstacleMask, hits, out support))
            {
                Vector2 marker = new Vector2(target.center.x, support.bounds.max.y);
                preview.IsUnderground = true;
                preview.Destination = marker + Vector2.down * tuning.UndergroundDepth;
                preview.Bounds = new Bounds(marker, new Vector3(0.8f, 0.12f));
                free = GlitchUtility.TryEmergence(body, motor.Position, support, preview.Destination, tuning, overlaps, out _);
            }
            preview.CanExecute = free && CooldownRemaining(now) <= 0f;
            return preview;
        }

        public bool TryExecute(GlitchRequest request, double now)
        {
            var preview = Evaluate(request, now, out var support);
            if (!preview.CanExecute) return false;
            if (!preview.IsUnderground) motor.Relocate(preview.Destination, Vector2.zero);
            else
            {
                floor = support;
                GroundMarkerPosition = preview.Bounds.center;
                UndergroundPosition = preview.Destination;
                previousDamageEnabled = health.DamageEnabled;
                health.SetDamageEnabled(false);
                motor.Hold();
                IsUnderground = true;
                undergroundEndsAt = now + tuning.UndergroundDuration;
            }
            ArrivalTarget = request.Target;
            nextUseAt = now + tuning.Cooldown;
            unchecked { SuccessVersion++; }
            return true;
        }

        public void Tick(double now)
        {
            if (tuning.Cooldown == 0f) nextUseAt = 0;
            if (IsUnderground && (now >= undergroundEndsAt || !UnitPhysics2D.IsAvailable(ArrivalTarget) || floor == null))
                CancelUnderground();
        }

        public bool TryEmerge()
        {
            if (!IsUnderground) return false;
            bool valid = GlitchUtility.ValidTarget(owner, ArrivalTarget, motor.Position, tuning, hits);
            Vector2 destination = default;
            if (valid)
            {
                var target = UnitPhysics2D.Body(ArrivalTarget).bounds;
                valid = GlitchUtility.TryFloor(target, tuning.ObstacleMask, hits, out var currentFloor) && currentFloor == floor &&
                    GlitchUtility.TryEmergence(body, motor.Position, floor, UndergroundPosition, tuning, overlaps, out destination);
            }
            EndUnderground();
            if (!valid) { ArrivalTarget = null; return false; }
            motor.Relocate(destination, Vector2.zero);
            return true;
        }

        public void CancelUnderground() { EndUnderground(); ArrivalTarget = null; }

        private void EndUnderground()
        {
            if (!IsUnderground) return;
            IsUnderground = false;
            floor = null;
            motor.Release();
            health.SetDamageEnabled(previousDamageEnabled);
        }

        public void Reset()
        {
            CancelUnderground();
            nextUseAt = 0;
        }
    }
}
