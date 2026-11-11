using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    public interface IBossMover
    {
        Vector2 Position { get; }
        bool IsGrounded { get; }
        bool IsForcedMoving { get; }
        bool IsBlocked { get; }
        bool HasArrived { get; }
        void MoveTo(float x, float speed);
        void Stop();
        bool IsAirborne { get; }
        // Airborne: the body is kinematic and the pattern places it directly; ground movement is off until EndAirborne.
        void BeginAirborne();
        void SetAirbornePosition(Vector2 position);
        void EndAirborne();
    }

    // Patterns and the brain move the boss only through this seam, so another body type can replace the ground mover.
    public sealed class GroundBossMover : IBossMover
    {
        private readonly GroundMovement2D ground;
        private Rigidbody2D airborneBody;
        private RigidbodyType2D previousBodyType;

        public GroundBossMover(GroundMovement2D ground)
        {
            this.ground = ground != null ? ground : throw new System.ArgumentNullException(nameof(ground));
        }

        public Vector2 Position => ground.Position;
        public bool IsGrounded => ground.IsGrounded;
        public bool IsForcedMoving => ground.IsForcedMoving;
        public bool IsBlocked => ground.IsBlocked;
        public bool HasArrived => ground.HasArrived;
        public void MoveTo(float x, float speed) => ground.MoveTo(x, speed);
        public void Stop() => ground.Stop();
        public bool IsAirborne => airborneBody != null;

        public void BeginAirborne()
        {
            if (airborneBody != null) return;
            airborneBody = ground.GetComponent<Rigidbody2D>();
            // Disabling first clears movement commands (GroundMovement2D.OnDisable) before the body stops responding to them.
            ground.enabled = false;
            previousBodyType = airborneBody.bodyType;
            airborneBody.bodyType = RigidbodyType2D.Kinematic;
            airborneBody.linearVelocity = Vector2.zero;
            airborneBody.angularVelocity = 0f;
        }

        public void SetAirbornePosition(Vector2 position) { if (airborneBody != null) airborneBody.position = position; }

        public void EndAirborne()
        {
            if (airborneBody == null) return;
            var body = airborneBody; airborneBody = null;
            // The object can be destroyed while airborne (stage teardown).
            if (body == null || ground == null) return;
            body.bodyType = previousBodyType;
            body.linearVelocity = Vector2.zero;
            ground.enabled = true;
        }
    }
}
