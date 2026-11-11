using ActionPlatformer.Presentation;
using ActionPlatformer.Units.Bosses;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class AttackZoneAssetTests
    {
        private const string ProfilePath = "Assets/_Game/Data/Presentation/AttackZone.asset";

        [Test] public void AttackZoneIsALoopingRedGlitchTileOnThePlayerGrid()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AttackZoneProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null, ProfilePath);
            Assert.That(profile.Frames, Has.Length.EqualTo(8));
            Assert.That(profile.Fps, Is.EqualTo(12f).Within(1e-4f));
            var distinct = new System.Collections.Generic.HashSet<string>();
            foreach (var sprite in profile.Frames)
            {
                Assert.That(sprite, Is.Not.Null);
                string path = AssetDatabase.GetAssetPath(sprite);
                Assert.That(path, Does.StartWith("Assets/_Game/Art/Effects/PixelFxAttackZone/"));
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(32, 32)), path);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(18f), path);
                Assert.That(sprite.border, Is.EqualTo(new Vector4(1, 1, 1, 1)), path + ": the 1 px rim must not stretch.");
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), "Tiled drawing needs a full-rect mesh.");
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                var art = new Texture2D(2, 2);
                try
                {
                    Assert.That(art.LoadImage(System.IO.File.ReadAllBytes(path)), Is.True);
                    int holes = 0;
                    // Reds only: no teal or any other hue where green or blue outweighs red.
                    foreach (var pixel in art.GetPixels())
                    {
                        if (pixel.a < .5f) { holes++; continue; }
                        Assert.That(pixel.r, Is.GreaterThanOrEqualTo(pixel.g), path); Assert.That(pixel.r, Is.GreaterThanOrEqualTo(pixel.b), path);
                    }
                    Assert.That(holes, Is.GreaterThan(0).And.LessThan(32 * 32 / 10), path + ": a few missing pixels, not a hollow tile");
                    distinct.Add(System.Convert.ToBase64String(art.EncodeToPNG()));
                }
                finally { Object.DestroyImmediate(art); }
            }
            Assert.That(distinct.Count, Is.EqualTo(8), "every frame of the loop differs");
            Assert.That(profile.StartAlpha, Is.EqualTo(.2f).Within(1e-6f));
            Assert.That(profile.ImpactAlpha, Is.EqualTo(.7f).Within(1e-6f));
            Assert.That(profile.Alpha(.5f), Is.EqualTo(.45f).Within(1e-6f));
        }

        [Test] public void FramesAdvanceWithTimeAndLoop()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AttackZoneProfile>(ProfilePath);
            var frames = profile.Frames; float step = 1f / profile.Fps;
            Assert.That(profile.FrameAt(0), Is.SameAs(frames[0]));
            Assert.That(profile.FrameAt(step * 1.5), Is.SameAs(frames[1]));
            Assert.That(profile.FrameAt(step * 7.5), Is.SameAs(frames[7]));
            Assert.That(profile.FrameAt(step * 8.5), Is.SameAs(frames[0]), "loops");
            Assert.That(profile.FrameAt(1000.0 + step * .5), Is.SameAs(profile.FrameAt(1000.0 + step * .5 - step * 8)));
        }

        [Test] public void EveryMonsterVisualUsesTheSharedAttackZone()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AttackZoneProfile>(ProfilePath);
            foreach (string name in new[] { "Enemies/ShieldSoldier", "Enemies/ShieldFront", "Enemies/SurveillanceTurret", "Enemies/FlyingDrone", "Enemies/PatrolEnemy", "Bosses/TestBoss" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/" + name + ".prefab");
                Assert.That(prefab, Is.Not.Null, name);
                MonoBehaviour visual = prefab.GetComponentInChildren<EnemyVisual2D>(true);
                if (visual == null) visual = prefab.GetComponentInChildren<BossVisual2D>(true);
                if (visual == null) visual = prefab.GetComponentInChildren<UnitVisual2D>(true);
                Assert.That(visual, Is.Not.Null, name);
                Assert.That(new SerializedObject(visual).FindProperty("attackZone").objectReferenceValue, Is.SameAs(profile), name);
            }
        }
    }
}
