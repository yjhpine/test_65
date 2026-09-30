using UnityEngine;
namespace ActionPlatformer.Units.Features
{
    public interface IForcedMovementReceiver
    {
        bool IsForcedMoving { get; }
        Vector2 Velocity { get; }
        void ApplyForcedMovement(Vector2 velocity, float duration);
        void ApplyKnockback(float horizontalSpeed, float deceleration, float controlLockDuration);
        void CancelForcedMovement();
    }
}
