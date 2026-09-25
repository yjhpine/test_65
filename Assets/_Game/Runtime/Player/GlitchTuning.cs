using UnityEngine;

namespace ActionPlatformer.Player
{
    [CreateAssetMenu(menuName = "Action Platformer/Glitch Tuning")]
    public sealed class GlitchTuning : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float maxDistance = 6f;
        [SerializeField, Min(0f)] private float aimAssistRadius = 0.75f;
        [SerializeField, Min(0f)] private float centerRadius = 0.1f;
        [SerializeField, Min(0.01f)] private float clearance = 0.15f;
        [SerializeField, Min(0f)] private float cooldown;
        [SerializeField] private bool undergroundEnabled = true;
        [SerializeField, Min(0.01f)] private float undergroundDepth = 0.6f;
        [SerializeField, Min(0.01f)] private float undergroundDuration = 2f;
        [SerializeField, Min(0f)] private float undergroundMoveSpeed = 3f;
        [SerializeField] private LayerMask targetMask = 1 << 2;
        [SerializeField] private LayerMask obstacleMask = 1;
        public float MaxDistance => maxDistance;
        public float AimAssistRadius => aimAssistRadius;
        public float CenterRadius => centerRadius;
        public float Clearance => clearance;
        public float Cooldown => Mathf.Max(0f, cooldown);
        public bool UndergroundEnabled => undergroundEnabled;
        public float UndergroundDepth => undergroundDepth;
        public float UndergroundDuration => undergroundDuration;
        public float UndergroundMoveSpeed => undergroundMoveSpeed;
        public LayerMask TargetMask => targetMask;
        public LayerMask ObstacleMask => obstacleMask;
        private void OnValidate() => cooldown = Mathf.Max(0f, cooldown);
        public bool TryValidate() => Positive(maxDistance) && NonNegative(aimAssistRadius) &&
            NonNegative(centerRadius) && Positive(clearance) && NonNegative(Cooldown) &&
            Positive(undergroundDepth) && Positive(undergroundDuration) && NonNegative(undergroundMoveSpeed);
        private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
        private static bool NonNegative(float value) => value >= 0f && !float.IsInfinity(value);
    }
}
