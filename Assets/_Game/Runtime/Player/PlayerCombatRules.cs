using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Player
{
    // Rules are stateless. PlayerCombat owns the selected step, target history and all timers.
    public readonly struct PlayerComboStep
    {
        public readonly int Strike;
        public readonly int NextStrike;
        public readonly bool IsFinisher;
        public PlayerComboStep(int strike, int nextStrike, bool isFinisher)
        { Strike = strike; NextStrike = nextStrike; IsFinisher = isFinisher; }
        public static PlayerComboStep Basic => new PlayerComboStep(1, 1, false);
    }

    public interface IPlayerComboRule
    {
        PlayerComboStep Select(int nextStrike, bool expired, bool targetChanged);
    }

    public sealed class SequentialComboRule : IPlayerComboRule
    {
        private readonly int length;
        private readonly bool retainOnTargetChange;
        public SequentialComboRule(int length = 3, bool retainOnTargetChange = true)
        {
            if (length < 1) throw new System.ArgumentOutOfRangeException(nameof(length));
            this.length = length; this.retainOnTargetChange = retainOnTargetChange;
        }
        public PlayerComboStep Select(int nextStrike, bool expired, bool targetChanged)
        {
            int strike = expired || (!retainOnTargetChange && targetChanged) ? 1 : Mathf.Clamp(nextStrike, 1, length);
            bool finisher = strike == length;
            return new PlayerComboStep(strike, finisher ? 1 : strike + 1, finisher);
        }
    }

    public readonly struct PlayerAttackSelection
    {
        public readonly PlayerAttack Kind;
        public readonly float Facing;
        public readonly Vector2 HitOffset;
        public readonly Vector2 HitSize;
        public readonly float LaunchSpeed;
        public PlayerAttackSelection(PlayerAttack kind, float facing, Vector2 hitOffset, Vector2 hitSize, float launchSpeed = 0f)
        { Kind = kind; Facing = facing < 0f ? -1f : 1f; HitOffset = hitOffset; HitSize = hitSize; LaunchSpeed = launchSpeed; }

        public static PlayerAttackSelection Side(Bounds body, float facing, PlayerCombatTuning tuning)
        {
            float direction = facing < 0f ? -1f : 1f;
            return new PlayerAttackSelection(PlayerAttack.Side, direction,
                new Vector2(direction * (body.extents.x + tuning.Reach * 0.5f), 0f), new Vector2(tuning.Reach, tuning.Width));
        }
    }

    public interface IPlayerAttackSelector
    {
        PlayerAttackSelection Select(Bounds body, UnitHealth target, float facing, bool emergence);
    }

    public sealed class PositionAttackSelector : IPlayerAttackSelector
    {
        private readonly PlayerCombatTuning tuning;
        public PositionAttackSelector(PlayerCombatTuning tuning) { this.tuning = tuning; }

        public PlayerAttackSelection Select(Bounds body, UnitHealth target, float facing, bool emergence)
        {
            var kind = emergence ? PlayerAttack.Emergence : PlayerAttack.Side;
            var targetBody = UnitPhysics2D.Body(target);
            if (UnitPhysics2D.IsAvailable(target) && targetBody != null &&
                Vector2.Distance(body.center, targetBody.bounds.center) <= tuning.Reach +
                    Mathf.Max(body.extents.x + targetBody.bounds.extents.x, body.extents.y + targetBody.bounds.extents.y))
            {
                Vector2 delta = targetBody.bounds.center - body.center;
                if (Mathf.Abs(delta.x) > 0.01f) facing = Mathf.Sign(delta.x);
                // Centers alone classify tall/overlapping enemies as being above the player.
                // Lift requires the target's feet to be above the player's body.
                if (!emergence && targetBody.bounds.min.y >= body.max.y - 0.02f &&
                    delta.y > Mathf.Abs(delta.x)) kind = PlayerAttack.Lift;
            }
            float direction = facing < 0f ? -1f : 1f;
            if (kind == PlayerAttack.Side) return PlayerAttackSelection.Side(body, direction, tuning);
            var size = new Vector2(tuning.Width, tuning.Reach);
            if (kind == PlayerAttack.Emergence)
                return new PlayerAttackSelection(kind, direction,
                    new Vector2(0f, -body.extents.y + tuning.Reach * 0.5f), size, tuning.EmergenceSpeed);
            return new PlayerAttackSelection(kind, direction,
                new Vector2(0f, (kind == PlayerAttack.Lift ? 1f : -1f) * (body.extents.y + tuning.Reach * 0.5f)), size);
        }
    }

    public interface IPlayerHitReaction
    {
        void Apply(UnitHealth target, PlayerAttackSelection attack, PlayerComboStep combo);
    }

    public sealed class GroundHitReaction : IPlayerHitReaction
    {
        private readonly PlayerCombatTuning tuning;
        public GroundHitReaction(PlayerCombatTuning tuning) { this.tuning = tuning; }

        public void Apply(UnitHealth target, PlayerAttackSelection attack, PlayerComboStep combo)
        {
            if (attack.Kind == PlayerAttack.Shockwave) return;
            if (!target.Owner.Definition.AllowForcedMovement || !target.TryGetComponent<GroundMovement2D>(out var movement)) return;
            if (attack.Kind == PlayerAttack.Side)
            {
                float speed = combo.IsFinisher ? tuning.KnockbackSpeed : tuning.LightKnockbackSpeed;
                float deceleration = combo.IsFinisher ? tuning.KnockbackDeceleration : tuning.LightKnockbackDeceleration;
                movement.ApplyKnockback(attack.Facing * speed, deceleration, tuning.ForcedDuration);
                return;
            }
            var velocity = new Vector2(0f, attack.Kind == PlayerAttack.Slam ? -tuning.SlamKnockdownSpeed : tuning.LaunchSpeed);
            movement.ApplyForcedMovement(velocity, tuning.ForcedDuration);
        }
    }
}
