#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ActionPlatformer.Presentation;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using ActionPlatformer.Units.Fsm;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    // Barrage (straight, shotgun, heavy, mortar), sweep laser and domain enemies (2026-11-05 design documents).
    public sealed class RangedEnemyTests
    {
        readonly List<Object> created = new List<Object>();
        Unit enemy; UnitHealth target; RangedFsmDefinition settings;
        RangedCombat2D Combat => enemy.GetComponent<RangedCombat2D>();
        IRangedActionState State => Combat.State;
        RangedPhase Phase => State.Phase;
        UnitHealth Health => enemy.GetComponent<UnitHealth>();
        EnemyProjectiles Field => EnemyProjectiles.Instance;
        static readonly WaitForFixedUpdate Step = new WaitForFixedUpdate();
        const float Floor = 50.5f;
        static void Set(Object obj, string name, object value)
        {
            var so = new SerializedObject(obj); var p = so.FindProperty(name);
            if (value is Object o) p.objectReferenceValue = o; else if (value is float f) p.floatValue = f; else if (value is int i) p.intValue = i; else if (value is bool b) p.boolValue = b;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        GameObject Block(Vector2 position, Vector2 size)
        {
            var go = new GameObject("Ranged test terrain"); created.Add(go); go.transform.position = position;
            go.AddComponent<BoxCollider2D>().size = size; Physics2D.SyncTransforms(); return go;
        }
        // Tests tick the FSM by hand right after this; the mortar tests instead wait on the unit's own frames.
        IEnumerator Spawn(string name, Vector2 targetAt, Vector2? targetSize = null)
        {
            Block(new Vector2(0, 50), new Vector2(80, 1));
            settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RangedFsmDefinition>("Assets/_Game/Data/Fsm/" + name + "Fsm.asset")); created.Add(settings);
            var def = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Game/Data/Units/" + name + "Definition.asset")); created.Add(def); Set(def, "fsm", settings);
            var parent = new GameObject("Ranged test setup"); parent.SetActive(false); created.Add(parent);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/Ranged/" + name + ".prefab");
            float y = name == "RangedMortar" ? Floor + 4f : Floor + prefab.GetComponent<BoxCollider2D>().size.y * .5f + .005f;
            var instance = Object.Instantiate(prefab, new Vector3(0, y), Quaternion.identity, parent.transform);
            enemy = instance.GetComponent<Unit>(); Set(enemy, "definition", def);
            var playerDefinition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Game/Data/Units/PlayerDefinition.asset")); created.Add(playerDefinition);
            Set(playerDefinition, "maxHealth", 1000);
            var player = new GameObject("Ranged test target"); player.SetActive(false); created.Add(player); player.layer = 2; player.transform.position = targetAt;
            Set(player.AddComponent<Unit>(), "definition", playerDefinition); player.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            player.AddComponent<BoxCollider2D>().size = targetSize ?? new Vector2(.6f, 1f);
            target = player.AddComponent<UnitHealth>(); Set(target, "deactivateOnDeath", false); player.AddComponent<EnemyRepositionProbe>();
            player.SetActive(true); parent.SetActive(true); yield return null; yield return Step; yield return Step; yield return null;
            enemy.Fsm.Stop(); enemy.Fsm.Start(); Physics2D.SyncTransforms();
        }
        void Tick(float dt = .01f) => enemy.Fsm.Tick(dt);
        void MoveTarget(Vector2 p) { target.GetComponent<Rigidbody2D>().position = p; target.transform.position = p; Physics2D.SyncTransforms(); }
        void MoveEnemy(Vector2 p) { enemy.GetComponent<Rigidbody2D>().position = p; enemy.transform.position = p; Physics2D.SyncTransforms(); }
        void Telegraph() { Tick(); Tick(.2f); Assert.That(Phase, Is.EqualTo(RangedPhase.Preparing), "recognized for the 0.2 s delay, then the telegraph"); }
        List<EnemyProjectiles.Shot> Mine()
        {
            var list = new List<EnemyProjectiles.Shot>();
            foreach (var s in Field.Shots) if (s.Owner == Combat.OwnerId) list.Add(s);
            return list;
        }
        void StepShots(int steps) { for (int i = 0; i < steps; i++) { Field.Step(.02f); Physics2D.SyncTransforms(); } }
        // Steps until no shot is left (the pop it leaves lasts only 0.12 s, so stop right there).
        void StepUntilGone(int max = 200) { for (int i = 0; i < max && Field.Shots.Count > 0; i++) { Field.Step(.02f); Physics2D.SyncTransforms(); } }
        [TearDown] public void Cleanup()
        {
            if (EnemyProjectiles.Existing != null) Object.DestroyImmediate(EnemyProjectiles.Existing.gameObject);
            for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [UnityTest] public IEnumerator StraightWaitsOnceThenFiresAlongTheAimFixedAtTheTelegraph()
        {
            yield return Spawn("RangedStraight", new Vector2(-6, 51));
            Tick(); Assert.That(Phase, Is.EqualTo(RangedPhase.Idle), "still within the first recognition delay");
            Tick(.2f); Assert.That(Phase, Is.EqualTo(RangedPhase.Preparing));
            Vector2 aim = ((Vector2)target.transform.position - Combat.Center).normalized;
            MoveTarget(new Vector2(-6, 54));
            Tick(.39f); Assert.That(Phase, Is.EqualTo(RangedPhase.Preparing), "the telegraph lasts 0.4 s");
            Tick(.02f); Assert.That(Phase, Is.EqualTo(RangedPhase.Idle));
            var shots = Mine(); Assert.That(shots.Count, Is.EqualTo(1));
            var shot = shots[0];
            Assert.That(Vector2.Angle(shot.Velocity, aim), Is.LessThan(.05f), "aimed where the player was when the telegraph began");
            Assert.That(shot.Velocity.magnitude, Is.EqualTo(4.42f).Within(.001f));
            Assert.That(shot.Radius, Is.EqualTo(.295f).Within(.0001f));
            Assert.That(shot.Light, Is.True); Assert.That(shot.Kind, Is.EqualTo(ProjectileKind.Bolt));
        }

        [UnityTest] public IEnumerator ShotgunFiresThreeSpreadShotsAndWaitsForAllOfThemAndTheInterval()
        {
            yield return Spawn("RangedShotgun", new Vector2(-6, 51));
            Telegraph(); Tick(.41f);
            var shots = Mine(); Assert.That(shots.Count, Is.EqualTo(3), "three at once, not a burst");
            Assert.That(Vector2.Angle(shots[0].Velocity, shots[1].Velocity), Is.EqualTo(12f).Within(.01f));
            Assert.That(Vector2.Angle(shots[1].Velocity, shots[2].Velocity), Is.EqualTo(12f).Within(.01f));
            foreach (var s in shots) { Assert.That(s.Radius, Is.EqualTo(.21f).Within(.0001f)); Assert.That(s.Light, Is.True); Assert.That(s.Kind, Is.EqualTo(ProjectileKind.Pellet)); }
            Tick(5.2f); Assert.That(Phase, Is.EqualTo(RangedPhase.Idle), "past the interval, but its shots are still flying");
            Field.ClearAll(); Tick();
            Assert.That(Phase, Is.EqualTo(RangedPhase.Preparing), "shots gone and 5 s passed: next telegraph, no second recognition delay");
        }

        [UnityTest] public IEnumerator ShotsAreRemovedOnContactEvenWhenTheyCannotHurt()
        {
            yield return Spawn("RangedStraight", new Vector2(-6, 51));
            ProjectileSpec Spec() => new ProjectileSpec { Kind = ProjectileKind.Bolt, Origin = new Vector2(-5, 51), Velocity = new Vector2(-4.42f, 0),
                Radius = .295f, Lifetime = 6, Damage = 10, Light = true, TargetMask = 4, ObstacleMask = 1, LandingY = float.NaN };
            Field.Spawn(1, Spec()); StepUntilGone();
            Assert.That(Field.Shots.Count, Is.Zero); Assert.That(Field.Pops.Count, Is.EqualTo(1), "contact pop");
            Assert.That(target.CurrentHealth, Is.EqualTo(990)); Assert.That(target.LastDamage.Light, Is.True, "a straight shot is a light hit");
            target.SetInvulnerable(true); Field.Spawn(1, Spec()); StepUntilGone();
            Assert.That(Field.Shots.Count, Is.Zero, "an invulnerable player still removes the shot"); Assert.That(target.CurrentHealth, Is.EqualTo(990));
            target.SetInvulnerable(false); target.SetDamageEnabled(false); Field.Spawn(1, Spec()); StepShots(40);
            Assert.That(Field.Shots.Count, Is.EqualTo(1), "a burrowed player (damage off) is passed over");
            Assert.That(Field.Shots[0].Position.x, Is.LessThan(-7f));
        }

        [UnityTest] public IEnumerator HeavyShotIsSlowBigAndAnOrdinaryHit()
        {
            yield return Spawn("RangedHeavy", new Vector2(-6, 51));
            Telegraph(); Tick(.41f);
            var shot = Mine()[0];
            Assert.That(shot.Velocity.magnitude, Is.EqualTo(2.21f).Within(.001f)); Assert.That(shot.Radius, Is.EqualTo(.505f).Within(.0001f));
            Assert.That(shot.Light, Is.False); Assert.That(shot.Kind, Is.EqualTo(ProjectileKind.Heavy));
        }

        [UnityTest] public IEnumerator ShotsOutliveTheirShooterAndWallsStopThem()
        {
            yield return Spawn("RangedStraight", new Vector2(-6, 51));
            Telegraph(); Tick(.41f);
            Health.ApplyDamage(30); Tick(); Assert.That(Phase, Is.EqualTo(RangedPhase.Death));
            Tick(.7f); Assert.That(enemy.gameObject.activeSelf, Is.False);
            Assert.That(Field.Shots.Count, Is.EqualTo(1), "the shooter's death leaves its shot");
            Block(new Vector2(-3, 51.5f), new Vector2(.4f, 3f));
            StepUntilGone();
            Assert.That(Field.Shots.Count, Is.Zero); Assert.That(Field.Pops.Count, Is.EqualTo(1)); Assert.That(target.CurrentHealth, Is.EqualTo(1000));
            Field.Spawn(1, new ProjectileSpec { Kind = ProjectileKind.Bolt, Origin = new Vector2(10, 60), Velocity = Vector2.right, Radius = .3f, Lifetime = 6,
                TargetMask = 4, ObstacleMask = 1, LandingY = float.NaN });
            EnemyProjectiles.ClearExisting(); Assert.That(Field.Shots.Count, Is.Zero, "a cleared encounter takes the leftover shots");
        }

        [UnityTest] public IEnumerator LostRecognitionCancelsTheTelegraphAndTheIntervalCountsFromIt()
        {
            yield return Spawn("RangedStraight", new Vector2(-6, 51));
            Telegraph();
            MoveTarget(new Vector2(-20, 51)); Tick();
            Assert.That(Phase, Is.EqualTo(RangedPhase.Idle)); Assert.That(Mine().Count, Is.Zero, "cancelled, nothing fired");
            MoveTarget(new Vector2(-6, 51)); Tick(1f);
            Assert.That(Phase, Is.EqualTo(RangedPhase.Idle), "the next telegraph still waits for 5 s from the cancelled one");
            Tick(4f); Assert.That(Phase, Is.EqualTo(RangedPhase.Preparing));
            // Off the player's screen is not recognized either.
            var camera = new GameObject("Ranged test view").AddComponent<Camera>(); created.Add(camera.gameObject);
            camera.orthographic = true; camera.orthographicSize = 3; camera.transform.position = new Vector3(-30, 51, -10); camera.enabled = false;
            var view = target.gameObject.AddComponent<ViewProbe>(); view.View = camera;
            Tick(); Assert.That(Phase, Is.EqualTo(RangedPhase.Idle), "off screen cancels the telegraph");
        }

        [UnityTest] public IEnumerator LaunchCancelsTelegraphAndSweepButOrdinaryHitsDoNot()
        {
            yield return Spawn("SweepLaser", new Vector2(-5, 51));
            Telegraph();
            Health.ApplyDamage(1, Combat.Center + Vector2.left); Tick();
            Assert.That(Phase, Is.EqualTo(RangedPhase.Preparing), "a basic hit keeps the telegraph");
            Health.ApplyDamage(1, Combat.Center + Vector2.left, true, true); Tick();
            Assert.That(Phase, Is.EqualTo(RangedPhase.Reacting)); Tick();
            Assert.That(Phase, Is.EqualTo(RangedPhase.Idle), "not moved: back at once, the attack is not resumed");
            Tick(5f); Assert.That(Phase, Is.EqualTo(RangedPhase.Preparing));
            Tick(.41f); Assert.That(Phase, Is.EqualTo(RangedPhase.Sweeping)); Assert.That(Combat.SweepActive, Is.True);
            Health.ApplyDamage(1, Combat.Center + Vector2.left, true, true); Tick();
            Assert.That(Phase, Is.EqualTo(RangedPhase.Reacting)); Assert.That(Combat.SweepActive, Is.False, "the live beam is removed");
        }

        [UnityTest] public IEnumerator SweepLaserTurnsClockwiseHurtsOnceAndNotAsAnAfterimage()
        {
            yield return Spawn("SweepLaser", new Vector2(-5, 51));
            Telegraph();
            float aim = Mathf.Atan2(State.Aim.y, State.Aim.x) * Mathf.Rad2Deg;
            Assert.That(State.SweepFrom, Is.EqualTo(aim + 30f).Within(.01f)); Assert.That(State.SweepTo, Is.EqualTo(aim - 30f).Within(.01f), "clockwise on screen");
            Tick(.41f); Assert.That(Phase, Is.EqualTo(RangedPhase.Sweeping));
            Assert.That(target.CurrentHealth, Is.EqualTo(1000), "the fan itself never hurts");
            Tick(.4f); Assert.That(target.CurrentHealth, Is.EqualTo(990), "the line crossed the player");
            Tick(.41f); Assert.That(Phase, Is.EqualTo(RangedPhase.Afterimage)); Assert.That(target.CurrentHealth, Is.EqualTo(990), "one hit per sweep");
            MoveTarget(Combat.CurrentBeam.End + (Combat.CurrentBeam.Start - Combat.CurrentBeam.End).normalized * 2f);
            Tick(.1f); Assert.That(target.CurrentHealth, Is.EqualTo(990), "the afterimage is harmless");
            Tick(.06f); Assert.That(Phase, Is.EqualTo(RangedPhase.Idle));
        }

        [UnityTest] public IEnumerator InvulnerableContactDoesNotUseUpTheSweepsHit()
        {
            // A tall target stays on the line for most of the sweep.
            yield return Spawn("SweepLaser", new Vector2(-5, 51), new Vector2(.6f, 8f));
            Telegraph(); Tick(.41f);
            target.SetInvulnerable(true); Tick(.4f); Assert.That(target.CurrentHealth, Is.EqualTo(1000));
            target.SetInvulnerable(false); Tick(.1f); Assert.That(target.CurrentHealth, Is.EqualTo(990), "hit after invulnerability ends, same sweep");
            Tick(.3f); Assert.That(target.CurrentHealth, Is.EqualTo(990));
        }

        [UnityTest] public IEnumerator LaserStopsAtTerrain()
        {
            yield return Spawn("SweepLaser", new Vector2(-5, 51));
            Block(new Vector2(-2.5f, 51.5f), new Vector2(.4f, 3f));
            Assert.That(Combat.BeamAt(180f).End.x, Is.EqualTo(-2.3f).Within(.01f));
            Assert.That(Vector2.Distance(Combat.BeamAt(90f).Start, Combat.BeamAt(90f).End), Is.EqualTo(13.05f).Within(.01f));
        }

        [UnityTest] public IEnumerator MortarTracksAlignsAndItsGroundBlastHurtsOnce()
        {
            yield return Spawn("RangedMortar", new Vector2(4, 51));
            float deadline = Time.realtimeSinceStartup + 4f;
            while (Phase != RangedPhase.Preparing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Phase, Is.EqualTo(RangedPhase.Preparing));
            Assert.That(Mathf.Abs(target.transform.position.x - Combat.Center.x), Is.LessThanOrEqualTo(1.3f), "tracked to within the align distance");
            Assert.That(State.LandingPoint.y, Is.EqualTo(Floor).Within(.01f), "the ground below, fixed at the telegraph");
            Assert.That(Combat.Center.y, Is.EqualTo(Floor + 4f).Within(.1f), "at its placed height");
            float x = Combat.Center.x; bool blasted = false;
            while (target.DamageVersion == 0 && Time.realtimeSinceStartup < deadline + 2f)
            { if (Field.Blasts.Count > 0) blasted = true; yield return null; }
            blasted |= Field.Blasts.Count > 0;
            Assert.That(blasted, Is.True, "landing beside the player blasts the ground");
            Assert.That(State.LandingPoint.x, Is.EqualTo(x).Within(.01f));
            float until = Time.realtimeSinceStartup + .4f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.That(target.CurrentHealth, Is.EqualTo(990), "one blast, one hit");
        }

        [UnityTest] public IEnumerator MortarDirectHitLeavesNoGroundBlast()
        {
            yield return Spawn("RangedMortar", new Vector2(0, 51));
            float deadline = Time.realtimeSinceStartup + 4f; bool blasted = false;
            while (target.DamageVersion == 0 && Time.realtimeSinceStartup < deadline) { if (Field.Blasts.Count > 0) blasted = true; yield return null; }
            Assert.That(target.CurrentHealth, Is.EqualTo(990)); Assert.That(target.LastDamage.Light, Is.False);
            yield return Step; yield return Step;
            Assert.That(blasted || Field.Blasts.Count > 0, Is.False, "a shell that hits the body does not blast");
        }

        [UnityTest] public IEnumerator SelfDomainFollowsItsOwnerOnlyWhileActiveAndEnds()
        {
            yield return Spawn("DomainSelf", new Vector2(-4, 51));
            Telegraph();
            Assert.That(GlitchHazards.Count, Is.Zero, "no kill during the telegraph");
            Tick(.41f); Assert.That(Combat.DomainActive, Is.True); Assert.That(GlitchHazards.Count, Is.EqualTo(1));
            Vector2 c = Combat.Center;
            Assert.That(Combat.Domain.ContainsPoint(c + new Vector2(2.52f, 2.1f)), Is.True, "5.05 x 4.21 around the owner");
            Assert.That(Combat.Domain.ContainsPoint(c + new Vector2(2.54f, 0)), Is.False);
            Assert.That(Combat.Domain.ContainsPoint(c + new Vector2(0, 2.12f)), Is.False);
            Health.ApplyDamage(1, Combat.Center + Vector2.left, true, true); Tick();
            Assert.That(Combat.DomainActive, Is.True, "a launch does not remove an open field");
            MoveEnemy(c + Vector2.right * 3f);
            Assert.That(Combat.Domain.ContainsPoint(c + Vector2.right * 5f), Is.True, "self-centered follows its owner");
            Tick(1f); Assert.That(Combat.DomainActive, Is.True);
            Tick(1f); Assert.That(Combat.DomainActive, Is.False, "2 s active"); Assert.That(GlitchHazards.Count, Is.Zero);
        }

        [UnityTest] public IEnumerator FixedDomainStaysAtItsPointAndItsOwnersDeathRemovesIt()
        {
            yield return Spawn("DomainFixed", new Vector2(-4, 51));
            Combat.FixedDomainCenter = new Vector2(-6, 52);
            Telegraph(); Tick(.41f);
            Assert.That(Combat.Domain.ContainsPoint(new Vector2(-6, 52)), Is.True);
            Assert.That(Combat.Domain.ContainsPoint(Combat.Center), Is.False, "not around its owner");
            MoveEnemy(Combat.Center + Vector2.right * 2f);
            Assert.That(Combat.Domain.ContainsPoint(new Vector2(-6, 52)), Is.True, "stays where it was set");
            Health.ApplyDamage(30); Tick();
            Assert.That(Phase, Is.EqualTo(RangedPhase.Death)); Assert.That(Combat.DomainActive, Is.False); Assert.That(GlitchHazards.Count, Is.Zero);
        }

        [UnityTest] public IEnumerator TelegraphsUseTheSharedAttackZoneAndFieldsTheirOwnTile()
        {
            yield return Spawn("RangedShotgun", new Vector2(-6, 51));
            var visual = enemy.GetComponentInChildren<RangedVisual2D>();
            var zoneProfile = AssetDatabase.LoadAssetAtPath<AttackZoneProfile>("Assets/_Game/Data/Presentation/AttackZone.asset");
            SpriteRenderer Zone(int i) { var root = visual.TelegraphRoot(i); return root == null ? null : root.Find("Attack zone").GetComponent<SpriteRenderer>(); }
            void Frame() { visual.SendMessage("LateUpdate"); EffectsRuntime.Vfx.SendMessage("LateUpdate"); }
            Frame(); Assert.That(Zone(0).enabled, Is.False, "idle");
            Telegraph(); Tick(.2f); Frame();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(Zone(i).enabled, Is.True, "one strip per shot");
                Assert.That(System.Array.IndexOf(zoneProfile.Frames, Zone(i).sprite), Is.GreaterThanOrEqualTo(0));
                Assert.That(Zone(i).color.a, Is.EqualTo(Mathf.Lerp(.2f, .7f, .5f)).Within(.01f), "darkens toward the shot");
                Assert.That(Zone(i).transform.lossyScale.x, Is.EqualTo(1f).Within(.001f));
            }
            Assert.That(Zone(3).enabled, Is.False);
            var strip = Combat.ShotPath(Combat.ShotDirection(State.Aim, 1));
            Assert.That(Zone(1).size.x, Is.EqualTo(Vector2.Distance(strip.Start, strip.End)).Within(.01f));
            Assert.That(Zone(1).size.y, Is.EqualTo(.42f).Within(.001f), "as wide as the shot");
            Tick(.21f); Frame(); Assert.That(Zone(0).enabled, Is.False, "gone once fired");
            var view = Field.GetComponent<EnemyProjectileView>(); view.SendMessage("LateUpdate");
            Assert.That(view.VisibleCount, Is.EqualTo(3), "each shot drawn");
        }

        [UnityTest] public IEnumerator LaserBeamAndDomainFieldAreDrawn()
        {
            yield return Spawn("SweepLaser", new Vector2(-5, 51));
            var visual = enemy.GetComponentInChildren<RangedVisual2D>();
            void Frame() { visual.SendMessage("LateUpdate"); EffectsRuntime.Vfx.SendMessage("LateUpdate"); }
            Telegraph(); Frame();
            for (int i = 0; i < 7; i++) Assert.That(visual.TelegraphRoot(i).Find("Attack zone").GetComponent<SpriteRenderer>().enabled, Is.True, "the fan as seven rays");
            Tick(.41f); Tick(.2f); Frame();
            var body = visual.Effects.GetSessionRoot(visual.LaserOwner).Find("Laser body 0").GetComponent<SpriteRenderer>();
            Assert.That(body.enabled, Is.True, "the live beam");
            Assert.That(visual.TelegraphRoot(0).Find("Attack zone").GetComponent<SpriteRenderer>().enabled, Is.False);
            Cleanup();
            yield return Spawn("DomainFixed", new Vector2(-4, 51));
            Combat.FixedDomainCenter = new Vector2(-6, 52);
            visual = enemy.GetComponentInChildren<RangedVisual2D>();
            var fieldProfile = AssetDatabase.LoadAssetAtPath<AttackZoneProfile>("Assets/_Game/Data/Presentation/DomainField.asset");
            Telegraph(); Tick(.41f); Frame();
            var field = visual.FieldRoot.Find("Attack zone").GetComponent<SpriteRenderer>();
            Assert.That(field.enabled, Is.True); Assert.That(System.Array.IndexOf(fieldProfile.Frames, field.sprite), Is.GreaterThanOrEqualTo(0));
            Assert.That(Vector2.Distance(field.bounds.center, new Vector2(-6, 52)), Is.LessThan(.01f));
            Assert.That(field.bounds.size.x, Is.EqualTo(5.05f).Within(.01f)); Assert.That(field.bounds.size.y, Is.EqualTo(4.21f).Within(.01f));
            Assert.That(visual.LinkRoot.Find("Attack zone").GetComponent<SpriteRenderer>().enabled, Is.True, "linked to its owner");
        }
    }

    public sealed class ViewProbe : MonoBehaviour, IUnitViewSource { public Camera View { get; set; } }
}
#endif
