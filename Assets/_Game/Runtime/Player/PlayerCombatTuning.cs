using UnityEngine;

namespace ActionPlatformer.Player
{
    [CreateAssetMenu(menuName = "Action Platformer/Player Combat Tuning")]
    public sealed class PlayerCombatTuning : ScriptableObject
    {
        [Header("Optional links (applied when the player initializes)")]
        [SerializeField] private bool enableCombo = true;
        [SerializeField] private bool enablePositionAttacks = true;
        [SerializeField] private bool enableHitReactions = true;
        [SerializeField, Min(1)] private int comboLength = 3;
        [SerializeField] private bool retainComboOnTargetChange = true;
        [Header("Attack timing and shape")]
        [SerializeField, Min(0f)] private float windup = 0.12f;
        [SerializeField, Min(0.01f)] private float activeDuration = 0.06f;
        [SerializeField, Min(0f)] private float recovery = 0.18f;
        [SerializeField, Min(0.01f)] private float comboWindow = 1f;
        [SerializeField, Min(0.01f)] private float reach = 1.2f;
        [SerializeField, Min(0.01f)] private float width = 1.6f;
        [Header("Light hit knockback (non-finisher side attacks)")]
        [SerializeField, Min(0f)] private float lightKnockbackSpeed = 3f;
        [SerializeField, Min(0.01f)] private float lightKnockbackDeceleration = 30f;
        [Header("Finisher knockback")]
        [Tooltip("Initial horizontal speed on a combo finisher hit.")]
        [SerializeField, Min(0f)] private float knockbackSpeed = 12f;
        [Tooltip("Horizontal speed lost per second during knockback. Higher values stop sooner.")]
        [SerializeField, Min(0.01f)] private float knockbackDeceleration = 18f;
        [SerializeField, Min(0f)] private float launchSpeed = 10f;
        [Header("Air slam")]
        [SerializeField, Min(0f)] private float slamHoverDuration = 0.5f;
        [SerializeField, Min(0.01f)] private float slamSpeed = 28f;
        [SerializeField, Min(0.01f)] private float slamKnockdownSpeed = 36f;
        [SerializeField, Min(0.01f)] private float shockwaveRadius = 2f;
        [SerializeField, Min(0.01f)] private float shockwaveDuration = 0.22f;
        [SerializeField, Min(0.01f)] private float slamTimeout = 3f;
        [SerializeField, Min(0f)] private float emergenceSpeed = 10f;
        [SerializeField, Min(0.01f)] private float forcedDuration = 0.35f;
        [SerializeField, Min(0f)] private float hitStun = 0.25f;
        [Header("Attack input buffer")]
        [SerializeField, Min(0f)] private float attackBufferTime = 0.1f;
        [Header("Impact feedback (0 disables each effect)")]
        [SerializeField, Min(0f)] private float hitStopDuration = 0.035f;
        [SerializeField, Min(0f)] private float heavyHitStopDuration = 0.055f;
        [SerializeField, Min(0f)] private float slamHitStopDuration = 0.08f;
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.025f;
        [SerializeField, Min(0f)] private float heavyShakeAmplitude = 0.075f;
        [SerializeField, Min(0f)] private float slamShakeAmplitude = 0.14f;
        [SerializeField, Min(0f)] private float shakeDuration = 0.14f;
        [SerializeField, Min(0.01f)] private float shakeFrequency = 22f;
        [SerializeField] private LayerMask targetMask = 1 << 2;
        [SerializeField] private LayerMask obstacleMask = 1;
        public float Windup => windup;
        public bool EnableCombo => enableCombo;
        public bool EnablePositionAttacks => enablePositionAttacks;
        public bool EnableHitReactions => enableHitReactions;
        public int ComboLength => comboLength;
        public bool RetainComboOnTargetChange => retainComboOnTargetChange;
        public float ActiveDuration => activeDuration;
        public float Recovery => recovery;
        public float ComboWindow => comboWindow;
        public float Reach => reach;
        public float Width => width;
        public float LightKnockbackSpeed => lightKnockbackSpeed;
        public float LightKnockbackDeceleration => lightKnockbackDeceleration;
        public float KnockbackSpeed => knockbackSpeed;
        public float KnockbackDeceleration => knockbackDeceleration;
        public float LaunchSpeed => launchSpeed;
        public float SlamSpeed => slamSpeed;
        public float SlamKnockdownSpeed => slamKnockdownSpeed;
        public float SlamHoverDuration => slamHoverDuration;
        public float ShockwaveRadius => shockwaveRadius;
        public float ShockwaveDuration => shockwaveDuration;
        public float SlamTimeout => slamTimeout;
        public float EmergenceSpeed => emergenceSpeed;
        public float ForcedDuration => forcedDuration;
        public float HitStun => hitStun;
        public float AttackBufferTime => attackBufferTime;
        public float HitStopDuration => hitStopDuration;
        public float HeavyHitStopDuration => heavyHitStopDuration;
        public float SlamHitStopDuration => slamHitStopDuration;
        public float ShakeAmplitude => shakeAmplitude;
        public float HeavyShakeAmplitude => heavyShakeAmplitude;
        public float SlamShakeAmplitude => slamShakeAmplitude;
        public float ShakeDuration => shakeDuration;
        public float ShakeFrequency => shakeFrequency;
        public LayerMask TargetMask => targetMask;
        public LayerMask ObstacleMask => obstacleMask;
        public bool TryValidate() => (!enableCombo || comboLength >= 1) && NonNegative(windup) && Positive(activeDuration) && NonNegative(recovery) &&
            Positive(comboWindow) && Positive(reach) && Positive(width) && NonNegative(lightKnockbackSpeed) && Positive(lightKnockbackDeceleration) &&
            NonNegative(knockbackSpeed) && Positive(knockbackDeceleration) &&
            NonNegative(launchSpeed) && NonNegative(slamHoverDuration) && Positive(slamSpeed) && Positive(slamKnockdownSpeed) && Positive(shockwaveRadius) &&
            Positive(shockwaveDuration) && Positive(slamTimeout) && NonNegative(emergenceSpeed) &&
            Positive(forcedDuration) && NonNegative(hitStun) && NonNegative(attackBufferTime) &&
            NonNegative(hitStopDuration) && NonNegative(heavyHitStopDuration) && NonNegative(slamHitStopDuration) &&
            NonNegative(shakeAmplitude) && NonNegative(heavyShakeAmplitude) && NonNegative(slamShakeAmplitude) &&
            NonNegative(shakeDuration) && Positive(shakeFrequency);
        private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
        private static bool NonNegative(float value) => value >= 0f && !float.IsInfinity(value);
    }
}
