using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Units.Fsm
{
    [CreateAssetMenu(menuName = "Action Platformer/Units/FSM/Patrol")]
    public sealed class PatrolFsmDefinition : FsmDefinition
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 2f;
        [SerializeField, Min(0.1f)] private float patrolDistance = 3f;
        [SerializeField, Min(0.05f)] private float waitDuration = 0.6f;

        public float MoveSpeed => moveSpeed;
        public float PatrolDistance => patrolDistance;
        public float WaitDuration => waitDuration;

        protected override IFsmState CreateInitialState(Unit owner)
        {
            if (!owner.TryGetComponent<GroundMovement2D>(out var movement))
                throw new System.InvalidOperationException("Patrol FSM requires GroundMovement2D on its unit.");
            if (!IsPositiveFinite(moveSpeed) || !IsPositiveFinite(patrolDistance) || !IsPositiveFinite(waitDuration))
                throw new System.InvalidOperationException("Patrol speed, distance and wait duration must be positive finite values.");

            // Two state types, with independent instances for the two directions.
            // Starting again always enters the wait before the rightward leg.
            var waitRight = new WaitState(movement, waitDuration);
            var walkRight = new WalkState(movement, 1f, moveSpeed, patrolDistance);
            var waitLeft = new WaitState(movement, waitDuration);
            var walkLeft = new WalkState(movement, -1f, moveSpeed, patrolDistance);
            waitRight.Next = walkRight;
            walkRight.Next = waitLeft;
            waitLeft.Next = walkLeft;
            walkLeft.Next = waitRight;
            return waitRight;
        }

        private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);

        private sealed class WaitState : IFsmState
        {
            private readonly GroundMovement2D movement;
            private readonly float duration;
            private float elapsed;
            public IFsmState Next;

            public WaitState(GroundMovement2D movement, float duration)
            {
                this.movement = movement;
                this.duration = duration;
            }

            public void Enter() { elapsed = 0f; movement.Stop(); }
            public IFsmState Tick(float deltaTime)
            {
                if (!movement.isActiveAndEnabled) return null;
                elapsed += deltaTime;
                return elapsed >= duration ? Next : null;
            }
            public void Exit() { }
        }

        private sealed class WalkState : IFsmState
        {
            private readonly GroundMovement2D movement;
            private readonly float direction;
            private readonly float speed;
            private readonly float distance;
            public IFsmState Next;

            public WalkState(GroundMovement2D movement, float direction, float speed, float distance)
            {
                this.movement = movement;
                this.direction = direction;
                this.speed = speed;
                this.distance = distance;
            }

            public void Enter() => movement.MoveTo(movement.Position.x + direction * distance, speed);
            public IFsmState Tick(float deltaTime) =>
                !movement.isActiveAndEnabled || movement.HasArrived || movement.IsBlocked ? Next : null;
            public void Exit() { if (movement != null) movement.Stop(); }
        }
    }
}
