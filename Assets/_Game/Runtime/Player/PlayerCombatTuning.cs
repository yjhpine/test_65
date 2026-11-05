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
        [Header("Combo finisher (strong third hit)")]
        [Tooltip("Damage multiplier of the combo's finishing strike, whatever attack kind it is.")]
        [SerializeField, Min(1f)] private float finisherDamageMultiplier = 2f;
        [Tooltip("Launch speed multiplier of a finishing lift, applied to both the player and its target.")]
        [SerializeField, Min(1f)] private float finisherLaunchMultiplier = 1.5f;
        [Header("Air slam")]
        [SerializeField, Min(0f)] private float slamHoverDuration = 0.5f;
        [SerializeField, Min(0.01f)] private float slamSpeed = 36f;
        [SerializeField, Min(0.01f)] private float slamKnockdownSpeed = 36f;
        [SerializeField, Min(0.01f)] private float shockwaveRadius = 2f;
        [SerializeField, Min(0.01f)] private float shockwaveDuration = 0.22f;
        [SerializeField, Min(0.01f)] private float slamTimeout = 3f;
        [SerializeField, Min(0f)] private float emergenceSpeed = 10f;
        [SerializeField, Min(0.01f)] private float forcedDuration = 0.35f;
        [SerializeField, Min(0f)] private float hitStun = 0.25f;
        [Header("Player damage response (0 disables each part)")]
        [Tooltip("Enemy hits are ignored this long after the player loses health. Real time, so the hit's slow motion counts; a stopped world (pause, cutscene) does not. The sprite blinks meanwhile.")]
        [SerializeField, Min(0f)] private float damageInvulnerability = 1.5f;
        [Tooltip("An enemy hit pushes the player away from its source: starting speed, falling linearly to zero over the duration (game time). Damage without a source does not push.")]
        [SerializeField, Min(0f)] private float damageKnockbackSpeed = 10f;
        [SerializeField, Min(0f)] private float damageKnockbackDuration = .25f;
        [Tooltip("Upward speed of the small hop that starts the knockback.")]
        [SerializeField, Min(0f)] private float damageKnockbackLift = 7f;
        [Header("Attack recoil (a landed basic swing pushes the player back; 0 disables)")]
        [Tooltip("Starting speed away from the struck side; it falls linearly to zero over the duration.")]
        [SerializeField, Min(0f)] private float recoilSpeed = 4f;
        [SerializeField, Min(0f)] private float recoilDuration = .1f;
        [SerializeField, Min(0f)] private float finisherRecoilSpeed = 7f;
        [SerializeField, Min(0f)] private float finisherRecoilDuration = .12f;
        [SerializeField, Range(0f, 1f)] private float airRecoilMultiplier = .6f;
        [Header("Attack input buffer")]
        [SerializeField, Min(0f)] private float attackBufferTime = 0.1f;
        [Header("Impact feedback (0 disables each effect)")]
        [SerializeField, Min(0f)] private float hitStopDuration = 0.035f;
        [SerializeField, Min(0f)] private float heavyHitStopDuration = 0.055f;
        [SerializeField, Min(0f)] private float slamHitStopDuration = 0.08f;
        [Tooltip("Normal hit movement in reference-screen pixels (1120 x 660). Capped at 4.")]
        [SerializeField, Min(0f)] private float shakeAmplitude = 1.5f;
        [Tooltip("Lift/emergence movement in reference-screen pixels; finishers do not boost camera motion.")]
        [SerializeField, Min(0f)] private float heavyShakeAmplitude = 2f;
        [SerializeField, Min(0f)] private float slamShakeAmplitude = 3f;
        [SerializeField, Min(0f)] private float shakeDuration = 0.06f;
        [Tooltip("Oscillation of the slam, shockwave and kill shakes; other hits stay a single directional kick.")]
        [SerializeField, Min(0.01f)] private float shakeFrequency = 22f;
        [Tooltip("Extra hit stop when the swing kills a target. Added only to an enabled hit stop.")]
        [SerializeField, Min(0f)] private float killHitStopBonus = .04f;
        [Tooltip("Slow motion after the hit stop of the blow that clears a room's last enemy.")]
        [SerializeField, Range(.01f, 1f)] private float finalBlowSlowTimeScale = .3f;
        [SerializeField, Min(0f)] private float finalBlowSlowDuration = .2f;
        [SerializeField, Min(0f)] private float finalBlowSlowRecovery = .15f;
        [Tooltip("Glitch screen tear when the combo's strong strike lands. Off in reduced motion.")]
        [SerializeField, Min(0f)] private float finisherGlitchDuration = .1f;
        [Header("Player damage feedback (0 disables each effect)")]
        [SerializeField, Range(.01f, 1f)] private float damageSlowTimeScale = .2f;
        [SerializeField, Min(0f)] private float damageSlowDuration = .15f;
        [SerializeField, Min(0f)] private float damageSlowRecovery = .1f;
        [Tooltip("Player damage movement in reference-screen pixels; zero direction never invents an axis.")]
        [SerializeField, Min(0f)] private float damageShakeAmplitude = 4f;
        [SerializeField, Min(0f)] private float damageShakeDuration = .12f;
        [SerializeField, HideInInspector, Range(0f, .2f)] private float damageZoom;
        [SerializeField, Min(0f)] private float damageZoomDuration = .18f;
        [SerializeField, HideInInspector, Range(0f, 1f)] private float damageFlashOpacity;
        [SerializeField, Min(0f)] private float damageFlashDuration = .065f;
        [SerializeField, Range(0f, .12f)] private float damageVignetteOpacity = .12f;
        [SerializeField, Range(0f, .16f)] private float damageScreenDuration = .16f;
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
        public float FinisherDamageMultiplier => finisherDamageMultiplier;
        public float FinisherLaunchMultiplier => finisherLaunchMultiplier;
        public float SlamSpeed => slamSpeed;
        public float SlamKnockdownSpeed => slamKnockdownSpeed;
        public float SlamHoverDuration => slamHoverDuration;
        public float ShockwaveRadius => shockwaveRadius;
        public float ShockwaveDuration => shockwaveDuration;
        public float SlamTimeout => slamTimeout;
        public float EmergenceSpeed => emergenceSpeed;
        public float ForcedDuration => forcedDuration;
        public float HitStun => hitStun;
        public float DamageInvulnerability => damageInvulnerability;
        public float DamageKnockbackSpeed => damageKnockbackSpeed;
        public float DamageKnockbackDuration => damageKnockbackDuration;
        public float DamageKnockbackLift => damageKnockbackLift;
        public float AttackBufferTime => attackBufferTime;
        public float RecoilSpeed => recoilSpeed;
        public float RecoilDuration => recoilDuration;
        public float FinisherRecoilSpeed => finisherRecoilSpeed;
        public float FinisherRecoilDuration => finisherRecoilDuration;
        public float AirRecoilMultiplier => airRecoilMultiplier;
        public float HitStopDuration => hitStopDuration;
        public float HeavyHitStopDuration => heavyHitStopDuration;
        public float SlamHitStopDuration => slamHitStopDuration;
        public float ShakeAmplitude => shakeAmplitude;
        public float HeavyShakeAmplitude => heavyShakeAmplitude;
        public float SlamShakeAmplitude => slamShakeAmplitude;
        public float ShakeDuration => shakeDuration;
        public float ShakeFrequency => shakeFrequency;
        public float KillHitStopBonus => killHitStopBonus;
        public float FinalBlowSlowTimeScale => finalBlowSlowTimeScale;
        public float FinalBlowSlowDuration => finalBlowSlowDuration;
        public float FinalBlowSlowRecovery => finalBlowSlowRecovery;
        public float FinisherGlitchDuration => finisherGlitchDuration;
        public float DamageSlowTimeScale => damageSlowTimeScale;
        public float DamageSlowDuration => damageSlowDuration;
        public float DamageSlowRecovery => damageSlowRecovery;
        public float DamageShakeAmplitude => damageShakeAmplitude;
        public float DamageShakeDuration => damageShakeDuration;
        public float DamageZoom => damageZoom;
        public float DamageZoomDuration => damageZoomDuration;
        public float DamageFlashOpacity => damageFlashOpacity;
        public float DamageFlashDuration => damageFlashDuration;
        public float DamageVignetteOpacity => damageVignetteOpacity;
        public float DamageScreenDuration => damageScreenDuration;
        public LayerMask TargetMask => targetMask;
        public LayerMask ObstacleMask => obstacleMask;
        public bool TryValidate() => (!enableCombo || comboLength >= 1) && NonNegative(windup) && Positive(activeDuration) && NonNegative(recovery) &&
            Positive(comboWindow) && Positive(reach) && Positive(width) && NonNegative(lightKnockbackSpeed) && Positive(lightKnockbackDeceleration) &&
            NonNegative(knockbackSpeed) && Positive(knockbackDeceleration) &&
            NonNegative(launchSpeed) && Positive(finisherDamageMultiplier) && finisherDamageMultiplier >= 1f &&
            Positive(finisherLaunchMultiplier) && finisherLaunchMultiplier >= 1f && NonNegative(slamHoverDuration) && Positive(slamSpeed) && Positive(slamKnockdownSpeed) && Positive(shockwaveRadius) &&
            Positive(shockwaveDuration) && Positive(slamTimeout) && NonNegative(emergenceSpeed) &&
            Positive(forcedDuration) && NonNegative(hitStun) && NonNegative(attackBufferTime) &&
            NonNegative(damageInvulnerability) && NonNegative(damageKnockbackSpeed) && NonNegative(damageKnockbackDuration) &&
            NonNegative(damageKnockbackLift) &&
            NonNegative(recoilSpeed) && NonNegative(recoilDuration) && NonNegative(finisherRecoilSpeed) &&
            NonNegative(finisherRecoilDuration) && NonNegative(airRecoilMultiplier) && airRecoilMultiplier <= 1f &&
            NonNegative(hitStopDuration) && NonNegative(heavyHitStopDuration) && NonNegative(slamHitStopDuration) &&
            NonNegative(shakeAmplitude) && NonNegative(heavyShakeAmplitude) && NonNegative(slamShakeAmplitude) &&
            NonNegative(shakeDuration) && Positive(shakeFrequency) && NonNegative(killHitStopBonus) &&
            Positive(finalBlowSlowTimeScale) && finalBlowSlowTimeScale <= 1f && NonNegative(finalBlowSlowDuration) &&
            NonNegative(finalBlowSlowRecovery) && NonNegative(finisherGlitchDuration) &&
            Positive(damageSlowTimeScale) && damageSlowTimeScale <= 1f &&
            NonNegative(damageSlowDuration) && NonNegative(damageSlowRecovery) && NonNegative(damageShakeAmplitude) && NonNegative(damageShakeDuration) &&
            NonNegative(damageZoom) && damageZoom <= .2f && NonNegative(damageZoomDuration) &&
            NonNegative(damageFlashOpacity) && damageFlashOpacity <= 1f && NonNegative(damageFlashDuration) &&
            NonNegative(damageVignetteOpacity) && damageVignetteOpacity <= 1f && NonNegative(damageScreenDuration);
        private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
        private static bool NonNegative(float value) => value >= 0f && !float.IsInfinity(value);
    }
}
