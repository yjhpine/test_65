using UnityEngine;
namespace ActionPlatformer.Presentation
{
    [CreateAssetMenu(menuName="Action Platformer/Presentation/Enemy SFX")]
    public sealed class EnemySfxProfile : ScriptableObject
    {
        [SerializeField] private AudioClip shotSound;
        [SerializeField, Range(0,1)] private float shotVolume = .35f;
        public AudioClip ShotSound => shotSound;
        public float ShotVolume => shotVolume;
    }
}
