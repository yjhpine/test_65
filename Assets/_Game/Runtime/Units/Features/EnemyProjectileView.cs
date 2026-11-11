using System.Collections.Generic;
using ActionPlatformer.Presentation;
using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    // Draws EnemyProjectiles' shots, blasts and pops with pooled renderers. The look comes from the first barrage
    // visual that registers one (all of them share Data/Presentation/EnemyProjectiles); without it nothing is drawn.
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyProjectiles))]
    public sealed class EnemyProjectileView : MonoBehaviour
    {
        private static EnemyProjectileProfile look;
        private static int sortingLayer;
        private static Material material;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { look = null; sortingLayer = 0; material = null; }
        // The shooter's own sprite material and layer, so shots are lit like the monsters that fire them.
        public static void Use(EnemyProjectileProfile profile, SpriteRenderer source)
        {
            if (profile == null || source == null) return;
            look = profile; sortingLayer = source.sortingLayerID; material = source.sharedMaterial;
            var field = EnemyProjectiles.Existing;
            if (field != null) field.PopDuration = profile.PopDuration;
        }

        private EnemyProjectiles field;
        private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        public int VisibleCount { get; private set; }
        private void Awake()
        {
            field = GetComponent<EnemyProjectiles>();
            if (look != null) field.PopDuration = look.PopDuration;
        }
        private void LateUpdate()
        {
            int used = 0;
            if (look != null)
            {
                // Game time: shots stop animating with the world (pause, hit stop).
                double now = Time.timeAsDouble;
                foreach (var shot in field.Shots)
                    Place(ref used, EnemyProjectileProfile.At(look.Frames((int)shot.Kind), now + shot.Owner * .37, look.ShotFps), shot.Position, 0);
                foreach (var blast in field.Blasts)
                    Place(ref used, EnemyProjectileProfile.Over(look.BlastFrames, blast.Duration <= 0f ? 1f : blast.Age / blast.Duration), blast.Center, 1);
                foreach (var pop in field.Pops)
                    Place(ref used, EnemyProjectileProfile.Over(look.PopFrames, pop.Age / field.PopDuration), pop.Position, 2);
            }
            for (int i = used; i < pool.Count; i++) pool[i].enabled = false;
            VisibleCount = used;
        }
        private void Place(ref int used, Sprite sprite, Vector2 position, int order)
        {
            if (sprite == null) return;
            if (used == pool.Count)
            {
                var go = new GameObject("Enemy shot"); go.transform.SetParent(transform, false);
                pool.Add(go.AddComponent<SpriteRenderer>());
            }
            var r = pool[used++];
            if (material != null && r.sharedMaterial != material) r.sharedMaterial = material;
            r.enabled = true; r.sprite = sprite; r.sortingLayerID = sortingLayer; r.sortingOrder = look.SortingOrder + order;
            r.transform.position = new Vector3(position.x, position.y, 0f);
        }
    }
}
