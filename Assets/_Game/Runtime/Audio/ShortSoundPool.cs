using UnityEngine;

namespace ActionPlatformer.Audio
{
    // Owned by the presenting object. Bounded sources respect AudioListener pause/volume.
    internal sealed class ShortSoundPool
    {
        private readonly AudioSource[] sources;
        private int next;
        public ShortSoundPool(Transform owner, int count, bool spatial)
        {
            sources = new AudioSource[count];
            for (int i = 0; i < count; i++)
            {
                var child = new GameObject("SFX voice " + i); child.transform.SetParent(owner, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.dopplerLevel = 0;
                source.spatialBlend = spatial ? .85f : 0;
                source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 10; source.maxDistance = 38;
                source.priority = spatial ? 150 : 100;
                sources[i] = source;
            }
        }
        public void Play(AudioClip clip, float volume)
        {
            if (clip == null || volume <= 0 || AudioListener.pause) return;
            int index = next;
            for (int i = 0; i < sources.Length; i++)
                if (!sources[(next + i) % sources.Length].isPlaying) { index = (next + i) % sources.Length; break; }
            var source = sources[index]; next = (index + 1) % sources.Length;
            source.Stop(); source.clip = clip; source.volume = Mathf.Clamp01(volume); source.pitch = 1;
            source.Play();
        }
        public void Stop() { foreach (var source in sources) { source.Stop(); source.clip = null; } next = 0; }
    }
}
