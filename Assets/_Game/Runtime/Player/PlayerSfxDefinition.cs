using UnityEngine;

namespace ActionPlatformer.Player
{
    [CreateAssetMenu(menuName = "Action Platformer/Audio/Player SFX")]
    public sealed class PlayerSfxDefinition : ScriptableObject
    {
        [SerializeField] private AudioClip[] swings, hits, landings;
        [SerializeField] private AudioClip heavySwing, heavyHit, slamLanding, glitch;
        [SerializeField, Range(0, 1)] private float swingVolume = .32f;
        [SerializeField, Range(0, 1)] private float hitVolume = .42f;
        [SerializeField, Range(0, 1)] private float landingVolume = .24f;
        [SerializeField, Range(0, 1)] private float glitchVolume = .38f;
        public AudioClip[] Swings => swings;
        public AudioClip[] Hits => hits;
        public AudioClip[] Landings => landings;
        public AudioClip HeavySwing => heavySwing;
        public AudioClip HeavyHit => heavyHit;
        public AudioClip SlamLanding => slamLanding;
        public AudioClip Glitch => glitch;
        public float SwingVolume => swingVolume;
        public float HitVolume => hitVolume;
        public float LandingVolume => landingVolume;
        public float GlitchVolume => glitchVolume;
    }
}
