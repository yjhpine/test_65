using UnityEngine;

namespace ActionPlatformer.Player
{
    // Single-player impact presentation. Combat reports facts; this object never changes damage or combo rules.
    public sealed class PlayerImpactFeedback
    {
        private readonly PlayerCombatTuning tuning;
        private Camera camera;
        private Vector3 appliedOffset;
        private bool ownsTimeScale;
        private float savedTimeScale;
        private double stopEndsAt;
        private float appliedTimeScale;
        private float savedFixedDeltaTime;
        private float appliedFixedDeltaTime;
        private bool slowing;
        private double slowStartedAt;
        private double shakeStartedAt = double.NegativeInfinity;
        private float shakeAmplitude;
        private float activeShakeDuration;
        private double damageStartedAt = double.NegativeInfinity;
        private float appliedZoom;
        private readonly PlayerDamageScreen damageScreen;
        public bool IsDamageFeedbackActive(double now) => now >= damageStartedAt &&
            now < damageStartedAt + System.Math.Max(tuning.DamageScreenDuration, tuning.DamageFlashDuration);
        private Vector2 shakeDirection;
        public bool IsHitStopped => ownsTimeScale && !slowing;

        public PlayerImpactFeedback(PlayerCombatTuning tuning, Camera camera, Material damageMaterial = null)
        {
            this.tuning = tuning ?? throw new System.ArgumentNullException(nameof(tuning));
            this.camera = camera;
            damageScreen = new PlayerDamageScreen(damageMaterial);
        }

        public void SetCamera(Camera value)
        {
            RemoveCameraOffset();
            camera = value;
            damageScreen.Hide();
        }

        public void Play(PlayerImpact impact, double unscaledNow)
        {
            float duration = impact.Strength == PlayerImpactStrength.Slam ? tuning.SlamHitStopDuration :
                impact.Strength == PlayerImpactStrength.Heavy ? tuning.HeavyHitStopDuration : tuning.HitStopDuration;
            float amplitude = impact.Strength == PlayerImpactStrength.Slam ? tuning.SlamShakeAmplitude :
                impact.Strength == PlayerImpactStrength.Heavy ? tuning.HeavyShakeAmplitude : tuning.ShakeAmplitude;
            RequestHitStop(duration, unscaledNow);
            // Simultaneous victims share the strongest impulse, rather than adding camera offsets.
            if (amplitude > 0f && tuning.ShakeDuration > 0f &&
                (unscaledNow >= shakeStartedAt + activeShakeDuration || amplitude > shakeAmplitude))
                StartShake(impact.Direction, amplitude, tuning.ShakeDuration, unscaledNow);
        }

        public void PlayDamage(Vector2 direction, double unscaledNow)
        {
            // Damage slow motion takes priority over outgoing hit stop and refreshes on repeat hits.
            if (tuning.DamageSlowDuration > 0f && tuning.DamageSlowTimeScale < 1f && AcquireTimeScale())
            {
                slowing = true;
                slowStartedAt = unscaledNow;
                ApplySlowScale(tuning.DamageSlowTimeScale);
            }
            damageStartedAt = unscaledNow;
            if (tuning.DamageShakeAmplitude > 0f && tuning.DamageShakeDuration > 0f)
                StartShake(direction, tuning.DamageShakeAmplitude, tuning.DamageShakeDuration, unscaledNow);
        }

        private void StartShake(Vector2 direction, float amplitude, float duration, double now)
        {
            shakeStartedAt = now;
            shakeAmplitude = amplitude;
            activeShakeDuration = duration;
            shakeDirection = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.right;
        }

        private bool AcquireTimeScale()
        {
            if (ownsTimeScale && Time.timeScale != appliedTimeScale) ReleaseTimeScale();
            if (ownsTimeScale) return true;
            if (Time.timeScale <= 0f) return false;
            savedTimeScale = Time.timeScale;
            savedFixedDeltaTime = appliedFixedDeltaTime = Time.fixedDeltaTime;
            appliedTimeScale = Time.timeScale;
            ownsTimeScale = true;
            return true;
        }

        private void ApplySlowScale(float multiplier)
        {
            appliedTimeScale = savedTimeScale * multiplier;
            Time.timeScale = appliedTimeScale;
            // Keep real-time physics cadence smooth while gameplay time advances more slowly.
            appliedFixedDeltaTime = savedFixedDeltaTime * multiplier;
            Time.fixedDeltaTime = appliedFixedDeltaTime;
            // Unity quantizes the physics interval internally; retain the value it actually stored.
            appliedFixedDeltaTime = Time.fixedDeltaTime;
        }

