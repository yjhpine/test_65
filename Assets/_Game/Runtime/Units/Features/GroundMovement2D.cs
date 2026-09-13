using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class GroundMovement2D : MonoBehaviour
    {
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private LayerMask environmentMask = 1;
        [SerializeField] private bool stopAtLedges = true;
        [SerializeField, Min(0.001f)] private float arrivalTolerance = 0.02f;
        [SerializeField, Min(0.11f)] private float groundProbeDistance = 0.3f;
        private const float Skin = 0.05f;
        private const float GroundProbeHeight = 0.1f;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        private Rigidbody2D body;
        private ContactFilter2D environmentFilter;
        private float targetX;
        private float speed;
        private bool hasDestination;

        public Vector2 Position => body == null ? (Vector2)transform.position : body.position;
        public Vector2 Velocity => body == null ? Vector2.zero : body.linearVelocity;
        public bool IsBlocked { get; private set; }
        public bool HasArrived => hasDestination && Mathf.Abs(targetX - Position.x) <= arrivalTolerance;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            if (bodyCollider == null || bodyCollider.isTrigger || bodyCollider.attachedRigidbody != body)
            {
                Debug.LogError("Ground movement needs a non-trigger Collider2D attached to its Rigidbody2D.", this);
                enabled = false;
                return;
            }
            if (!IsFinite(arrivalTolerance) || arrivalTolerance <= 0f ||
                !IsFinite(groundProbeDistance) || groundProbeDistance <= GroundProbeHeight)
            {
                Debug.LogError("Ground movement needs a positive arrival tolerance and a ground probe distance greater than 0.1.", this);
                enabled = false;
                return;
            }
            environmentFilter = new ContactFilter2D { useLayerMask = true, layerMask = environmentMask, useTriggers = false };
        }

        public void MoveTo(float destinationX, float moveSpeed)
        {
            if (!IsFinite(destinationX)) throw new System.ArgumentOutOfRangeException(nameof(destinationX));
            if (!IsFinite(moveSpeed) || moveSpeed <= 0f) throw new System.ArgumentOutOfRangeException(nameof(moveSpeed));
            if (!isActiveAndEnabled) return;
            targetX = destinationX;
            speed = moveSpeed;
            IsBlocked = false;
            hasDestination = true;
        }

        public void Stop()
        {
            hasDestination = false;
            IsBlocked = false;
            SetHorizontalVelocity(0f);
        }

        private void FixedUpdate()
        {
            if (!hasDestination || HasArrived)
            {
                SetHorizontalVelocity(0f);
                return;
            }

            float remaining = targetX - body.position.x;
            float direction = Mathf.Sign(remaining);
            float step = Mathf.Min(speed * Time.fixedDeltaTime, Mathf.Abs(remaining));
            IsBlocked = IsPathBlocked(direction, step);
            SetHorizontalVelocity(IsBlocked ? 0f : direction * step / Time.fixedDeltaTime);
        }

        private bool IsPathBlocked(float direction, float step)
        {
            int count = bodyCollider.Cast(new Vector2(direction, 0f), environmentFilter, hits, step + Skin);
            for (int i = 0; i < count; i++)
                if (hits[i].normal.x * direction < -0.5f) return true;

            if (!stopAtLedges) return false;

            // Report unsafe steps; the caller decides whether to wait, turn, or choose another destination.
            Bounds bounds = bodyCollider.bounds;
            var foot = new Vector2(bounds.center.x + direction * (bounds.extents.x + step + Skin), bounds.min.y + GroundProbeHeight);
            count = Physics2D.Raycast(foot, Vector2.down, environmentFilter, hits, groundProbeDistance);
            for (int i = 0; i < count; i++)
                if (hits[i].normal.y > 0.65f) return false;
            return true;
        }

        private void SetHorizontalVelocity(float horizontal)
        {
            if (body != null) body.linearVelocity = new Vector2(horizontal, body.linearVelocity.y);
        }

        private void OnDisable() => Stop();

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
