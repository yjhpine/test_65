using UnityEngine;

namespace ActionPlatformer.Player
{
    public interface ICharacterMotionState
    {
        Vector2 Velocity { get; }
        bool IsGrounded { get; }
    }

    public sealed class CharacterMotor2D : ICharacterMotionState
    {
        private readonly PlayerTuning tuning;
        private readonly Rigidbody2D body;
        private readonly Collider2D bodyCollider;
        private readonly ContactFilter2D groundFilter;
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        private readonly RaycastHit2D[] probeHits = new RaycastHit2D[4];
        private RigidbodyType2D savedBodyType;
        private bool savedSimulated;
        public bool IsTransferring { get; private set; }
        public bool IsHeld { get; private set; }

        public Vector2 Velocity => body.linearVelocity;
        public Vector2 Position => body.position;
        public bool IsGrounded { get; private set; }

        public CharacterMotor2D(Rigidbody2D body, Collider2D bodyCollider, PlayerTuning tuning, LayerMask groundMask)
        {
            if (body == null) throw new System.ArgumentNullException(nameof(body));
            if (bodyCollider == null) throw new System.ArgumentNullException(nameof(bodyCollider));
            if (tuning == null) throw new System.ArgumentNullException(nameof(tuning));
            if (bodyCollider.attachedRigidbody != body)
                throw new System.ArgumentException("The collider must belong to the supplied Rigidbody2D.", nameof(bodyCollider));
            this.body = body;
            this.bodyCollider = bodyCollider;
            this.tuning = tuning;
            groundFilter = new ContactFilter2D { useLayerMask = true, layerMask = groundMask, useTriggers = false };
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
            if (IsHeld) return;
            Step(MovementMath.HorizontalSpeed(Velocity.x, horizontal, IsGrounded, tuning, deltaTime), jumpHeld, deltaTime);
        }

        // Attack recoil: the given horizontal speed replaces locomotion for this step; gravity and grounding are unchanged.
        public void SimulateRecoil(float horizontalVelocity, bool jumpHeld, float deltaTime)
        {
            if (IsHeld) return;
            Step(horizontalVelocity, jumpHeld, deltaTime);
        }

        private void Step(float horizontalVelocity, bool jumpHeld, float deltaTime)
        {
            Vector2 velocity = new Vector2(horizontalVelocity, Velocity.y);
            float gravity = velocity.y > 0f ? tuning.RiseGravity : tuning.FallGravity;
            if (velocity.y > 0f && !jumpHeld) gravity *= 1.6f;
            velocity.y = IsGrounded ? -2f : Mathf.Max(velocity.y - gravity * deltaTime, -tuning.MaxFallSpeed);
            body.linearVelocity = velocity;
        }

        // Whether floor stays under the body a little past its center after moving `distance` along `direction`.
        public bool HasGroundAhead(float direction, float distance)
        {
            var bounds = bodyCollider.bounds;
            var origin = new Vector2(bounds.center.x + Mathf.Sign(direction) * (Mathf.Abs(distance) + .1f), bounds.min.y + .1f);
            int count = Physics2D.Raycast(origin, Vector2.down, groundFilter, probeHits, .25f);
            for (int i = 0; i < count; i++)
                if (probeHits[i].normal.y > .65f) return true;
            return false;
        }

        public void Teleport(Vector2 position)
        {
            Relocate(position, Vector2.zero);
        }

        public void Relocate(Vector2 position, Vector2 velocity)
        {
            body.position = position;
            // Unsimulated bodies do not publish their physics pose to the Transform/camera.
            if (IsTransferring) body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
            body.linearVelocity = velocity;
            IsGrounded = false;
        }

        public void StopHorizontal()
        {
            body.linearVelocity = new Vector2(0f, Velocity.y);
        }

        public void SetVerticalVelocity(float velocity)
        {
            body.linearVelocity = new Vector2(Velocity.x, velocity);
            IsGrounded = false;
        }

        public void Hover()
        {
            body.linearVelocity = Vector2.zero;
            IsGrounded = false;
        }

        // Sweep the entire body through this step so a fast descent cannot skip a thin platform.
        public bool SimulateSlam(float speed, float deltaTime, out Vector2 impact)
        {
            impact = default;
            if (IsHeld) return false;
            float travel = speed * deltaTime;
            int count = bodyCollider.Cast(Vector2.down, groundFilter, groundHits, travel + 0.02f);
            int nearest = -1;
            for (int i = 0; i < count; i++)
                if (groundHits[i].normal.y > 0.65f &&
                    (nearest < 0 || groundHits[i].distance < groundHits[nearest].distance)) nearest = i;
            if (nearest >= 0)
            {
                var floor = groundHits[nearest];
                Relocate(Position + Vector2.down * Mathf.Max(0f, floor.distance - 0.01f), Vector2.zero);
                IsGrounded = true;
                impact = floor.point;
                return true;
            }
            body.linearVelocity = Vector2.down * speed;
            IsGrounded = false;
            return false;
        }

        public void StopSlam()
        {
            body.linearVelocity = Vector2.zero;
            RefreshContacts();
        }

        public void Hold()
        {
            if (IsHeld) return;
            savedBodyType = body.bodyType;
            body.linearVelocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;
            IsHeld = true;
            IsGrounded = false;
        }

        public void BeginTransfer()
        {
            if (IsTransferring) return;
            Hold();
            savedSimulated = body.simulated;
            body.simulated = false;
            IsTransferring = true;
        }

        public void EndTransfer()
        {
            if (!IsTransferring) return;
            Vector2 position = body.position;
            body.simulated = savedSimulated;
            IsTransferring = false;
            Release();
            body.position = position;
            body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
            Physics2D.SyncTransforms();
            RefreshContacts();
        }

        public void Release()
        {
            if (!IsHeld) return;
            body.bodyType = savedBodyType;
            body.linearVelocity = Vector2.zero;
            IsHeld = false;
            IsGrounded = false;
        }
    }
}
