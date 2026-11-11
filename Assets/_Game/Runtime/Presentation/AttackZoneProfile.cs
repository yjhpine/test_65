using UnityEngine;
namespace ActionPlatformer.Presentation
{
    // The one danger-zone look every monster shares: a looping red "corrupted data" tile laid over the attack area.
    // Frames are drawn Tiled (1 px border), so the glitch pixels keep their art size however far the zone stretches.
    // Urgency 0 is the moment the warning appears, 1 the moment the attack lands; only the opacity follows it.
    [CreateAssetMenu(menuName="Action Platformer/Presentation/Attack Zone")]
    public sealed class AttackZoneProfile : ScriptableObject
    {
        [Tooltip("Looping tile frames (Sprite border > 0, Full Rect mesh), drawn Tiled over each attack area.")]
        [SerializeField] private Sprite[] frames;
        [Tooltip("Frames per second of game time (pauses and slows with the game).")]
        [SerializeField, Min(.01f)] private float fps=12f;
        [Tooltip("Opacity when the warning appears.")]
        [SerializeField, Range(0f,1f)] private float startAlpha=.2f;
        [Tooltip("Opacity at the moment the attack lands.")]
        [SerializeField, Range(0f,1f)] private float impactAlpha=.7f;
        [Tooltip("Sorting order above the monster's own sprite.")]
        [SerializeField] private int sortingOffset=2;
        public Sprite[] Frames => frames;
        public Sprite Sprite => frames!=null && frames.Length>0 ? frames[0] : null;
        public float Fps => fps;
        public float StartAlpha => startAlpha;
        public float ImpactAlpha => impactAlpha;
        public int SortingOffset => sortingOffset;
        public float Alpha(float urgency) => Mathf.Lerp(startAlpha,impactAlpha,Mathf.Clamp01(urgency));
        public Sprite FrameAt(double time)
        {
            if(frames==null || frames.Length==0)return null;
            long index=(long)System.Math.Floor(time*fps);
            return frames[(int)(((index%frames.Length)+frames.Length)%frames.Length)];
        }
    }
}
