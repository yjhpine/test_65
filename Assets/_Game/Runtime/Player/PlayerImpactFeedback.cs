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
        private double shakeStartedAt = double.NegativeInfinity;
        private float shakeAmplitude;
        private Vector2 shakeDirection;
        public bool IsHitStopped => ownsTimeScale;

        public PlayerImpactFeedback(PlayerCombatTuning tuning, Camera camera)
        {
            this.tuning = tuning ?? throw new System.ArgumentNullException(nameof(tuning));
            this.camera = camera;
        }

        public void SetCamera(Camera value)
        {
            RemoveCameraOffset();
            camera = value;
        }

        public void Play(PlayerImpact impact, double unscaledNow)
        {
            float duration = impact.Strength == PlayerImpactStrength.Slam ? tuning.SlamHitStopDuration :
                impact.Strength == PlayerImpactStrength.Heavy ? tuning.HeavyHitStopDuration : tuning.HitStopDuration;
            float amplitude = impact.Strength == PlayerImpactStrength.Slam ? tuning.SlamShakeAmplitude :
                impact.Strength == PlayerImpactStrength.Heavy ? tuning.HeavyShakeAmplitude : tuning.ShakeAmplitude;
            if (duration > 0f && (ownsTimeScale || Time.timeScale > 0f))
            {
                if (!ownsTimeScale)
                {
                    savedTimeScale = Time.timeScale;
                    ownsTimeScale = true;
                    stopEndsAt = unscaledNow;
                    Time.timeScale = 0f;
                }
                stopEndsAt = System.Math.Max(stopEndsAt, unscaledNow + duration);
            }
            // Replace with the strongest impact, never sum the amplitudes or durations of simultaneous victims.
            if (amplitude > 0f && tuning.ShakeDuration > 0f &&
                (unscaledNow >= shakeStartedAt + tuning.ShakeDuration || amplitude > shakeAmplitude))
            {
                shakeStartedAt = unscaledNow;
                shakeAmplitude = amplitude;
                shakeDirection = impact.Direction.normalized;
            }
        }

        // Called before input sampling: aim rays use the unshaken camera position.
        public void Tick(double unscaledNow)
        {
            RemoveCameraOffset();
            if (ownsTimeScale && (unscaledNow >= stopEndsAt || Time.timeScale != 0f)) ReleaseHitStop();
        }

        public void LateTick(double unscaledNow)
        {
            RemoveCameraOffset();
            if (camera == null || tuning.ShakeDuration <= 0f) return;
            float age = (float)(unscaledNow - shakeStartedAt);
            if (age < 0f || age >= tuning.ShakeDuration) return;
            float envelope = 1f - age / tuning.ShakeDuration;
            float wave = Mathf.Cos(age * tuning.ShakeFrequency * Mathf.PI * 2f);
            appliedOffset = shakeDirection * (shakeAmplitude * envelope * envelope * wave);
            camera.transform.position += appliedOffset;
        }

        private void RemoveCameraOffset()
        {
            if (camera != null) camera.transform.position -= appliedOffset;
            appliedOffset = Vector3.zero;
        }

        private void ReleaseHitStop()
        {
            if (!ownsTimeScale) return;
            // Respect a nonzero time-scale change made by another owner while the effect was active.
            if (Time.timeScale == 0f) Time.timeScale = savedTimeScale;
            ownsTimeScale = false;
        }

        public void Reset()
        {
            ReleaseHitStop();
            RemoveCameraOffset();
            shakeStartedAt = double.NegativeInfinity;
            shakeAmplitude = 0f;
        }
    }
}
