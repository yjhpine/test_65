#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ActionPlatformer.Presentation;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    // The shared red danger zone over shield soldiers' and ranged enemies' coming attacks.
    public sealed class AttackZoneTests
    {
        private readonly List<Object> created = new List<Object>();
        private sealed class State : IEnemyActionState
        {
            public EnemyPhase Phase { get; set; } = EnemyPhase.Idle;
            public float Elapsed { get; set; }
            public float Progress { get; set; }
            public float Facing { get; set; } = 1;
            public Vector2 Aim { get; set; } = Vector2.right;
            public bool Vertical { get; set; }
        }

        [TearDown] public void Cleanup()
        {
            for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        private IEnumerator Spawn(string name, Vector2 position, System.Action<EnemyCombat2D, EnemyVisual2D, State> use)
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/" + name + ".prefab"), position, Quaternion.identity);
            created.Add(go); yield return null;
            go.GetComponent<Unit>().Fsm.Stop();
            var combat = go.GetComponent<EnemyCombat2D>();
            var state = new State(); combat.Bind(combat.Settings, state);
            use(combat, go.GetComponentInChildren<EnemyVisual2D>(), state);
        }

        // Runs the visual then the manager, as one frame's LateUpdate would.
        private static SpriteRenderer Zone(EnemyVisual2D visual)
        {
            visual.SendMessage("LateUpdate"); EffectsRuntime.Vfx.SendMessage("LateUpdate");
            return visual.AttackZoneRoot == null ? null : visual.AttackZoneRoot.Find("Attack zone").GetComponent<SpriteRenderer>();
        }
        private static bool Shown(EnemyVisual2D visual) { var zone = Zone(visual); return zone != null && zone.enabled; }
        private static float Alpha(float urgency) => Mathf.Lerp(.2f, .7f, urgency);

        [UnityTest] public IEnumerator ShieldPreparationCoversItsAttackAreaAndDarkensTowardTheHit()
        {
            foreach (string name in new[] { "ShieldFront", "ShieldSoldier" })
                yield return Spawn(name, new Vector2(0, 100), (combat, visual, state) =>
                {
                    Assert.That(Shown(visual), Is.False, name + " idle");
                    foreach (float facing in new[] { 1f, -1f })
                    foreach (float progress in new[] { 0f, .5f, 1f })
                    {
                        state.Phase = EnemyPhase.Preparing; state.Facing = facing; state.Progress = progress; state.Elapsed = progress * .4f;
                        var zone = Zone(visual);
                        Assert.That(zone.enabled, Is.True, name);
                        combat.ShieldAttackArea(facing, out var center, out var size);
                        Assert.That(Vector2.Distance(zone.bounds.center, center), Is.LessThan(.002f), name);
                        Assert.That(zone.bounds.size.x, Is.EqualTo(size.x).Within(.002f), name);
                        Assert.That(zone.bounds.size.y, Is.EqualTo(size.y).Within(.002f), name);
                        Assert.That(zone.color.a, Is.EqualTo(Alpha(progress)).Within(.002f), name + " progress " + progress);
                    }
                    // The old orange floor line is gone.
                    Assert.That(visual.Effects.GetSessionRoot(visual.EffectOwner).Find("Attack telegraph").GetComponent<SpriteRenderer>().enabled, Is.False);
                    foreach (var phase in new[] { EnemyPhase.Dash, EnemyPhase.Strike, EnemyPhase.Recovery, EnemyPhase.Idle })
                    { state.Phase = phase; Assert.That(Shown(visual), Is.False, name + " " + phase); }
                });
        }

        [UnityTest] public IEnumerator RangedWarningIsTheBeamStripAndDarkensThroughTheAimLock()
        {
            foreach (string name in new[] { "SurveillanceTurret", "FlyingDrone" })
                yield return Spawn(name, new Vector2(0, 100), (combat, visual, state) =>
                {
                    float lockTime = combat.Settings.LockDuration;
                    Assert.That(lockTime, Is.GreaterThan(0f), name);
                    state.Aim = new Vector2(1f, .3f);
                    float last = -1f;
                    // Preparation of 1 s, then the lock: one ramp from 0 at the start to 1 when the beam fires.
                    var samples = new (EnemyPhase phase, float elapsed, float progress, float urgency)[] {
                        (EnemyPhase.Preparing, 0f, 0f, 0f), (EnemyPhase.Preparing, .5f, .5f, .5f / (1f + lockTime)),
                        (EnemyPhase.Locked, lockTime * .5f, .5f, (1f + lockTime * .5f) / (1f + lockTime)), (EnemyPhase.Locked, lockTime, 1f, 1f) };
                    foreach (var sample in samples)
                    {
                        state.Phase = sample.phase; state.Elapsed = sample.elapsed; state.Progress = sample.progress;
                        var zone = Zone(visual);
                        Assert.That(zone.enabled, Is.True, name + " " + sample.phase);
                        var beam = combat.CalculateBeam(state.Aim, false);
                        Vector2 delta = beam.End - beam.Start;
                        Assert.That(Vector2.Distance(zone.transform.position, (beam.Start + beam.End) * .5f), Is.LessThan(.002f), name);
                        Assert.That(zone.size.x * zone.transform.lossyScale.x, Is.EqualTo(delta.magnitude).Within(.002f), name);
                        Assert.That(zone.size.y * zone.transform.lossyScale.y, Is.EqualTo(beam.Width).Within(.002f), name);
                        Assert.That(Mathf.DeltaAngle(zone.transform.eulerAngles.z, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg), Is.EqualTo(0f).Within(.05f), name);
                        Assert.That(zone.color.a, Is.EqualTo(Alpha(sample.urgency)).Within(.002f), name + " " + sample.phase + " " + sample.elapsed);
                        Assert.That(zone.color.a, Is.GreaterThan(last), "never fades back");
                        last = zone.color.a;
                    }
                    Assert.That(last, Is.EqualTo(.7f).Within(.002f), name + " reaches full warning as it fires");
                    Assert.That(visual.Effects.GetSessionRoot(visual.EffectOwner).Find("Attack telegraph").GetComponent<SpriteRenderer>().enabled, Is.False,
                        "the old solid line is not drawn under the zone");
                    state.Phase = EnemyPhase.Recovery; Assert.That(Shown(visual), Is.False, name + " recovery");
                });
        }

        [UnityTest] public IEnumerator DisablingTheEnemyHidesItsZone()
        {
            GameObject owner = null; EnemyVisual2D view = null;
            yield return Spawn("ShieldFront", new Vector2(0, 100), (combat, visual, state) =>
            {
                state.Phase = EnemyPhase.Preparing; state.Progress = .5f; state.Elapsed = .2f;
                Assert.That(Shown(visual), Is.True);
                owner = combat.gameObject; view = visual;
            });
            var zone = view.AttackZoneRoot.Find("Attack zone").GetComponent<SpriteRenderer>();
            owner.SetActive(false);
            EffectsRuntime.Vfx.SendMessage("LateUpdate");
            Assert.That(zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy, Is.True);
        }
    }
}
#endif
