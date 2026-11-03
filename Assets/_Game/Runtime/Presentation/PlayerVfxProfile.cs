using UnityEngine;
namespace ActionPlatformer.Presentation
{
    [CreateAssetMenu(menuName="Action Platformer/Presentation/Player VFX")]
    public sealed class PlayerVfxProfile : ScriptableObject
    {
        [SerializeField] private Sprite[] basicAttackEffectFrames;
        public Sprite[] BasicAttackEffectFrames => basicAttackEffectFrames;
        [SerializeField] private Sprite[] comboSlash1Frames;
        [SerializeField] private Sprite[] comboSlash2Frames;
        [SerializeField] private Sprite[] comboSlash3Frames;
        public Sprite[] ComboSlash1Frames => comboSlash1Frames;
        public Sprite[] ComboSlash2Frames => comboSlash2Frames;
        public Sprite[] ComboSlash3Frames => comboSlash3Frames;
        public Sprite[] ComboSlashFrames(int strike) => strike == 3 ? comboSlash3Frames :
            strike == 2 ? comboSlash2Frames : comboSlash1Frames;
        [SerializeField] private Sprite[] glitchEffectFrames;
        public Sprite[] GlitchEffectFrames => glitchEffectFrames;
        [SerializeField] private Sprite[] jumpDustFrames;
        public Sprite[] JumpDustFrames => jumpDustFrames;
        [SerializeField] private Sprite[] groundDustFrames;
        public Sprite[] GroundDustFrames => groundDustFrames;
        [SerializeField] private Sprite[] hitImpactFrames;
        public Sprite[] HitImpactFrames => hitImpactFrames;
        [SerializeField] private Sprite[] finisherImpactFrames;
        public Sprite[] FinisherImpactFrames => finisherImpactFrames;
        // One-shots played at the sprites' own size: debris that lingers after a hit, and a burst where a kill's target stood.
        [SerializeField] private Sprite[] hitDebrisFrames;
        public Sprite[] HitDebrisFrames => hitDebrisFrames;
        [SerializeField] private Sprite[] killShatterFrames;
        public Sprite[] KillShatterFrames => killShatterFrames;
        [SerializeField, Min(1f)] private float contactBurstFps = 30f;
        public float ContactBurstFps => contactBurstFps;
        // Looping glitch destination marker while aiming, at the sprites' own size. Empty keeps the plain circle.
        [SerializeField] private Sprite[] glitchMarkerReadyFrames;
        public Sprite[] GlitchMarkerReadyFrames => glitchMarkerReadyFrames;
        [SerializeField] private Sprite[] glitchMarkerBlockedFrames;
        public Sprite[] GlitchMarkerBlockedFrames => glitchMarkerBlockedFrames;
        [SerializeField, Min(1f)] private float glitchMarkerFps = 30f;
        public float GlitchMarkerFps => glitchMarkerFps;
        [SerializeField] private Vector2 dustFootOffset = new Vector2(0, -.83f);
        public Vector2 DustFootOffset => dustFootOffset;
        [SerializeField] private Material actionEffectMaterial;
        public Material ActionEffectMaterial => actionEffectMaterial;
        [SerializeField, Min(0.01f)] private float basicAttackEffectSize = 2f;
        public float BasicAttackEffectSize => basicAttackEffectSize;
        [SerializeField, Min(0.01f)] private float glitchEffectSize = 2.5f;
        public float GlitchEffectSize => glitchEffectSize;
        [SerializeField, Min(1f)] private float glitchEffectFps = 30f;
        public float GlitchEffectFps => glitchEffectFps;
        [SerializeField, Min(0.1f)] private float glitchTrailSpacing = .6f;
        public float GlitchTrailSpacing => glitchTrailSpacing;
        [SerializeField, Min(0.001f)] private float glitchTrailInterval = .025f;
        public float GlitchTrailInterval => glitchTrailInterval;
    }
}
