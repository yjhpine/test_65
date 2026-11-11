using UnityEngine;
namespace ActionPlatformer.Presentation
{
    // Look of the barrage family's shots, drop blasts and shot pops. Each shot sprite is drawn at its art size
    // (PixelFX, 18 px per unit), authored to the shot's collision diameter; nothing here changes damage or motion.
    [CreateAssetMenu(menuName = "Action Platformer/Presentation/Enemy Projectile Profile")]
    public sealed class EnemyProjectileProfile : ScriptableObject
    {
        [SerializeField] private Sprite[] boltFrames;
        [SerializeField] private Sprite[] pelletFrames;
        [SerializeField] private Sprite[] heavyFrames;
        [SerializeField] private Sprite[] shellFrames;
        [SerializeField, Min(.1f)] private float shotFps = 12f;
        [Tooltip("The mortar's ground blast; the frames are spread over the blast's own duration.")]
        [SerializeField] private Sprite[] blastFrames;
        [Tooltip("Shot removed by a wall, the player or a direct mortar hit (presentation only).")]
        [SerializeField] private Sprite[] popFrames;
        [SerializeField, Min(.01f)] private float popDuration = .12f;
        [SerializeField] private int sortingOrder = 12;
        public float ShotFps => shotFps;
        public Sprite[] BlastFrames => blastFrames;
        public Sprite[] PopFrames => popFrames;
        public float PopDuration => popDuration;
        public int SortingOrder => sortingOrder;
        public Sprite[] Frames(int kind) => kind == 0 ? boltFrames : kind == 1 ? pelletFrames : kind == 2 ? heavyFrames : shellFrames;
        public static Sprite At(Sprite[] frames, double time, float fps)
        {
            if (frames == null || frames.Length == 0) return null;
            long index = (long)System.Math.Floor(System.Math.Max(0, time) * fps);
            return frames[(int)(index % frames.Length)];
        }
        public static Sprite Over(Sprite[] frames, float progress)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[Mathf.Clamp((int)(Mathf.Clamp01(progress) * frames.Length), 0, frames.Length - 1)];
        }
    }
}
