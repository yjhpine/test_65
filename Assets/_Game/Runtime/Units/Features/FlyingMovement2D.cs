using UnityEngine;
namespace ActionPlatformer.Units.Features
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D), typeof(UnitHealth))]
    public sealed class FlyingMovement2D : MonoBehaviour, IForcedMovementReceiver
    {
        [SerializeField] private LayerMask environmentMask = 1;
        [SerializeField, Min(.01f)] private float reactionGravity = 35f;
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private Unit unit;
        private UnitHealth health;
        private Vector2 home;
        private float clock, remaining, forcedX, deceleration;
        private bool knocking, cruising;
        private float radius, amplitude, maxX, maxY;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        public bool IsForcedMoving => remaining > 0f || knocking;
        public Vector2 Velocity => body == null ? Vector2.zero : body.linearVelocity;
        public Vector2 Position => body == null ? (Vector2)transform.position : body.position;
        private void Awake()
        {
            body = GetComponent<Rigidbody2D>(); bodyCollider = GetComponent<Collider2D>();
            unit = GetComponent<Unit>(); health = GetComponent<UnitHealth>(); home = body.position;
            body.gravityScale = 0f;
        }
        public void Configure(float patrolRadius, float bobAmplitude, float horizontalSpeed, float verticalSpeed)
        { radius = patrolRadius; amplitude = bobAmplitude; maxX = horizontalSpeed; maxY = verticalSpeed; }
        public void Cruise(bool value) { cruising = value; if (!value && !IsForcedMoving) StopBody(); }
        public void StopBody() { if (body != null) body.linearVelocity = Vector2.zero; }
        private bool CanReact => isActiveAndEnabled && body != null && unit != null && unit.isActiveAndEnabled &&
            unit.Definition.AllowForcedMovement && health.CanReceiveDamage;
        public void ApplyForcedMovement(Vector2 velocity, float duration)
        {
            if (!Finite(velocity.x) || !Finite(velocity.y) || !Finite(duration) || duration <= 0f) throw new System.ArgumentOutOfRangeException(nameof(duration));
            if (!CanReact) return;
            knocking = false; remaining = duration; forcedX = velocity.x; body.linearVelocity = velocity;
        }
        public void ApplyKnockback(float speed, float braking, float lockDuration)
        {
            if (!Finite(speed) || !Finite(braking) || braking <= 0f || !Finite(lockDuration) || lockDuration < 0f)
                throw new System.ArgumentOutOfRangeException(nameof(braking));
            if (!CanReact) return;
            remaining = lockDuration; deceleration = braking; knocking = speed != 0f; forcedX = 0f;
            body.linearVelocity = new Vector2(speed, Velocity.y);
        }
        public void CancelForcedMovement() { remaining = 0f; knocking = false; forcedX = 0f; StopBody(); }
        private void FixedUpdate()
        {
            if (unit == null || !unit.isActiveAndEnabled || !health.IsAlive) { CancelForcedMovement(); return; }
            float dt = Time.fixedDeltaTime; clock += dt;
            Vector2 velocity;
            if (IsForcedMoving)
            {
                remaining = Mathf.Max(0f, remaining - dt);
                float x = knocking ? Mathf.MoveTowards(Velocity.x, 0f, deceleration * dt) : forcedX;
                velocity = new Vector2(x, Velocity.y - reactionGravity * dt);
                velocity = ClipVelocity(velocity, dt);
                if (knocking) knocking = velocity.x != 0f;
            }
            else if (cruising)
            {
                Vector2 target = home + new Vector2(Mathf.Sin(clock * .65f) * radius, Mathf.Sin(clock * 1.3f) * amplitude);
                Vector2 delta = (target - Position) * 2f;
                velocity = ClipVelocity(new Vector2(Mathf.Clamp(delta.x, -maxX, maxX), Mathf.Clamp(delta.y, -maxY, maxY)), dt);
            }
            else velocity = Vector2.zero;
            body.linearVelocity = velocity;
        }
        private Vector2 ClipVelocity(Vector2 velocity, float dt)
        {
            if (bodyCollider == null || velocity.sqrMagnitude < .00001f) return velocity;
            var filter = UnitPhysics2D.Filter(environmentMask);
            // Clip each axis so a horizontal wall does not erase a launch's vertical velocity.
            for (int axis = 0; axis < 2; axis++)
            {
                float value = axis == 0 ? velocity.x : velocity.y;
                if (value == 0f) continue;
                Vector2 direction = axis == 0 ? Vector2.right * Mathf.Sign(value) : Vector2.up * Mathf.Sign(value);
                int count = bodyCollider.Cast(direction, filter, hits, Mathf.Abs(value) * dt + .02f);
                for (int i = 0; i < count; i++)
                    if (Vector2.Dot(hits[i].normal, direction) < -.5f)
                    { if (axis == 0) velocity.x = 0f; else velocity.y = 0f; break; }
            }
            return velocity;
        }
        private void OnDisable() { cruising = false; CancelForcedMovement(); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
