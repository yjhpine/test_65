using UnityEngine;

namespace ActionPlatformer.Presentation
{
    // Player-chosen strengths of the decorative screen effects (planning doc 5.2), each 0..1.
    // They only scale decoration: glitch range, collisions, hit boxes, cooldowns and danger markers never read them.
    public static class PresentationSettings
    {
        // Camera shake from hits, slams, kills and damage.
        public static float Shake { get; private set; } = 1f;
        // The full-screen tear/split overlay of the strong strike.
        public static float Distortion { get; private set; } = 1f;
        // Full-screen brightness change (damage vignette), contact flashes and the protection blink's depth.
        public static float Flash { get; private set; } = 1f;

        public static void Set(float shake, float distortion, float flash)
        {
            Shake = Mathf.Clamp01(shake);
            Distortion = Mathf.Clamp01(distortion);
            Flash = Mathf.Clamp01(flash);
        }

        public static void Reset() => Set(1f, 1f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
