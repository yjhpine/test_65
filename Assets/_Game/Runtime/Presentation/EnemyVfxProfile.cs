using UnityEngine;
namespace ActionPlatformer.Presentation
{
    [CreateAssetMenu(menuName="Action Platformer/Presentation/Enemy VFX")]
    public sealed class EnemyVfxProfile : ScriptableObject
    {
        [SerializeField] private Sprite shapeSprite;
        [SerializeField] private Sprite[] startFrames, bodyFrames, endFrames;
        [SerializeField] private Material beamMaterial;
        [SerializeField] private Color telegraphColor = new Color(.17f, .44f, .91f);
        public Sprite ShapeSprite => shapeSprite;
        public Sprite[] StartFrames => startFrames;
        public Sprite[] BodyFrames => bodyFrames;
        public Sprite[] EndFrames => endFrames;
        public Material BeamMaterial => beamMaterial;
        public Color TelegraphColor => telegraphColor;
        public bool HasLaser => startFrames != null && startFrames.Length == 21 && bodyFrames != null && bodyFrames.Length == 13 && endFrames != null && endFrames.Length == 13 && beamMaterial != null;
    }
}
