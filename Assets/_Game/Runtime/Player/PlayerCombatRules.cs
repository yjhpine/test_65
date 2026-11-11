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

        // Position-based attacks consider a target only within Reach of the closer body edge on either axis.
        public static bool WithinReach(Bounds body, Bounds target, PlayerCombatTuning tuning) =>
            Vector2.Distance(body.center, target.center) <= tuning.Reach +
                Mathf.Max(body.extents.x + target.extents.x, body.extents.y + target.extents.y);

        public static PlayerAttackSelection Side(Bounds body, float facing, PlayerCombatTuning tuning)
        {
            float direction = facing < 0f ? -1f : 1f;
            return new PlayerAttackSelection(PlayerAttack.Side, direction,
                new Vector2(direction * (body.extents.x + tuning.Reach * 0.5f), 0f), new Vector2(tuning.Reach, tuning.Width));
        }
    }

    public interface IPlayerAttackSelector
    {
        PlayerAttackSelection Select(Bounds body, UnitHealth target, float facing, bool emergence, bool grounded);
    }

    public sealed class PositionAttackSelector : IPlayerAttackSelector
    {
        private readonly PlayerCombatTuning tuning;
        public PositionAttackSelector(PlayerCombatTuning tuning) { this.tuning = tuning; }

        public PlayerAttackSelection Select(Bounds body, UnitHealth target, float facing, bool emergence, bool grounded)
        {
            var basic = grounded ? PlayerAttack.Side : PlayerAttack.Air;
            // Emergence is a grounded follow-up; an airborne request uses the ordinary position rules.
            bool emerging = emergence && grounded;
            var kind = emerging ? PlayerAttack.Emergence : basic;
            var targetBody = UnitPhysics2D.Body(target);
            if (UnitPhysics2D.IsAvailable(target) && targetBody != null &&
                PlayerAttackSelection.WithinReach(body, targetBody.bounds, tuning))
            {
                Vector2 delta = targetBody.bounds.center - body.center;
                // Centers alone classify tall/overlapping enemies as being above or below the player.
                // Lift requires the target's feet above the player's head. A slam requires the player's center above
                // the target's head, so a short fall after reaching the top (waiting out recovery) still slams.
                if (!emerging && targetBody.bounds.min.y >= body.max.y - 0.02f && delta.y > Mathf.Abs(delta.x))
                    kind = PlayerAttack.Lift;
                else if (!emerging && !grounded && body.center.y >= targetBody.bounds.max.y && -delta.y > Mathf.Abs(delta.x))
                    kind = PlayerAttack.Slam;
                // Basic swings keep the direction chosen when the attack starts; vertical attacks face their target.
                if (kind != basic && Mathf.Abs(delta.x) > 0.01f) facing = Mathf.Sign(delta.x);
            }
            float direction = facing < 0f ? -1f : 1f;
            var forward = PlayerAttackSelection.Side(body, direction, tuning);
            if (kind == PlayerAttack.Side) return forward;
            if (kind == PlayerAttack.Air) return new PlayerAttackSelection(kind, direction, forward.HitOffset, forward.HitSize);
            var size = new Vector2(tuning.Width, tuning.Reach);
            if (kind == PlayerAttack.Emergence)
                return new PlayerAttackSelection(kind, direction,
                    new Vector2(0f, -body.extents.y + tuning.Reach * 0.5f), size, tuning.EmergenceSpeed);
            // A lift raises the player with its target on a valid hit; a slam strikes the column below its descent.
            bool lift = kind == PlayerAttack.Lift;
            return new PlayerAttackSelection(kind, direction,
                new Vector2(0f, (lift ? 1f : -1f) * (body.extents.y + tuning.Reach * 0.5f)), size, lift ? tuning.LaunchSpeed : 0f);
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
            if (!target.Owner.Definition.AllowForcedMovement || !target.TryGetComponent<IForcedMovementReceiver>(out var movement)) return;
            if (attack.Kind == PlayerAttack.Side || attack.Kind == PlayerAttack.Air)
            {
                float speed = combo.IsFinisher ? tuning.KnockbackSpeed : tuning.LightKnockbackSpeed;
                float deceleration = combo.IsFinisher ? tuning.KnockbackDeceleration : tuning.LightKnockbackDeceleration;
                movement.ApplyKnockback(attack.Facing * speed, deceleration, tuning.ForcedDuration);
                return;
            }
            float launch = tuning.LaunchSpeed * (attack.Kind == PlayerAttack.Lift && combo.IsFinisher ? tuning.FinisherLaunchMultiplier : 1f);
            var velocity = new Vector2(0f, attack.Kind == PlayerAttack.Slam ? -tuning.SlamKnockdownSpeed : launch);
            movement.ApplyForcedMovement(velocity, tuning.ForcedDuration);
        }
    }
}