        private void RequestHitStop(float duration, double unscaledNow)
        {
            if (duration <= 0f || (ownsTimeScale && slowing) || !AcquireTimeScale()) return;
            if (appliedTimeScale != 0f) stopEndsAt = unscaledNow;
            appliedTimeScale = 0f;
            Time.timeScale = 0f;
            stopEndsAt = System.Math.Max(stopEndsAt, unscaledNow + duration);
        }

        // Called before input sampling: aim rays use the unshaken camera position.
        public void Tick(double unscaledNow)
        {
            RemoveCameraOffset();
            if (!ownsTimeScale) return;
            if (Time.timeScale != appliedTimeScale) { ReleaseTimeScale(); return; }
            if (!slowing)
            {
                if (unscaledNow >= stopEndsAt) ReleaseTimeScale();
                return;
            }
            float recoveryAge = (float)(unscaledNow - slowStartedAt) - tuning.DamageSlowDuration;
            if (recoveryAge >= tuning.DamageSlowRecovery) { ReleaseTimeScale(); return; }
            float progress = tuning.DamageSlowRecovery > 0f ? Mathf.Clamp01(recoveryAge / tuning.DamageSlowRecovery) : 0f;
            ApplySlowScale(Mathf.Lerp(tuning.DamageSlowTimeScale, 1f, Mathf.SmoothStep(0f, 1f, progress)));
        }

        public bool CameraEffectsSuppressed { get; set; }

        public void LateTick(double unscaledNow)
        {
            RemoveCameraOffset();
            if (CameraEffectsSuppressed) { damageScreen.Hide(); return; }
            float damageAge = (float)(unscaledNow - damageStartedAt);
            float flash = Fade(damageAge, tuning.DamageFlashDuration) * tuning.DamageFlashOpacity;
            float vignette = Fade(damageAge, tuning.DamageScreenDuration) * tuning.DamageVignetteOpacity;
            float streak = Fade(damageAge, Mathf.Min(.18f, tuning.DamageScreenDuration)) * tuning.DamageVignetteOpacity;
            if (camera == null) { damageScreen.Hide(); return; }
            if (camera.orthographic)
            {
                appliedZoom = camera.orthographicSize * tuning.DamageZoom * Fade(damageAge, tuning.DamageZoomDuration);
                camera.orthographicSize -= appliedZoom;
            }
            float age = (float)(unscaledNow - shakeStartedAt);
            if (age >= 0f && age < activeShakeDuration)
            {
                float envelope = 1f - age / activeShakeDuration;
                float wave = Mathf.Cos(age * tuning.ShakeFrequency * Mathf.PI * 2f);
                appliedOffset = shakeDirection * (shakeAmplitude * envelope * envelope * wave);
                camera.transform.position += appliedOffset;
            }
            damageScreen.Draw(camera, flash, vignette, streak);
        }

        private static float Fade(float age, float duration) => age < 0f || duration <= 0f ? 0f : Mathf.Pow(Mathf.Clamp01(1f - age / duration), 2f);

        private void RemoveCameraOffset()
        {
            if (camera != null)
            {
                camera.transform.position -= appliedOffset;
                if (appliedZoom != 0f) camera.orthographicSize += appliedZoom;
            }
            appliedOffset = Vector3.zero;
            appliedZoom = 0f;
        }

        private void ReleaseTimeScale()
        {
            if (!ownsTimeScale) return;
            // Respect an external pause or time-scale override. Restore only values we still own.
            if (Time.timeScale == appliedTimeScale) Time.timeScale = savedTimeScale;
            if (Time.fixedDeltaTime == appliedFixedDeltaTime) Time.fixedDeltaTime = savedFixedDeltaTime;
            slowing = false;
            ownsTimeScale = false;
        }

        public void Reset()
        {
            ReleaseTimeScale();
            RemoveCameraOffset();
            shakeStartedAt = double.NegativeInfinity;
            shakeAmplitude = 0f;
            activeShakeDuration = 0f;
            damageStartedAt = double.NegativeInfinity;
            damageScreen.Hide();
        }

        public void Dispose() { Reset(); damageScreen.Dispose(); }
    }
}
