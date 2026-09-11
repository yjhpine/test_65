using UnityEngine;

namespace ActionPlatformer.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class CharacterMotor2D : MonoBehaviour
    {
        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private LayerMask groundMask = 1;
        private Rigidbody2D body;
        private CapsuleCollider2D bodyCollider;
        private ContactFilter2D groundFilter;
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];

        public PlayerTuning Tuning => tuning;
        public Vector2 Velocity => body == null ? Vector2.zero : body.linearVelocity;
        public Vector2 Position => body == null ? (Vector2)transform.position : body.position;
        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<CapsuleCollider2D>();
            groundFilter = new ContactFilter2D { useLayerMask = true, layerMask = groundMask, useTriggers = false };
            if (tuning == null) { Debug.LogError("Player tuning is missing.", this); enabled = false; }
        }

        public void RefreshContacts()
        {
            IsGrounded = false;
            if (Velocity.y > 0.2f) return;
            int count = bodyCollider.Cast(Vector2.down, groundFilter, groundHits, 0.075f);
            for (int i = 0; i < count; i++)
                if (groundHits[i].normal.y > 0.65f) { IsGrounded = true; break; }
        }

        public void Jump()
        {
            body.linearVelocity = new Vector2(Velocity.x, tuning.JumpSpeed);
            IsGrounded = false;
        }

        public void CutJump()
        {
            if (Velocity.y > 0f)
                body.linearVelocity = new Vector2(Velocity.x, Velocity.y * tuning.JumpCutMultiplier);
        }

        public void Simulate(float horizontal, bool jumpHeld, float deltaTime)
        {
            Vector2 velocity = Velocity;
            velocity.x = MovementMath.HorizontalSpeed(velocity.x, horizontal, IsGrounded, tuning, deltaTime);
            float gravity = velocity.y > 0f ? tuning.RiseGravity : tuning.FallGravity;
            if (velocity.y > 0f && !jumpHeld) gravity *= 1.6f;
            velocity.y = IsGrounded ? -2f : Mathf.Max(velocity.y - gravity * deltaTime, -tuning.MaxFallSpeed);
            body.linearVelocity = velocity;
        }

        public void Teleport(Vector2 position)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
            IsGrounded = false;
        }
    }
}
