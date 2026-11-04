using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    // Fills the blackboard from the boss's own body and the player's read-only contracts. Never touches PlayerUnit directly.
    public static class BossSenses
    {
        private const float UndergroundSightLift = .5f;

        public static void Refresh(BossBlackboard board, BossContext context, float deltaTime)
        {
            var combat = context.Combat;
            var settings = context.Settings;
            // A target is kept while alive and active; recognition (range, sight) is judged separately so brief invulnerability or an
            // underground player never drops it.
            if (!UnitPhysics2D.IsAvailable(board.Target)) board.Target = combat.FindPlayer(settings.RecognitionRange, settings.RecognitionHeight);
            BindTarget(board);
            board.BossPosition = combat.Center;
            board.BossFacing = context.Facing;
            int max = context.Health.MaxHealth;
            board.HealthRatio = max > 0 ? Mathf.Clamp01((float)context.Health.CurrentHealth / max) : 0f;

            var target = board.Target;
            if (target == null) { board.TargetInSight = false; return; }
            var action = board.ActionState;
            Vector2 sightPoint;
            bool underground = action != null && action.IsUnderground;
            if (underground) { sightPoint = action.GroundMarkerPosition + new Vector2(0, UndergroundSightLift); board.PlayerPosition = action.GroundMarkerPosition; }
            else
            {
                var body = UnitPhysics2D.Body(target);
                Vector2 center = body != null ? (Vector2)body.bounds.center : board.PlayerPosition;
                board.PlayerPosition = sightPoint = center;
            }
            board.TargetInSight = combat.HasSight(board.BossPosition, sightPoint);

            board.PlayerGrounded = board.MotionState?.IsGrounded;
            board.PlayerUnderground = action?.IsUnderground;
            board.PlayerAttacking = action == null ? (bool?)null : action.AttackPhase != PlayerAttackPhase.Ready;
            var camera = board.ViewSource?.View;
            if (camera != null)
            {
                Vector3 v = camera.WorldToViewportPoint(board.BossPosition);
                board.PlayerSeesBoss = v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
            }
            else board.PlayerSeesBoss = null;

            var reposition = board.RepositionState;
            if (reposition != null)
            {
                uint version = reposition.RepositionVersion;
                if (!board.HasSeenReposition) { board.HasSeenReposition = true; board.SeenRepositionVersion = version; }
                else if (version != board.SeenRepositionVersion) { board.SeenRepositionVersion = version; board.SinceGlitch = 0f; }
                else board.SinceGlitch += deltaTime;
            }
        }

        // Caches the contract references and resets per-target memory whenever the target changes.
        private static void BindTarget(BossBlackboard board)
        {
            var target = board.Target;
            if (target == board.SensedTarget) return;
            board.SensedTarget = target;
            board.HasSeenReposition = false; board.SinceGlitch = float.PositiveInfinity;
            board.ActionState = target == null ? null : target.GetComponent<IPlayerActionState>();
            board.MotionState = target == null ? null : target.GetComponent<ICharacterMotionState>();
            board.RepositionState = target == null ? null : target.GetComponent<IUnitRepositionState>();
            board.ViewSource = target == null ? null : target.GetComponent<IUnitViewSource>();
            if (target == null) { board.PlayerGrounded = board.PlayerUnderground = board.PlayerAttacking = board.PlayerSeesBoss = null; }
        }

        public static bool IsRecognized(BossBlackboard board, BossFsmDefinition settings) =>
            UnitPhysics2D.IsAvailable(board.Target) && board.TargetInSight &&
            Mathf.Abs(board.DeltaX) <= settings.RecognitionRange && Mathf.Abs(board.DeltaY) <= settings.RecognitionHeight &&
            board.PlayerSeesBoss != false;
    }
}
