using ActionPlatformer.Presentation;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using ActionPlatformer.Units.Fsm;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    // The 7 barrage/laser/domain enemies (2026-11-05): values converted from the design documents at 47.5 px per unit.
    public sealed class RangedEnemyAssetTests
    {
        private static readonly (string name, RangedAttack attack)[] Kinds =
        {
            ("RangedStraight", RangedAttack.Straight), ("RangedShotgun", RangedAttack.Shotgun), ("RangedHeavy", RangedAttack.Heavy),
            ("RangedMortar", RangedAttack.Mortar), ("SweepLaser", RangedAttack.SweepLaser), ("DomainSelf", RangedAttack.SelfDomain),
            ("DomainFixed", RangedAttack.FixedDomain),
        };
        private static RangedFsmDefinition Fsm(string name) => AssetDatabase.LoadAssetAtPath<RangedFsmDefinition>("Assets/_Game/Data/Fsm/" + name + "Fsm.asset");

        [Test] public void EveryKindHasItsDefinitionFsmAndPrefab()
        {
            var zone = AssetDatabase.LoadAssetAtPath<AttackZoneProfile>("Assets/_Game/Data/Presentation/AttackZone.asset");
            var field = AssetDatabase.LoadAssetAtPath<AttackZoneProfile>("Assets/_Game/Data/Presentation/DomainField.asset");
            var shots = AssetDatabase.LoadAssetAtPath<EnemyProjectileProfile>("Assets/_Game/Data/Presentation/EnemyProjectiles.asset");
            foreach (var (name, attack) in Kinds)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/Ranged/" + name + ".prefab");
                Assert.That(prefab, Is.Not.Null, name);
                Assert.That(prefab.layer, Is.EqualTo(2), name);
                var definition = prefab.GetComponent<Unit>().Definition;
                Assert.That(definition.Kind, Is.EqualTo(UnitKind.Monster), name);
                Assert.That(definition.MaxHealth, Is.EqualTo(30), name + ": three basic hits of 10");
                Assert.That(definition.AllowGlitchTarget, Is.True, name); Assert.That(definition.AllowForcedMovement, Is.True, name);
                var fsm = definition.Fsm as RangedFsmDefinition;
                Assert.That(fsm, Is.SameAs(Fsm(name)), name); Assert.That(fsm.Attack, Is.EqualTo(attack), name);
                Assert.That(fsm.TryValidate(out string error), Is.True, error);
                Assert.That(prefab.GetComponent<RangedCombat2D>(), Is.Not.Null, name);
                Assert.That(prefab.GetComponent<UnitHealth>().GetType().GetField("deactivateOnDeath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(prefab.GetComponent<UnitHealth>()), Is.False, name);
                if (attack == RangedAttack.Mortar) Assert.That(prefab.GetComponent<FlyingMovement2D>(), Is.Not.Null);
                else
                {
                    var ground = new SerializedObject(prefab.GetComponent<GroundMovement2D>());
                    Assert.That(ground.FindProperty("ignoreKnockback").boolValue, Is.True, name + ": basic hits do not push");
                }
                var visual = prefab.GetComponentInChildren<RangedVisual2D>();
                Assert.That(visual, Is.Not.Null, name);
                Assert.That(visual.GetComponent<UnitDamageFlash>(), Is.Not.Null, name);
                var data = new SerializedObject(visual);
                Assert.That(data.FindProperty("stateSource").objectReferenceValue, Is.SameAs(prefab.GetComponent<RangedCombat2D>()), name);
                Assert.That(data.FindProperty("attackZone").objectReferenceValue, Is.SameAs(zone), name);
                Assert.That(data.FindProperty("projectileLook").objectReferenceValue, Is.SameAs(shots), name);
                bool domain = attack == RangedAttack.SelfDomain || attack == RangedAttack.FixedDomain;
                Assert.That(data.FindProperty("domainField").objectReferenceValue, domain ? Is.SameAs(field) : Is.Null, name);
                Assert.That(data.FindProperty("laserLook").objectReferenceValue != null, Is.EqualTo(attack == RangedAttack.SweepLaser), name);
            }
        }

        [Test] public void DesignValuesAreConvertedFromPixels()
        {
            foreach (var (name, _) in Kinds)
            {
                var f = Fsm(name);
                Assert.That(f.DetectionRange, Is.EqualTo(13.05f).Within(.001f), name + ": 620 px");
                Assert.That(f.RecognitionDelay, Is.EqualTo(.2f).Within(1e-5f)); Assert.That(f.Telegraph, Is.EqualTo(.4f).Within(1e-5f));
                Assert.That(f.AttackInterval, Is.EqualTo(5f).Within(1e-5f)); Assert.That(f.Damage, Is.EqualTo(10));
                Assert.That(f.ShotLifetime, Is.EqualTo(6f).Within(1e-5f));
            }
            var straight = Fsm("RangedStraight"); var shotgun = Fsm("RangedShotgun"); var heavy = Fsm("RangedHeavy"); var mortar = Fsm("RangedMortar");
            Assert.That(straight.ShotSpeed, Is.EqualTo(4.42f).Within(.001f)); Assert.That(straight.ShotRadius, Is.EqualTo(.295f).Within(.001f)); Assert.That(straight.LightHit, Is.True);
            Assert.That(shotgun.ShotSpeed, Is.EqualTo(4.42f).Within(.001f)); Assert.That(shotgun.ShotRadius, Is.EqualTo(.21f).Within(.001f));
            Assert.That(shotgun.SpreadAngle, Is.EqualTo(12f).Within(1e-4f)); Assert.That(shotgun.LightHit, Is.True);
            Assert.That(heavy.ShotSpeed, Is.EqualTo(2.21f).Within(.001f)); Assert.That(heavy.ShotRadius, Is.EqualTo(.505f).Within(.001f)); Assert.That(heavy.LightHit, Is.False);
            Assert.That(mortar.ShotSpeed, Is.EqualTo(7.58f).Within(.001f)); Assert.That(mortar.ShotRadius, Is.EqualTo(.379f).Within(.001f)); Assert.That(mortar.LightHit, Is.False);
            Assert.That(mortar.TrackSpeed, Is.EqualTo(3.79f).Within(.001f)); Assert.That(mortar.AlignDistance, Is.EqualTo(1.26f).Within(.001f));
            Assert.That(mortar.BlastRadius, Is.EqualTo(1.26f).Within(.001f)); Assert.That(mortar.BlastDuration, Is.EqualTo(.12f).Within(1e-5f));
            var laser = Fsm("SweepLaser");
            Assert.That(laser.SweepAngleRange, Is.EqualTo(60f).Within(1e-4f)); Assert.That(laser.SweepDuration, Is.EqualTo(.8f).Within(1e-5f));
            Assert.That(laser.Afterimage, Is.EqualTo(.15f).Within(1e-5f)); Assert.That(laser.LaserLength, Is.EqualTo(13.05f).Within(.001f));
            Assert.That(laser.LaserWidth, Is.EqualTo(.21f).Within(.001f));
            foreach (var name in new[] { "DomainSelf", "DomainFixed" })
            {
                Assert.That(Fsm(name).DomainSize.x, Is.EqualTo(5.05f).Within(.001f), "240 px"); Assert.That(Fsm(name).DomainSize.y, Is.EqualTo(4.21f).Within(.001f), "200 px");
                Assert.That(Fsm(name).DomainDuration, Is.EqualTo(2f).Within(1e-5f));
            }
        }

        [Test] public void ShotsBlastAndFieldArePixelFxFramesWithoutTeal()
        {
            var shots = AssetDatabase.LoadAssetAtPath<EnemyProjectileProfile>("Assets/_Game/Data/Presentation/EnemyProjectiles.asset");
            var expected = new[] { (shots.Frames(0), 11, 11, 4), (shots.Frames(1), 9, 9, 4), (shots.Frames(2), 19, 19, 6), (shots.Frames(3), 13, 19, 4),
                (shots.BlastFrames, 47, 47, 4), (shots.PopFrames, 11, 11, 4) };
            foreach (var (frames, w, h, count) in expected) Check(frames, w, h, count, true);
            var field = AssetDatabase.LoadAssetAtPath<AttackZoneProfile>("Assets/_Game/Data/Presentation/DomainField.asset");
            Check(field.Frames, 32, 32, 6, false);
            foreach (var sprite in field.Frames) Assert.That(sprite.border, Is.EqualTo(new Vector4(1, 1, 1, 1)), "drawn Tiled like the attack zone");
            Assert.That(field.Alpha(0f), Is.EqualTo(field.Alpha(1f)).Within(1e-6f), "a steady field");
            Assert.That(field.SortingOffset, Is.LessThan(AssetDatabase.LoadAssetAtPath<AttackZoneProfile>("Assets/_Game/Data/Presentation/AttackZone.asset").SortingOffset));
        }

        private static void Check(Sprite[] frames, int width, int height, int count, bool reds)
        {
            Assert.That(frames, Has.Length.EqualTo(count));
            foreach (var sprite in frames)
            {
                string path = AssetDatabase.GetAssetPath(sprite);
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(width, height)), path);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(18f), path);
                var art = new Texture2D(2, 2);
                try
                {
                    Assert.That(art.LoadImage(System.IO.File.ReadAllBytes(path)), Is.True);
                    foreach (var pixel in art.GetPixels())
                    {
                        if (pixel.a < .5f) continue;
                        // No teal (the player's glitch color): green never outweighs red; shots are reds and magenta only.
                        Assert.That(pixel.r, Is.GreaterThanOrEqualTo(pixel.g), path);
                        if (reds) Assert.That(pixel.r, Is.GreaterThanOrEqualTo(pixel.b), path);
                    }
                }
                finally { Object.DestroyImmediate(art); }
            }
        }

        [Test] public void TestSceneHoldsOneOfEachAndStaysOutOfTheBuild()
        {
            const string path = "Assets/_Game/Scenes/RangedEnemyTest.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
            foreach (var scene in EditorBuildSettings.scenes) Assert.That(scene.path, Is.Not.EqualTo(path));
            string text = System.IO.File.ReadAllText(path);
            foreach (var (name, _) in Kinds)
                StringAssert.Contains(AssetDatabase.AssetPathToGUID("Assets/_Game/Prefabs/Enemies/Ranged/" + name + ".prefab"), text, name);
            StringAssert.DoesNotContain(AssetDatabase.AssetPathToGUID("Assets/_Game/Prefabs/Bosses/TestBoss.prefab"), text);
        }
    }
}
