using UnityEngine;
namespace ActionPlatformer.Presentation
{
    [CreateAssetMenu(menuName="Action Platformer/Presentation/Player VFX")]
    public sealed class PlayerVfxProfile : ScriptableObject
    {
        [SerializeField] private Sprite[] basicAttackEffectFrames;
        public Sprite[] BasicAttackEffectFrames => basicAttackEffectFrames;
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
