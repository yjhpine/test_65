using UnityEngine;

namespace ActionPlatformer.Player
{
    [CreateAssetMenu(menuName = "Action Platformer/Player Tuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        [Header("Horizontal movement")]
        [SerializeField, Min(0.1f)] private float runSpeed = 10.5f;
        [SerializeField, Min(1f)] private float groundAcceleration = 125f;
        [SerializeField, Min(1f)] private float groundBraking = 110f;
        [SerializeField, Min(1f)] private float airAcceleration = 45f;
        [Header("Jump")]
        [SerializeField, Min(0.1f)] private float jumpHeight = 3.2f;
        [SerializeField, Min(1f)] private float riseGravity = 32f;
        [SerializeField, Min(1f)] private float fallGravity = 55f;
        [SerializeField, Min(1f)] private float maxFallSpeed = 28f;
        [SerializeField, Range(0.1f, 1f)] private float jumpCutMultiplier = 0.45f;
        [SerializeField, Range(0f, 0.3f)] private float coyoteTime = 0.1f;
        [SerializeField, Range(0f, 0.3f)] private float inputBufferTime = 0.13f;

        public float RunSpeed => runSpeed;
        public float GroundAcceleration => groundAcceleration;
        public float GroundBraking => groundBraking;
        public float AirAcceleration => airAcceleration;
        public float JumpSpeed => MovementMath.JumpSpeed(jumpHeight, riseGravity);
        public float RiseGravity => riseGravity;
        public float FallGravity => fallGravity;
        public float MaxFallSpeed => maxFallSpeed;
        public float JumpCutMultiplier => jumpCutMultiplier;
        public float CoyoteTime => coyoteTime;
        public float InputBufferTime => inputBufferTime;
    }
}
