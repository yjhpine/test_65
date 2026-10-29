#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    public sealed class EnemyHitVisualTests
    {
        private GameObject enemy;
        [TearDown] public void Cleanup() { if (enemy != null) Object.DestroyImmediate(enemy); }
        private void Spawn(string name)
        {
            enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/" + name + ".prefab"), new Vector3(300, 10, 0), Quaternion.identity);
        }
        [UnityTest] public IEnumerator TurretVisiblePartsFlashAndClearAfterDamage()
        {
            Spawn("SurveillanceTurret"); yield return null;
            var health = enemy.GetComponent<UnitHealth>();
            health.ApplyDamage(1);
            foreach (var flash in enemy.GetComponentsInChildren<UnitDamageFlash>()) flash.SendMessage("LateUpdate");
            var parts = enemy.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == "Turret head" || r.name == "Turret pedestal").ToArray();
            Assert.That(parts.Length, Is.EqualTo(2));
            foreach (var part in parts) { var p = new MaterialPropertyBlock(); part.GetPropertyBlock(p); Assert.That(p.GetFloat("_HitFlash"), Is.GreaterThan(0)); Assert.That(part.enabled, Is.True); }
            yield return new WaitForSecondsRealtime(.1f);
            foreach (var part in parts) { var p = new MaterialPropertyBlock(); part.GetPropertyBlock(p); Assert.That(p.GetFloat("_HitFlash"), Is.EqualTo(0)); }
        }
        [UnityTest] public IEnumerator DroneRecoilLeavesBodyAndCombatUntouchedAndResets()
        {
            Spawn("FlyingDrone"); yield return null;
            var visual = enemy.GetComponentInChildren<EnemyVisual2D>();
            var health = enemy.GetComponent<UnitHealth>();
            var combat = enemy.GetComponent<EnemyCombat2D>();
            var original = visual.transform.localRotation;
            var position = enemy.transform.position;
            var phase = combat.State.Phase;
            health.ApplyDirectedDamage(1, Vector2.right);
            visual.SendMessage("LateUpdate");
            Assert.That(Quaternion.Angle(original, visual.transform.localRotation), Is.GreaterThan(1));
            Assert.That(enemy.transform.position, Is.EqualTo(position));
            Assert.That(combat.State.Phase, Is.EqualTo(phase));
            yield return new WaitForSecondsRealtime(.16f);
            Assert.That(Quaternion.Angle(original, visual.transform.localRotation), Is.LessThan(.01f));
            health.ApplyDirectedDamage(1, Vector2.left); visual.SendMessage("LateUpdate");
            visual.enabled = false;
            Assert.That(Quaternion.Angle(original, visual.transform.localRotation), Is.LessThan(.01f));
            visual.enabled = true; visual.SendMessage("LateUpdate");
            Assert.That(Quaternion.Angle(original, visual.transform.localRotation), Is.LessThan(.01f));
            health.ApplyDamage(health.CurrentHealth); visual.SendMessage("LateUpdate");
            Assert.That(Quaternion.Angle(original, visual.transform.localRotation), Is.LessThan(.01f));
        }
    }
}
#endif
