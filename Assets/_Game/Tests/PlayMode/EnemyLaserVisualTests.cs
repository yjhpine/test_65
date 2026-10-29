#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    public sealed class EnemyLaserVisualTests
    {
        private readonly List<Object> created = new List<Object>();
        private sealed class State : IEnemyActionState
        {
            public EnemyPhase Phase { get; set; } = EnemyPhase.Idle;
            public float Elapsed => 0;
            public float Progress => .5f;
            public float Facing => 1;
            public Vector2 Aim { get; set; } = Vector2.right;
            public bool Vertical { get; set; }
        }
        private GameObject Spawn(string name, Vector2 position)
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/" + name + ".prefab"), position, Quaternion.identity);
            created.Add(go); return go;
        }
        private static SpriteRenderer Part(EnemyLaserVisual view, string label)
        { ActionPlatformer.Presentation.EffectsRuntime.Vfx.SendMessage("LateUpdate"); return view.SessionRoot.Find("Laser " + label).GetComponent<SpriteRenderer>(); }
        private static void AssertHidden(EnemyLaserVisual view)
        {
            ActionPlatformer.Presentation.EffectsRuntime.Vfx.SendMessage("LateUpdate");
            if(view.SessionRoot==null)return;
            foreach (var renderer in view.SessionRoot.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.name.StartsWith("Laser ")) Assert.That(renderer.enabled, Is.False, renderer.name);
        }
        [TearDown] public void Cleanup()
        {
            for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [UnityTest] public IEnumerator RangedPrefabsUseNativeColorsAndFrozenClippedShotGeometry()
        {
            foreach (string name in new[] { "SurveillanceTurret", "FlyingDrone" })
            {
                var go = Spawn(name, new Vector2(0, 100)); yield return null;
                go.GetComponent<Unit>().Fsm.Stop();
                var combat = go.GetComponent<EnemyCombat2D>();
                var state = new State(); combat.Bind(combat.Settings, state);
                var view = go.GetComponentInChildren<EnemyLaserVisual>();
                var visual = view.GetComponent<EnemyVisual2D>();
                string color = name == "SurveillanceTurret" ? "blue" : "red";
                var wall = new GameObject("Laser clipping wall"); created.Add(wall);
                wall.transform.position = new Vector3(4, 100); wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 6);
                Physics2D.SyncTransforms();
                foreach (var aim in new[] { Vector2.right, new Vector2(1, .3f), Vector2.left })
                {
                    state.Aim = aim; combat.Fire(aim, false); visual.SendMessage("LateUpdate");
                    var shot = combat.LastShot;
                    Assert.That(Part(view, "start 0").enabled && Part(view, "end 0").enabled, Is.True);
                    Assert.That(Part(view, "start 1").enabled, Is.False);
                    Assert.That(view.SessionRoot.Find("Attack telegraph").GetComponent<SpriteRenderer>().enabled, Is.False, "No solid placeholder over the imported beam.");
                    Assert.That(Vector2.Distance(Part(view, "start 0").transform.position, shot.Start), Is.LessThan(.001f));
                    Assert.That(Vector2.Distance(Part(view, "end 0").transform.position, shot.End), Is.LessThan(.001f));
                    if (aim.x > 0) Assert.That(shot.End.x, Is.EqualTo(3.9f).Within(.002f));
                    foreach (float progress in new[] { 0f, .5f, .9f })
                    {
                        view.Hide(); view.ShowShot(shot, combat.Center, false, progress);
                        var start = Part(view, "start 0"); var body = Part(view, "body 0"); var end = Part(view, "end 0");
                        StringAssert.Contains("_" + color + "_", start.sprite.name);
                        StringAssert.Contains("_" + color + "_", body.sprite.name);
                        Assert.That(body.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
                        Assert.That(body.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                        int startFrame = int.Parse(start.sprite.name.Substring(start.sprite.name.Length - 2));
                        int bodyFrame = int.Parse(body.sprite.name.Substring(body.sprite.name.Length - 2));
                        Assert.That(startFrame - 8, Is.EqualTo(bodyFrame));
                        Assert.That(end.sprite.name.Substring(end.sprite.name.Length - 2), Is.EqualTo(bodyFrame.ToString("00")));
                        float length = (start.sprite.rect.width - start.sprite.pivot.x) / start.sprite.pixelsPerUnit * start.transform.lossyScale.x +
                            body.size.x * body.transform.lossyScale.x + end.sprite.pivot.x / end.sprite.pixelsPerUnit * end.transform.lossyScale.x;
                        Assert.That(length, Is.EqualTo(Vector2.Distance(shot.Start, shot.End)).Within(.002f));
                    }
                    var frozen = Part(view, "start 0").transform.position;
                    state.Aim = Vector2.down; visual.SendMessage("LateUpdate");
                    Assert.That(Part(view, "start 0").transform.position, Is.EqualTo(frozen), "Changing tracking aim cannot move the shot.");
                }
                yield return new WaitForSeconds(combat.Settings.BeamDisplayDuration + .04f);
                AssertHidden(view);
                go.SetActive(false); go.SetActive(true); yield return null;
                AssertHidden(view);
                Object.DestroyImmediate(go); Object.DestroyImmediate(wall);
            }
        }

        [UnityTest] public IEnumerator DroneVerticalShotSplitsAtEmitterAndShortBeamsAndDeathHideCleanly()
        {
            var go = Spawn("FlyingDrone", new Vector2(0, 100)); yield return null;
            go.GetComponent<Unit>().Fsm.Stop();
            var combat = go.GetComponent<EnemyCombat2D>();
            var state = new State { Vertical = true, Phase = EnemyPhase.Preparing }; combat.Bind(combat.Settings, state);
            var view = go.GetComponentInChildren<EnemyLaserVisual>(); var visual = view.GetComponent<EnemyVisual2D>();
            visual.SendMessage("LateUpdate");
            Assert.That(Part(view, "start 0").enabled && Part(view, "start 1").enabled, Is.True);
            Assert.That(Part(view, "body 0").enabled || Part(view, "end 0").enabled, Is.False, "Charge has no damaging beam body.");
            state.Phase = EnemyPhase.Recovery; combat.Fire(Vector2.down, true); visual.SendMessage("LateUpdate");
            var shot = combat.LastShot;
            Assert.That(Vector2.Distance(Part(view, "start 0").transform.position, combat.Center), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(Part(view, "start 1").transform.position, combat.Center), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(Part(view, "end 0").transform.position, shot.Start), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(Part(view, "end 1").transform.position, shot.End), Is.LessThan(.001f));
            view.Hide(); view.ShowShot(new EnemyBeam(Vector2.zero, Vector2.right * .01f, .12f), Vector2.zero, false, .5f);
            Assert.That(Part(view, "start 0").enabled && Part(view, "end 0").enabled, Is.True);
            Assert.That(Part(view, "body 0").enabled, Is.False);
            view.Hide(); view.ShowShot(new EnemyBeam(Vector2.zero, Vector2.zero, .12f), Vector2.zero, false, .5f);
            AssertHidden(view);
            visual.SendMessage("LateUpdate");
            go.GetComponent<UnitHealth>().ApplyDamage(1000); visual.SendMessage("LateUpdate"); AssertHidden(view);
        }
    }
}
#endif
