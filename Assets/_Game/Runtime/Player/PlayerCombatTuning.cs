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
        [SerializeField, Min(0f)] private float knockbackSpeed = 6f;
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
        public float KnockbackSpeed => knockbackSpeed;
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
        public LayerMask TargetMask => targetMask;
        public LayerMask ObstacleMask => obstacleMask;
        public bool TryValidate() => (!enableCombo || comboLength >= 1) && NonNegative(windup) && Positive(activeDuration) && NonNegative(recovery) &&
            Positive(comboWindow) && Positive(reach) && Positive(width) && NonNegative(knockbackSpeed) &&
            NonNegative(launchSpeed) && NonNegative(slamHoverDuration) && Positive(slamSpeed) && Positive(slamKnockdownSpeed) && Positive(shockwaveRadius) &&
            Positive(shockwaveDuration) && Positive(slamTimeout) && NonNegative(emergenceSpeed) &&
            Positive(forcedDuration) && NonNegative(hitStun);
        private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
        private static bool NonNegative(float value) => value >= 0f && !float.IsInfinity(value);
    }
}
