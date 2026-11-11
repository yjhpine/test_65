using System.Linq;
using ActionPlatformer.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    // The player is always drawn on top of every enemy, NPC and enemy effect (2026-11-05, user request).
    public sealed class RenderOrderTests
    {
        // Runtime renderers that enemies add above their own sprite, by code:
        // EnemyBeamEffects / VfxManager shapes +2, turret head +1, hit contacts on the victim +1.
        private const int EnemyRuntimeOffset = 2;
        // EnemyProjectileView draws shots at the profile order + 0, blasts + 1, pops + 2.
        private const int ProjectileRuntimeOffset = 2;

        private static SpriteRenderer PlayerSprite()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab");
            return player.transform.Find("Visual").GetComponent<SpriteRenderer>();
        }

        private static Renderer[] OtherUnitRenderers()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Enemies", "Assets/_Game/Prefabs/Bosses" })
                .Select(AssetDatabase.GUIDToAssetPath).Append("Assets/_Game/Cinematics/Guide.prefab").ToArray();
            Assert.That(paths.Length, Is.GreaterThan(10), "enemy, boss and NPC prefabs");
            return paths.SelectMany(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<Renderer>(true)).ToArray();
        }

        [Test] public void PlayerSpriteSortsAboveEveryEnemyAndNpcRenderer()
        {
            var player = PlayerSprite();
            foreach (var renderer in OtherUnitRenderers())
            {
                string name = renderer.transform.root.name + "/" + renderer.name;
                Assert.That(renderer.sortingLayerID, Is.EqualTo(player.sortingLayerID), name + ": one sorting layer, ordered by number");
                Assert.That(renderer.sortingOrder + EnemyRuntimeOffset, Is.LessThan(player.sortingOrder), name);
            }
        }

        [Test] public void PlayerSpriteSortsAboveEnemyTelegraphsFieldsAndShots()
        {
            int player = PlayerSprite().sortingOrder;
            int enemyTop = OtherUnitRenderers().Max(r => r.sortingOrder);
            foreach (var path in new[] { "Assets/_Game/Data/Presentation/AttackZone.asset", "Assets/_Game/Data/Presentation/DomainField.asset" })
                Assert.That(enemyTop + AssetDatabase.LoadAssetAtPath<AttackZoneProfile>(path).SortingOffset, Is.LessThan(player), path);
            var shots = AssetDatabase.LoadAssetAtPath<EnemyProjectileProfile>("Assets/_Game/Data/Presentation/EnemyProjectiles.asset");
            Assert.That(shots.SortingOrder + ProjectileRuntimeOffset, Is.LessThan(player), "enemy shots, blasts and pops");
            // The player's own dust is drawn one below the player and must stay above enemies too.
            Assert.That(player - 1, Is.GreaterThan(enemyTop + EnemyRuntimeOffset));
        }
    }
}
