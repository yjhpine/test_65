using UnityEngine;

namespace ActionPlatformer.Audio
{
    // Player-chosen volume scales, 0..1 (planning doc 5.1). The master volume is AudioListener.volume.
    public static class AudioVolumes
    {
        public static float Sfx { get; private set; } = 1f;
        // Applies to music once the game has any; nothing plays music yet.
        public static float Music { get; private set; } = 1f;

        public static void Set(float master, float music, float sfx)
        {
            AudioListener.volume = Mathf.Clamp01(master);
            Music = Mathf.Clamp01(music);
            Sfx = Mathf.Clamp01(sfx);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Sfx = 1f; Music = 1f; AudioListener.volume = 1f; }
    }
}
