using UnityEngine;

namespace ActionPlatformer.Player
{
    // Single-player impact presentation. Combat reports facts; this object never changes damage or combo rules.
    public sealed class PlayerImpactFeedback
    {
        private readonly PlayerCombatTuning tuning;
        private Camera camera;
        private Vector3 appliedOffset;
        private Vector3 lastRenderedOffset;
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
        private int activePriority, pendingPriority;
        private float pendingPixels, pendingDuration;
        private Vector2 pendingDirection;
        private double pendingAt;
        private bool reducedMotion;
        public bool ReducedMotion
        {
            get => reducedMotion;
            set { reducedMotion = value; if (value) { RemoveCameraOffset(); lastRenderedOffset = Vector3.zero; } }
        }
        private readonly PlayerDamageScreen damageScreen;
        public bool IsDamageFeedbackActive(double now) => now >= damageStartedAt &&
            now < damageStartedAt + Mathf.Min(.16f, tuning.DamageScreenDuration);
        private Vector2 shakeDirection;
        public bool IsHitStopped => ownsTimeScale && !slowing;
        public bool IsTimeEffectActive => ownsTimeScale;

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
            lastRenderedOffset = Vector3.zero;
            damageScreen.Hide();
        }

        public void Play(PlayerImpact impact, double unscaledNow, bool screenFeedback = true)
        {
            float duration = impact.Strength == PlayerImpactStrength.Slam ? tuning.SlamHitStopDuration :
                impact.Strength == PlayerImpactStrength.Heavy ? tuning.HeavyHitStopDuration : tuning.HitStopDuration;
            RequestHitStop(duration, unscaledNow);
            if (screenFeedback) RequestHitScreen(impact.Strength == PlayerImpactStrength.Slam ? PlayerAttack.Slam :
                impact.Strength == PlayerImpactStrength.Heavy ? PlayerAttack.Lift : PlayerAttack.Side,
                false, impact.Direction, unscaledNow);
        }

        public void PlayHit(PlayerHitPresentation hit, double now)
            => RequestHitScreen(hit.Kind, hit.Killed, hit.Direction, now);

        private void RequestHitScreen(PlayerAttack kind, bool killed, Vector2 direction, double now)
        {
            bool down = kind == PlayerAttack.Slam || kind == PlayerAttack.Shockwave;
            bool up = kind == PlayerAttack.Lift || kind == PlayerAttack.Emergence;
            float pixels = killed ? 3f : down ? tuning.SlamShakeAmplitude : up ? tuning.HeavyShakeAmplitude : tuning.ShakeAmplitude;
            float duration = killed || down ? .10f : up ? .08f : tuning.ShakeDuration;
            QueueScreen(killed ? 2 : 1, direction, pixels, duration, now);
        }

        private void QueueScreen(int priority, Vector2 direction, float pixels, float duration, double now)
        {
            if (priority < pendingPriority || (priority == pendingPriority && pixels <= pendingPixels)) return;
            pendingPriority = priority; pendingDirection = direction; pendingPixels = Mathf.Clamp(pixels, 0, 4);
            pendingDuration = duration; pendingAt = now;
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
            QueueScreen(3, direction, tuning.DamageShakeAmplitude, tuning.DamageShakeDuration, unscaledNow);
        }

        private void StartShake(Vector2 direction, float amplitude, float duration, double now)
        {
            shakeStartedAt = now;
            shakeAmplitude = amplitude;
            activeShakeDuration = duration;
            shakeDirection = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.zero;
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

        // Remove the previous rendering offset from the base camera, but retain it for cursor picking.
        public void Tick(double unscaledNow)
        {
            lastRenderedOffset = appliedOffset;
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

        public Ray ScreenPointToRay(Vector2 position)
        {
            Ray ray = camera.ScreenPointToRay(position);
            ray.origin += lastRenderedOffset - appliedOffset;
            return ray;
        }

        public float ReferencePixelsToWorld(float pixels)
        {
            if (camera == null) return 0;
            float halfHeight = camera.orthographic ? camera.orthographicSize :
                Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) * Mathf.Abs(camera.transform.position.z);
            // actualPixels = referencePixels * viewportHeight / 660; worldPerPixel = 2H / viewportHeight.
            return Mathf.Clamp(pixels, 0, 4) * 2f * halfHeight / 660f;
        }

        public void LateTick(double unscaledNow)
        {
            RemoveCameraOffset();
            if (pendingPriority > 0)
            {
                if (unscaledNow >= shakeStartedAt + activeShakeDuration || pendingPriority > activePriority ||
                    (pendingPriority == activePriority && pendingPixels >= shakeAmplitude))
                {
                    activePriority = pendingPriority;
                    StartShake(pendingDirection, pendingPixels, pendingDuration, pendingAt);
                }
                pendingPriority = 0; pendingPixels = 0;
            }
            if (CameraEffectsSuppressed) { lastRenderedOffset = Vector3.zero; damageScreen.Hide(); return; }
            float damageAge = (float)(unscaledNow - damageStartedAt);
            float vignette = Fade(damageAge, Mathf.Min(.16f, tuning.DamageScreenDuration)) * Mathf.Min(.12f, tuning.DamageVignetteOpacity);
            if (camera == null) { damageScreen.Hide(); return; }
            float age = (float)(unscaledNow - shakeStartedAt);
            if (!reducedMotion && age >= 0f && age < activeShakeDuration)
            {
                float envelope = 1f - age / activeShakeDuration;
                appliedOffset = shakeDirection * (ReferencePixelsToWorld(shakeAmplitude) * envelope * envelope);
                camera.transform.position += appliedOffset;
            }
            lastRenderedOffset = appliedOffset;
            damageScreen.Draw(camera, 0, vignette, 0);
        }

        private static float Fade(float age, float duration) => age < 0f || duration <= 0f ? 0f : Mathf.Pow(Mathf.Clamp01(1f - age / duration), 2f);

        private void RemoveCameraOffset()
        {
            if (camera != null)
            {
                camera.transform.position -= appliedOffset;
            }
            appliedOffset = Vector3.zero;
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
            activePriority = pendingPriority = 0;
            pendingPixels = 0;
            lastRenderedOffset = Vector3.zero;
            damageStartedAt = double.NegativeInfinity;
            damageScreen.Hide();
        }

        public void Dispose() { Reset(); damageScreen.Dispose(); }
    }
}
