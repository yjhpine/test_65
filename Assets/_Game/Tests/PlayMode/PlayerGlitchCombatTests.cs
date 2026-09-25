#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ActionPlatformer.Player;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ActionPlatformer.Tests
{
    public sealed class PlayerGlitchCombatTests
    {
        private readonly List<Object> created = new List<Object>();
        private PlayerUnit player;
        private UnitHealth enemy;
        private Camera camera;
        private Driver input;
        private GlitchTuning glitchSettings;
        private PlayerCombatTuning combatSettings;
        private UnitDefinition enemyDefinition;
        private PlayerGlitch Glitch => player.Glitch;
        private PlayerCombat Combat => player.Combat;
        private UnitHealth Health => player.GetComponent<UnitHealth>();
        private GroundMovement2D Movement => enemy.GetComponent<GroundMovement2D>();
        private static readonly WaitForFixedUpdate Step = new WaitForFixedUpdate();

        private sealed class Driver : IPlayerInputSource
        {
            public PlayerCommand Command;
            public PlayerCommand Sample()
            {
                var result = Command;
                Command.GlitchPressed = Command.AttackPressed = Command.JumpPressed = Command.JumpReleased = false;
                return result;
            }
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            Block(new Vector2(0f, 60f), new Vector2(40f, 1f));
            glitchSettings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GlitchTuning>("Assets/_Game/Data/GlitchTuning.asset"));
            combatSettings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlayerCombatTuning>("Assets/_Game/Data/PlayerCombatTuning.asset"));
            created.Add(glitchSettings); created.Add(combatSettings);
            var parent = new GameObject("Glitch test setup"); parent.SetActive(false); created.Add(parent);
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab"),
                new Vector3(-3f, 61.32f), Quaternion.identity, parent.transform);
            player = go.GetComponent<PlayerUnit>();
            // These fixture checks deliberately exercise the optional hit-box overlay.
            Set(go.GetComponentInChildren<PlayerVisual>(), "showAttackArea", true);
            Set(player, "glitchTuning", glitchSettings); Set(player, "combatTuning", combatSettings);
            var cameraGo = new GameObject("Glitch test camera"); created.Add(cameraGo);
            camera = cameraGo.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 6;
            camera.transform.position = new Vector3(0f, 61f, -10f); camera.enabled = false;
            player.SetAimCamera(camera);
            enemyDefinition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Game/Data/Units/PatrolEnemyDefinition.asset"));
            created.Add(enemyDefinition); Set(enemyDefinition, "fsm", (Object)null); Set(enemyDefinition, "maxHealth", 200);
            enemy = CreateEnemy(0f, parent.transform);
            input = new Driver();
            parent.SetActive(true); player.SetInputSource(input);
            yield return null;
            for (int i = 0; i < 5; i++) yield return Step;
            Physics2D.SyncTransforms();
        }

        [TearDown] public void Cleanup()
        {
            for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        private static void Set(Object target, string name, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(Object target, string name, float value)
        {
            var data = new SerializedObject(target); data.FindProperty(name).floatValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(Object target, string name, int value)
        {
            var data = new SerializedObject(target); data.FindProperty(name).intValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(Object target, string name, bool value)
        {
            var data = new SerializedObject(target); data.FindProperty(name).boolValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        private UnitHealth CreateEnemy(float x, Transform parent = null)
        {
            var holder = new GameObject("Inactive enemy setup"); holder.SetActive(false); created.Add(holder);
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/PatrolEnemy.prefab"),
                new Vector3(x, 61.01f), Quaternion.identity, holder.transform);
            Set(go.GetComponent<Unit>(), "definition", enemyDefinition);
            if (parent != null) holder.transform.SetParent(parent, true);
            holder.SetActive(true);
            return go.GetComponent<UnitHealth>();
        }
        private GameObject Block(Vector2 point, Vector2 size)
        {
            var go = new GameObject("Glitch test obstacle"); created.Add(go);
            go.transform.position = point; go.AddComponent<BoxCollider2D>().size = size;
            Physics2D.SyncTransforms(); return go;
        }
        private GlitchRequest Request(GlitchDirection direction) => new GlitchRequest { Target = enemy, Direction = direction };
        private void PlacePlayer(Vector2 point) { player.Motor.Teleport(point); Physics2D.SyncTransforms(); }
        private void EnterUnderground(double now = 0)
        {
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Down), now), Is.True, "Grounded below-target request enters underground.");
            Assert.That(Glitch.IsUnderground, Is.True);
        }

        private void RebuildPlayer(bool withGlitch, bool withCombat)
        {
            Object.DestroyImmediate(player.gameObject);
            var parent = new GameObject("Optional player actions"); parent.SetActive(false); created.Add(parent);
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab"),
                new Vector3(-3f, 61.32f), Quaternion.identity, parent.transform);
            player = go.GetComponent<PlayerUnit>();
            Set(player, "glitchTuning", withGlitch ? glitchSettings : null);
            Set(player, "combatTuning", withCombat ? combatSettings : null);
            player.SetAimCamera(camera);
            parent.SetActive(true); player.SetInputSource(input);
            Physics2D.SyncTransforms();
            Assert.That(player.isActiveAndEnabled, Is.True);
        }

        [UnityTest] public IEnumerator CombatWithoutGlitchProcessesInputDamageAndLifecycle()
        {
            RebuildPlayer(false, true);
            Assert.That(Glitch, Is.Null); Assert.That(Combat, Is.Not.Null);
            PlacePlayer(new Vector2(-0.975f, 61.32f));
            input.Command = new PlayerCommand { AttackPressed = true };
            float deadline = Time.realtimeSinceStartup + 2f;
            while (enemy.DamageVersion == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
            while (Combat.IsAttacking && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Combat.Phase, Is.EqualTo(PlayerAttackPhase.Ready));
            Health.ApplyDamage(5); yield return Step; yield return null;
            Assert.That(Combat.CanAttack(Time.timeAsDouble), Is.False);
            player.enabled = false;
            Assert.That(Combat.Strike, Is.Zero);
            player.enabled = true;
            input.Command = new PlayerCommand { JumpPressed = true, JumpHeld = true };
            yield return null; yield return Step; yield return null;
            Assert.That(player.Motor.Velocity.y, Is.GreaterThan(0));
        }

        [UnityTest] public IEnumerator GlitchWithoutCombatCanRepositionAndLeaveUnderground()
        {
            RebuildPlayer(true, false);
            Assert.That(Combat, Is.Null); Assert.That(Glitch, Is.Not.Null);
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Left), Time.timeAsDouble), Is.True);
            input.Command = new PlayerCommand { GlitchPressed = true,
                AimScreenPosition = camera.WorldToScreenPoint(enemy.transform.position + Vector3.down * 0.35f) };
            yield return null; yield return Step; yield return null;
            Assert.That(Glitch.IsUnderground, Is.True);
            input.Command = new PlayerCommand { AttackPressed = true };
            yield return null; yield return Step; yield return null;
            AssertRestored();
            Assert.That(enemy.DamageVersion, Is.Zero);
        }

        [UnityTest] public IEnumerator MovementWithoutEitherActionSystemRemainsUsable()
        {
            RebuildPlayer(false, false);
            Assert.That(Glitch, Is.Null); Assert.That(Combat, Is.Null);
            input.Command = new PlayerCommand { Move = Vector2.right, JumpPressed = true, JumpHeld = true,
                AttackPressed = true, GlitchPressed = true };
            yield return null; yield return Step; yield return null;
            Assert.That(player.Motor.Velocity.x, Is.GreaterThan(0));
            Assert.That(player.Motor.Velocity.y, Is.GreaterThan(0));
        }

        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        public void RemovingEachLinkPreservesBasicDamage(bool comboEnabled, bool positionEnabled, bool reactionsEnabled)
        {
            Set(combatSettings, "enableCombo", comboEnabled);
            Set(combatSettings, "enablePositionAttacks", positionEnabled);
            Set(combatSettings, "enableHitReactions", reactionsEnabled);
            RebuildPlayer(true, true);
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(0f, 64f); Physics2D.SyncTransforms();
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Down), 0), Is.True);
            Assert.That(Combat.TryAttack(enemy, 1, false, 0), Is.True);
            Assert.That(Combat.Attack, Is.EqualTo(positionEnabled ? PlayerAttack.Lift : PlayerAttack.Side));
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(0f, 61.01f); Physics2D.SyncTransforms();
            Combat.Reset(); PlacePlayer(new Vector2(-0.975f, 61.32f));
            for (int i = 0; i < 3; i++)
            {
                double now = i * 0.4;
                Assert.That(Combat.TryAttack(enemy, 1, false, now), Is.True);
                Assert.That(Combat.Strike, Is.EqualTo(comboEnabled ? i + 1 : 1));
                Combat.Tick(now + 0.13); Combat.Tick(now + 0.2); Combat.Tick(now + 0.4);
            }
            Assert.That(enemy.CurrentHealth, Is.EqualTo(170));
            Assert.That(Movement.IsForcedMoving, Is.EqualTo(comboEnabled && reactionsEnabled));
        }

        private sealed class FixedBoxSelector : IPlayerAttackSelector
        {
            public PlayerAttackSelection Select(Bounds body, UnitHealth target, float facing, bool emergence) =>
                new PlayerAttackSelection(PlayerAttack.Side, -1, new Vector2(0.975f, 0), Vector2.one);
        }
        private sealed class CountingReaction : IPlayerHitReaction
        {
            public int Count, Strike, HealthAfterHit;
            public bool Finisher;
            public void Apply(UnitHealth target, PlayerAttackSelection attack, PlayerComboStep combo)
            { Count++; Strike = combo.Strike; Finisher = combo.IsFinisher; HealthAfterHit = target.CurrentHealth; }
        }

        [Test] public void ReplacedRulesControlGeometryFinisherAndReactionWithoutChangingDamage()
        {
            PlacePlayer(new Vector2(-0.975f, 61.32f));
            var reaction = new CountingReaction();
            var rule = new SequentialComboRule(2);
            var combat = new PlayerCombat(player, player.GetComponent<Collider2D>(), combatSettings, rule, new FixedBoxSelector(), reaction);
            for (int i = 0; i < 2; i++)
            {
                double now = i * 0.4;
                Assert.That(combat.TryAttack(enemy, 1, false, now), Is.True);
                Assert.That(combat.Strike, Is.EqualTo(i + 1));
                Assert.That(combat.Facing, Is.EqualTo(-1));
                Assert.That(combat.AttackBounds.center.x, Is.Zero.Within(0.01f));
                combat.Tick(now + 0.13); combat.Tick(now + 0.14); combat.Tick(now + 0.2); combat.Tick(now + 0.4);
                if (i == 0)
                {
                    var independent = new PlayerCombat(player, player.GetComponent<Collider2D>(), combatSettings, rule);
                    Assert.That(independent.TryAttack(enemy, 1, false, 0.4), Is.True);
                    Assert.That(independent.Strike, Is.EqualTo(1), "Shared rules must not inherit another combat's next strike.");
                }
            }
            Assert.That(enemy.CurrentHealth, Is.EqualTo(180));
            Assert.That(reaction.Count, Is.EqualTo(2));
            Assert.That(reaction.Strike, Is.EqualTo(2));
            Assert.That(reaction.Finisher, Is.True);
            Assert.That(reaction.HealthAfterHit, Is.EqualTo(180));
            Assert.That(Movement.IsForcedMoving, Is.False);
            var another = new PlayerCombat(player, player.GetComponent<Collider2D>(), combatSettings, rule);
            Assert.That(another.TryAttack(enemy, 1, false, 0.8), Is.True);
            Assert.That(another.Strike, Is.EqualTo(1), "Sharing a rule must not share combo progress.");
            Assert.That(combat.TryAttack(enemy, 1, false, 0.8), Is.True);
            Assert.That(combat.Strike, Is.EqualTo(1), "The replacement rule has two strikes.");
        }

        [Test] public void DifferentComboLengthMovesFinisherReactionWithoutHardcodedThirdHit()
        {
            Set(combatSettings, "comboLength", 2);
            Set(combatSettings, "retainComboOnTargetChange", false);
            RebuildPlayer(true, true); PlacePlayer(new Vector2(-0.975f, 61.32f));
            var other = CreateEnemy(4);
            Combat.TryAttack(enemy, 1, false, 0); Combat.Tick(0.13); Combat.Tick(0.2); Combat.Tick(0.4);
            Assert.That(Movement.IsForcedMoving, Is.False);
            Combat.TryAttack(other, 1, false, 0.4);
            Assert.That(Combat.Strike, Is.EqualTo(1), "Replacement retention rule resets on target change.");
            Combat.Interrupt(0.4);
            Combat.TryAttack(enemy, 1, false, 1); Combat.Tick(1.13); Combat.Tick(1.2); Combat.Tick(1.4);
            Combat.TryAttack(enemy, 1, false, 1.4); Combat.Tick(1.53);
            Assert.That(Combat.Strike, Is.EqualTo(2));
            Assert.That(Movement.IsForcedMoving, Is.True, "Reaction follows the finisher data, not Strike == 3.");
        }

        [Test] public void AboveArrivalEnablesOneAirSlamAndThenReturnsToBasicAttack()
        {
            Combat.ObserveArrival(true);
            Assert.That(Combat.TryAttack(enemy, 1, false, 0, false), Is.True);
            Assert.That(Combat.Attack, Is.EqualTo(PlayerAttack.Slam));
            Combat.Tick(0.5); Combat.LandSlam(new Vector2(0f, 60.5f), 0.51);
            Combat.Tick(0.6); Combat.Tick(0.8);
            Assert.That(Combat.TryAttack(enemy, 1, false, 0.8, false), Is.True);
            Assert.That(Combat.Attack, Is.EqualTo(PlayerAttack.Side));
            Assert.That(Combat.Strike, Is.EqualTo(2), "Changing attack kind must not reset the combo.");
        }

        [TestCase("landing")]
        [TestCase("other_arrival")]
        [TestCase("hit")]
        [TestCase("reset")]
        public void StaleAboveArrivalCannotTurnLaterAirAttacksIntoSlams(string cause)
        {
            Combat.ObserveArrival(true);
            switch (cause)
            {
                case "landing": Combat.ObserveGrounded(true); break;
                case "other_arrival": Combat.ObserveArrival(false); break;
                case "hit": Combat.Interrupt(0); break;
                default: Combat.Reset(); break;
            }
            Assert.That(Combat.TryAttack(enemy, 1, false, 1, false), Is.True);
            Assert.That(Combat.Attack, Is.EqualTo(PlayerAttack.Side));
        }

        [UnityTest] public IEnumerator NormalAirInputUsesBasicDamageWithoutHoverOrDownwardReaction()
        {
            for (int configuration = 0; configuration < 2; configuration++)
            {
                RebuildPlayer(configuration == 0, true);
                var enemyBody = enemy.GetComponent<Rigidbody2D>();
                enemyBody.gravityScale = 0f; enemyBody.linearVelocity = Vector2.zero;
                enemyBody.position = new Vector2(1f, 65f);
                PlacePlayer(new Vector2(0f, 65f));
                uint oldVersion = enemy.DamageVersion;
                input.Command = new PlayerCommand { AttackPressed = true, Move = Vector2.right };
                float deadline = Time.realtimeSinceStartup + 2f;
                while (enemy.DamageVersion == oldVersion && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(enemy.DamageVersion, Is.EqualTo(oldVersion + 1));
                Assert.That(Combat.Attack, Is.EqualTo(PlayerAttack.Side));
                Assert.That(Combat.IsPreparingSlam || Combat.IsDescending, Is.False);
                Assert.That(player.Motor.Velocity.x, Is.GreaterThan(0f));
                Assert.That(player.Motor.Velocity.y, Is.LessThan(0f));
                Assert.That(Movement.IsForcedMoving, Is.False);
                Assert.That(player.Shockwave.Visible, Is.False);
            }
        }

        [UnityTest] public IEnumerator OtherGlitchDirectionsDoNotEnableAirSlam()
        {
            enemy.GetComponent<Rigidbody2D>().gravityScale = 0f;
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(0f, 64f);
            yield return Step; yield return null;
            foreach (Vector3 direction in new[] { Vector3.left, Vector3.right, Vector3.down })
            {
                RebuildPlayer(true, true);
                input.Command = new PlayerCommand { GlitchPressed = true, AttackPressed = true,
                    AimScreenPosition = camera.WorldToScreenPoint(enemy.transform.position + direction * 0.35f) };
                yield return null; yield return Step; yield return null;
                Assert.That(Glitch.SuccessVersion, Is.EqualTo(1));
                Assert.That(Combat.Attack, Is.EqualTo(PlayerAttack.Side));
                Assert.That(Combat.IsPreparingSlam, Is.False);
            }
        }

        [UnityTest] public IEnumerator LandingAfterTopGlitchClearsSlamBeforeTheNextJump()
        {
            input.Command = new PlayerCommand { GlitchPressed = true,
                AimScreenPosition = camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * 0.35f) };
            yield return null; yield return Step; yield return null;
            Assert.That(Glitch.SuccessVersion, Is.EqualTo(1));
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!player.Motor.IsGrounded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.Motor.IsGrounded, Is.True);
            input.Command = new PlayerCommand { JumpPressed = true, JumpHeld = true };
            yield return null; yield return Step; yield return null;
            Assert.That(player.Motor.Velocity.y, Is.GreaterThan(0));
            input.Command = new PlayerCommand { AttackPressed = true, JumpHeld = true };
            yield return null; yield return Step; yield return null;
            Assert.That(Combat.Attack, Is.EqualTo(PlayerAttack.Side));
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void SlamFrontHitsFacingSideOnceAndShockwaveDoesNotRepeatThatHit(float facing)
        {
            PlacePlayer(new Vector2(0f, 65f));
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(facing, 65f);
            var behind = CreateEnemy(-facing);
            behind.GetComponent<Rigidbody2D>().position = new Vector2(-facing, 65f);
            enemy.gameObject.AddComponent<BoxCollider2D>(); Physics2D.SyncTransforms();
            Combat.ObserveArrival(true);
            Combat.TryAttack(enemy, facing, false, 0, false);
            Combat.Tick(0.49);
            Assert.That(enemy.DamageVersion, Is.Zero, "Hover has no attack hitbox.");
            Combat.Tick(0.5);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
            Assert.That(Movement.Velocity.y, Is.EqualTo(-combatSettings.SlamKnockdownSpeed));
            Assert.That(behind.CurrentHealth, Is.EqualTo(200), "The descending hitbox only faces forward.");
            Combat.Tick(0.52);
            Combat.LandSlam(new Vector2(0f, 64.5f), 0.54);
            Assert.That(enemy.DamageVersion, Is.EqualTo(1), "Front and landing hits share one attack's hit history.");
            Assert.That(behind.CurrentHealth, Is.EqualTo(190), "Landing still hits previously untouched nearby enemies.");
            Assert.That(behind.GetComponent<GroundMovement2D>().IsForcedMoving, Is.False, "The shockwave retains its damage-only reaction.");
        }

        [Test] public void SlamFrontHitChecksTerrainAndTargetDamagePermission()
        {
            PlacePlayer(new Vector2(0f, 65f));
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(1f, 65f);
            var wall = Block(new Vector2(0.65f, 65f), new Vector2(0.1f, 2f));
            Combat.ObserveArrival(true);
            Combat.TryAttack(enemy, 1, false, 0, false); Combat.Tick(0.5);
            Assert.That(enemy.DamageVersion, Is.Zero);
            Object.DestroyImmediate(wall); enemy.SetDamageEnabled(false);
            Combat.Tick(0.52); Assert.That(enemy.DamageVersion, Is.Zero);
            enemy.SetDamageEnabled(true); Combat.Tick(0.54);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
            Assert.That(Movement.Velocity.y, Is.EqualTo(-combatSettings.SlamKnockdownSpeed));
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void SlamDamageWorksWithoutKnockdownReaction(bool reactionsEnabled, bool allowForcedMovement)
        {
            Set(combatSettings, "enableHitReactions", reactionsEnabled);
            Set(enemyDefinition, "allowForcedMovement", allowForcedMovement);
            RebuildPlayer(false, true); PlacePlayer(new Vector2(0f, 65f));
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(1f, 65f); Physics2D.SyncTransforms();
            Combat.ObserveArrival(true);
            Combat.TryAttack(enemy, 1, false, 0, false); Combat.Tick(0.5);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
            Assert.That(Movement.IsForcedMoving, Is.False);
        }

        [UnityTest] public IEnumerator SlamFrontKnocksAirborneEnemyDownToFloorBeforePlayerLanding()
        {
            var enemyBody = enemy.GetComponent<Rigidbody2D>();
            enemyBody.gravityScale = 0f;
            enemyBody.position = new Vector2(1f, 65f);
            PlacePlayer(new Vector2(0f, 65.4f));
            Combat.ObserveArrival(true);
            input.Command = new PlayerCommand { AttackPressed = true };
            float deadline = Time.realtimeSinceStartup + 3f;
            while (enemy.DamageVersion == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Combat.IsDescending, Is.True);
            Assert.That(player.Shockwave.Visible, Is.False);
            Assert.That(enemyBody.linearVelocity.y, Is.LessThan(-30f));
            yield return null;
            Assert.That(player.transform.Find("Visual/Attack area").GetComponent<SpriteRenderer>().enabled, Is.True);
            while (!player.Shockwave.Visible && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.Shockwave.Visible, Is.True);
            Assert.That(enemy.GetComponent<Collider2D>().bounds.min.y, Is.EqualTo(60.5f).Within(0.04f));
            Assert.That(enemy.DamageVersion, Is.EqualTo(1));
        }

        [Test] public void SlamHoverUsesItsOwnDurationAndSupportsImmediateDescent()
        {
            Assert.That(combatSettings.SlamHoverDuration, Is.EqualTo(0.5f));
            Combat.ObserveArrival(true);
            Combat.TryAttack(null, 1, false, 0, false);
            Combat.Tick(0.499);
            Assert.That(Combat.IsPreparingSlam, Is.True);
            Combat.Tick(0.5);
            Assert.That(Combat.IsDescending, Is.True);
            Combat.Reset();
            Combat.TryAttack(null, 1, false, 1);
            Combat.Tick(1.13);
            Assert.That(Combat.Phase, Is.EqualTo(PlayerAttackPhase.Active), "Ground attacks retain their 0.12-second windup.");
            Combat.Reset(); Set(combatSettings, "slamHoverDuration", 0f);
            Combat.ObserveArrival(true);
            Combat.TryAttack(null, 1, false, 2, false); Combat.Tick(2);
            Assert.That(Combat.IsDescending, Is.True);
        }

        [UnityTest] public IEnumerator AirAttackHoversAtItsStartPositionThenDescendsAndHits()
        {
            player.Motor.Relocate(new Vector2(0f, 66f), new Vector2(3f, 8f));
            Combat.ObserveArrival(true);
            input.Command = new PlayerCommand { AttackPressed = true, Move = Vector2.right, JumpHeld = true };
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!Combat.IsPreparingSlam && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Combat.IsPreparingSlam, Is.True);
            Vector2 hoverPoint = player.Motor.Position;
            double observedStart = Time.timeAsDouble;
            while (Time.timeAsDouble - observedStart < 0.35 && Time.realtimeSinceStartup < deadline)
            {
                input.Command = new PlayerCommand { Move = Vector2.right, JumpPressed = true, JumpHeld = true, AttackPressed = true };
                yield return Step;
                Assert.That(Combat.IsPreparingSlam, Is.True);
                Assert.That(Vector2.Distance(player.Motor.Position, hoverPoint), Is.LessThan(0.001f));
                Assert.That(player.Motor.Velocity, Is.EqualTo(Vector2.zero));
                Assert.That(Health.CanReceiveDamage, Is.True);
                Assert.That(Combat.CanReposition(Time.timeAsDouble), Is.False);
                Assert.That(enemy.DamageVersion, Is.Zero);
            }
            while (!Combat.IsDescending && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Combat.IsDescending, Is.True);
            Assert.That(Time.timeAsDouble - observedStart, Is.InRange(0.45, 0.58));
            Assert.That(player.Motor.Velocity.y, Is.EqualTo(-combatSettings.SlamSpeed).Within(0.01f));
            input.Command = default;
            while (!player.Shockwave.Visible && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.Shockwave.Visible, Is.True);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
        }

        [UnityTest] public IEnumerator HoverCancellationByHitDisableOrDeathLeavesNoFrozenState()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Combat.Reset(); player.enabled = true; PlacePlayer(new Vector2(0f, 68f));
                Combat.ObserveArrival(true);
                input.Command = new PlayerCommand { AttackPressed = true };
                float deadline = Time.realtimeSinceStartup + 2f;
                while (!Combat.IsPreparingSlam && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Combat.IsPreparingSlam, Is.True);
                if (attempt == 0) { Health.ApplyDamage(1); yield return Step; yield return null; }
                else if (attempt == 1) player.enabled = false;
                else Health.ApplyDamage(Health.CurrentHealth);
                Assert.That(Combat.Phase, Is.EqualTo(PlayerAttackPhase.Ready));
                Assert.That(Combat.Strike, Is.Zero);
                Assert.That(player.Motor.IsHeld, Is.False);
                Assert.That(player.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
                Assert.That(player.Shockwave.Visible, Is.False);
                Assert.That(enemy.DamageVersion, Is.Zero);
                if (attempt == 0)
                {
                    yield return Step; yield return Step;
                    Assert.That(player.Motor.Velocity.y, Is.LessThan(0f), "Normal gravity resumes after interruption.");
                }
            }
        }

        [UnityTest] public IEnumerator UnitsOverlapWithoutPhysicalContactsAndStillStandOnTerrain()
        {
            Assert.That(Physics2D.GetIgnoreLayerCollision(2, 2), Is.True);
            Assert.That(Physics2D.GetIgnoreLayerCollision(2, 0), Is.False);
            var other = CreateEnemy(0f);
            PlacePlayer(new Vector2(0f, 61.32f));
            for (int i = 0; i < 8; i++) yield return Step;
            var playerBody = player.GetComponent<Collider2D>();
            var enemyBody = enemy.GetComponent<Collider2D>();
            Assert.That(playerBody.Distance(enemyBody).isOverlapped, Is.True);
            Assert.That(playerBody.IsTouching(enemyBody), Is.False);
            Assert.That(enemyBody.IsTouching(other.GetComponent<Collider2D>()), Is.False);
            Assert.That(player.Motor.Position.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(playerBody.bounds.min.y, Is.EqualTo(60.5f).Within(0.04f));
            Assert.That(enemyBody.bounds.min.y, Is.EqualTo(60.5f).Within(0.04f));
        }

        [UnityTest] public IEnumerator ArrivalSlamWithoutOptionalLinksDivesThenHitsBothSidesOnce()
        {
            Set(combatSettings, "shockwaveDuration", 0.6f);
            Set(combatSettings, "enableCombo", false); Set(combatSettings, "enablePositionAttacks", false);
            Set(combatSettings, "enableHitReactions", false); RebuildPlayer(false, true);
            var left = CreateEnemy(-1.4f); var right = CreateEnemy(1.4f); var outside = CreateEnemy(4.5f);
            enemy.gameObject.AddComponent<BoxCollider2D>();
            PlacePlayer(new Vector2(0f, 66f));
            Combat.ObserveArrival(true);
            input.Command = new PlayerCommand { AttackPressed = true, Move = Vector2.right, JumpHeld = true };
            yield return null; yield return Step; yield return null;
            Assert.That(Combat.Attack, Is.EqualTo(PlayerAttack.Slam));
            float deadline = Time.realtimeSinceStartup + 2f;
            bool descended = false;
            while (!player.Shockwave.Visible && Time.realtimeSinceStartup < deadline)
            {
                if (Combat.IsPreparingSlam) Assert.That(enemy.DamageVersion, Is.Zero, "Hover has no contact damage.");
                if (Combat.IsDescending)
                {
                    descended = true;
                    Assert.That(player.Motor.Velocity.x, Is.Zero.Within(0.01f));
                    Assert.That(player.Motor.Velocity.y, Is.EqualTo(-combatSettings.SlamSpeed).Within(0.1f));
                    Assert.That(Combat.CanReposition(Time.timeAsDouble), Is.False);
                }
                yield return null;
            }
            Assert.That(descended, Is.True); Assert.That(player.Shockwave.Visible, Is.True);
            Assert.That(Vector2.Distance(outside.GetComponent<Collider2D>().ClosestPoint(player.Shockwave.Center),
                player.Shockwave.Center), Is.GreaterThan(combatSettings.ShockwaveRadius));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
            Assert.That(left.CurrentHealth, Is.EqualTo(190)); Assert.That(right.CurrentHealth, Is.EqualTo(190));
            Assert.That(outside.CurrentHealth, Is.EqualTo(200));
            input.Command = default;
            while (player.Shockwave.Progress < 0.45f && Time.realtimeSinceStartup < deadline) yield return null;
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return null;
                Assert.That(player.transform.Find("Visual/Shockwave 0").GetComponent<SpriteRenderer>().enabled, Is.True);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) yield return Capture("Shockwave");
            }
            finally { Time.timeScale = previousTimeScale; }
            for (int i = 0; i < 24; i++) yield return Step;
            Assert.That(enemy.DamageVersion, Is.EqualTo(1), "Multiple colliders and the visual duration must not repeat damage.");
            Assert.That(player.Shockwave.Visible, Is.False);
        }

        [UnityTest] public IEnumerator SlamStopsAtNearestThinPlatformWithoutHittingThroughItsFloor()
        {
            Set(combatSettings, "slamSpeed", 100f);
            Block(new Vector2(0f, 63f), new Vector2(6f, 0.06f));
            var elevated = CreateEnemy(1.1f);
            elevated.GetComponent<Rigidbody2D>().position = new Vector2(1.1f, 63.55f);
            PlacePlayer(new Vector2(0f, 66f)); Physics2D.SyncTransforms();
            Combat.ObserveArrival(true);
            input.Command = new PlayerCommand { AttackPressed = true };
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!player.Shockwave.Visible && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.Shockwave.Visible, Is.True);
            Assert.That(player.GetComponent<Collider2D>().bounds.min.y, Is.EqualTo(63.03f).Within(0.04f));
            Assert.That(elevated.CurrentHealth, Is.EqualTo(190));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(200));
        }

        [UnityTest] public IEnumerator ShockwaveDoesNotDamageThroughWalls()
        {
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(1.3f, 61.01f);
            Block(new Vector2(0.7f, 61.5f), new Vector2(0.1f, 2f));
            PlacePlayer(new Vector2(0f, 65f));
            input.Command = new PlayerCommand { AttackPressed = true };
            Combat.ObserveArrival(true);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!player.Shockwave.Visible && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.Shockwave.Visible, Is.True);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(200));
        }

        [Test] public void PresentationReportsCombatTimingWithoutAdvancingOrConsumingAnAttack()
        {
            Assert.That(Combat.TryAttack(null, -1f, false, 10, false), Is.True);
            var first = Combat.GetPresentation(10.06);
            Assert.That(first.Kind, Is.EqualTo(PlayerAttack.Side));
            Assert.That(first.Airborne, Is.True); Assert.That(first.Strike, Is.EqualTo(1));
            Assert.That(first.PhaseProgress, Is.EqualTo(.5f).Within(.001f));
            Combat.GetPresentation(100);
            Assert.That(Combat.Phase, Is.EqualTo(PlayerAttackPhase.Windup), "Reading visual facts cannot advance combat.");
            Combat.Tick(10.13);
            Assert.That(Combat.GetPresentation(10.16).PhaseProgress, Is.EqualTo(.5f).Within(.001f));
            Combat.Tick(10.2); Combat.CancelRecovery();
            Assert.That(Combat.TryAttack(null, 1f, false, 10.21), Is.True);
            var second = Combat.GetPresentation(10.21);
            Assert.That(second.Version, Is.EqualTo(first.Version + 1));
            Assert.That(second.Strike, Is.EqualTo(2)); Assert.That(second.Airborne, Is.False);
            Combat.Interrupt(10.22);
            Assert.That(Combat.Phase, Is.EqualTo(PlayerAttackPhase.Ready));
        }

        [Test] public void OrdinaryAirAttackUsesSideRegardlessOfRelativePositionOrEmergenceRequest()
        {
            foreach (Vector2 position in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
            {
                Combat.Reset();
                PlacePlayer((Vector2)enemy.transform.position + position * 1.5f);
                Assert.That(Combat.TryAttack(enemy, 1, false, 0, false), Is.True);
                Assert.That(Combat.Attack, Is.EqualTo(PlayerAttack.Side));
            }
            Combat.Reset();
            Combat.TryAttack(null, 1, true, 0, false);
            Assert.That(Combat.Attack, Is.EqualTo(PlayerAttack.Side));
        }

        [UnityTest] public IEnumerator DamageAndDisableCancelDescentWithoutLeavingMotionOrDamage()
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Combat.Reset(); PlacePlayer(new Vector2(0f, 70f));
                Combat.ObserveArrival(true);
                input.Command = new PlayerCommand { AttackPressed = true };
                float deadline = Time.realtimeSinceStartup + 2f;
                while (!Combat.IsDescending && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Combat.IsDescending, Is.True);
                if (attempt == 0)
                {
                    Health.ApplyDamage(1); yield return Step; yield return null;
                    Assert.That(Combat.CanAttack(Time.timeAsDouble), Is.False);
                }
                else player.enabled = false;
                Assert.That(Combat.Phase, Is.EqualTo(PlayerAttackPhase.Ready));
                Assert.That(Combat.Strike, Is.Zero);
                Assert.That(player.Shockwave.Visible, Is.False);
                Assert.That(player.Motor.Velocity.y, Is.GreaterThan(-combatSettings.SlamSpeed * 0.5f));
                Assert.That(Health.DamageEnabled, Is.True);
                Assert.That(Health.CanReceiveDamage, Is.EqualTo(attempt == 0));
                Assert.That(player.Motor.IsHeld, Is.False);
                Assert.That(enemy.DamageVersion, Is.Zero);
            }
        }

        [UnityTest] public IEnumerator MissingFloorTimesOutAndRestoresNormalMovementWithoutShockwave()
        {
            Set(combatSettings, "slamTimeout", 0.15f);
            PlacePlayer(new Vector2(30f, 66f));
            Combat.ObserveArrival(true);
            input.Command = new PlayerCommand { AttackPressed = true, Move = Vector2.right };
            yield return null; yield return Step; yield return null;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Combat.IsAttacking && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Combat.Phase, Is.EqualTo(PlayerAttackPhase.Ready));
            Assert.That(player.Shockwave.Visible, Is.False);
            Assert.That(player.Motor.Velocity.x, Is.GreaterThan(0f));
            Assert.That(Combat.CanReposition(Time.timeAsDouble), Is.True);
        }

        [TestCase(GlitchDirection.Left)]
        [TestCase(GlitchDirection.Right)]
        [TestCase(GlitchDirection.Up)]
        [TestCase(GlitchDirection.Down)]
        public void PreviewMatchesExecutionWithoutChangingGameplayState(GlitchDirection direction)
        {
            Set(glitchSettings, "cooldown", 2f);
            Vector2 original = player.Motor.Position;
            player.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(2f, 3f);
            var preview = Glitch.Preview(Request(direction), 0);
            Assert.That(preview.Visible && preview.CanExecute, Is.True);
            Assert.That(player.Motor.Position, Is.EqualTo(original));
            Assert.That(player.Motor.Velocity, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(Glitch.SuccessVersion, Is.Zero);
            Assert.That(Glitch.IsUnderground, Is.False);
            Assert.That(Health.CanReceiveDamage, Is.True);
            Assert.That(Glitch.CooldownRemaining(0), Is.Zero);
            Assert.That(Glitch.TryExecute(Request(direction), 0), Is.True);
            Assert.That(preview.IsUnderground, Is.EqualTo(Glitch.IsUnderground));
            Assert.That(Vector2.Distance(preview.Destination,
                Glitch.IsUnderground ? Glitch.UndergroundPosition : player.Motor.Position), Is.LessThan(0.001f));
        }

        private void Aim(Vector2 direction)
        {
            input.Command = new PlayerCommand { HasAim = true, AimScreenPosition =
                camera.WorldToScreenPoint((Vector2)enemy.transform.position + direction * 0.35f) };
        }

        [UnityTest] public IEnumerator PreviewFollowsFourDirectionsShowsBlockedAndUndergroundDestinations()
        {
            var fill = player.transform.Find("Visual/Glitch preview").GetComponent<SpriteRenderer>();
            Vector2 original = player.Motor.Position;
            foreach (Vector2 direction in new[] { Vector2.left, Vector2.up, Vector2.down, Vector2.right })
            {
                Aim(direction); yield return null; yield return null;
                var preview = player.GlitchPreview;
                Assert.That(preview.Visible && preview.CanExecute, Is.True);
                Assert.That(fill.enabled, Is.True);
                Assert.That(fill.color.g, Is.GreaterThan(fill.color.r));
                Assert.That(preview.IsUnderground, Is.EqualTo(direction == Vector2.down));
                Assert.That(Vector2.Distance(fill.transform.position, (Vector2)preview.Bounds.center +
                    (preview.IsUnderground ? Vector2.up * 0.04f : Vector2.zero)), Is.LessThan(0.01f));
            }
            Assert.That(Vector2.Distance(original, player.Motor.Position), Is.LessThan(0.05f));
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                yield return Capture("PreviewRight", (Vector2)enemy.transform.position + Vector2.right * 0.35f);
            var wall = Block(new Vector2(1.1f, 61.5f), new Vector2(0.4f, 2f));
            Aim(Vector2.right); yield return null; yield return null;
            Assert.That(player.GlitchPreview.Visible, Is.True);
            Assert.That(player.GlitchPreview.CanExecute, Is.False);
            Assert.That(fill.color.r, Is.GreaterThan(fill.color.g));
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                yield return Capture("PreviewBlocked", (Vector2)enemy.transform.position + Vector2.right * 0.35f);
            Object.DestroyImmediate(wall); Aim(Vector2.down); yield return null; yield return null;
            Assert.That(player.GlitchPreview.IsUnderground && player.GlitchPreview.CanExecute, Is.True);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                yield return Capture("PreviewUnderground", (Vector2)enemy.transform.position + Vector2.down * 0.35f);
            EnterUnderground(Time.timeAsDouble); yield return null; yield return null;
            Assert.That(fill.enabled, Is.False);
        }

        [UnityTest] public IEnumerator PreviewReflectsCombatCooldownAndClearsWithAimTargetAndDisable()
        {
            var fill = player.transform.Find("Visual/Glitch preview").GetComponent<SpriteRenderer>();
            Aim(Vector2.right); yield return null; yield return null;
            Assert.That(player.GlitchPreview.CanExecute, Is.True);
            Set(combatSettings, "windup", 1f);
            Combat.TryAttack(enemy, 1, false, Time.timeAsDouble);
            yield return null; yield return null;
            Assert.That(player.GlitchPreview.CanExecute, Is.False);
            Combat.Reset();
            Set(glitchSettings, "cooldown", 1f);
            Glitch.TryExecute(Request(GlitchDirection.Left), Time.timeAsDouble);
            yield return null; yield return null;
            Assert.That(player.GlitchPreview.Visible && !player.GlitchPreview.CanExecute, Is.True);
            Set(glitchSettings, "cooldown", 0f);
            input.Command = default; yield return null; yield return null;
            Assert.That(fill.enabled, Is.False);
            Aim(Vector2.right); yield return null; yield return null;
            Assert.That(fill.enabled, Is.True);
            enemy.gameObject.SetActive(false); yield return null; yield return null;
            Assert.That(fill.enabled, Is.False);
            enemy.gameObject.SetActive(true); yield return null; yield return null;
            player.enabled = false;
            Assert.That(player.GlitchPreview.Visible, Is.False);
            Assert.That(fill.enabled, Is.False);
        }

        [Test] public void DirectAimWinsAssistCanBeDisabledAndUnavailableTargetsAreIgnored()
        {
            var other = CreateEnemy(1.4f); Physics2D.SyncTransforms();
            Vector2 cursor = (Vector2)enemy.transform.position + new Vector2(0.45f, 0f);
            Assert.That(Glitch.Select(cursor).Target, Is.SameAs(enemy));
            Set(glitchSettings, "aimAssistRadius", 0f);
            Assert.That(Glitch.Select(cursor).Target, Is.SameAs(enemy));
            Assert.That(Glitch.Select((Vector2)enemy.transform.position + Vector2.up * 1f).Target, Is.Null);
            enemy.gameObject.SetActive(false);
            Assert.That(Glitch.Select(cursor).Target, Is.Null);
            Assert.That(other.IsAlive, Is.True);
        }

        [TestCase(GlitchDirection.Left)]
        [TestCase(GlitchDirection.Right)]
        [TestCase(GlitchDirection.Up)]
        public void OrdinaryPlacementFitsCapsuleAndClearsVelocity(GlitchDirection direction)
        {
            player.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(3f, 5f);
            Assert.That(Glitch.TryExecute(Request(direction), 0), Is.True);
            Assert.That(Glitch.IsUnderground, Is.False);
            Assert.That(player.Motor.Velocity, Is.EqualTo(Vector2.zero));
            Physics2D.SyncTransforms();
            Assert.That(player.GetComponent<Collider2D>().Distance(enemy.GetComponent<Collider2D>()).isOverlapped, Is.False);
            Assert.That(player.GetComponent<Collider2D>().bounds.min.y, Is.GreaterThanOrEqualTo(60.5f));
        }

        [Test] public void AirborneBelowPlacementDoesNotEnterUnderground()
        {
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(0f, 64f); Physics2D.SyncTransforms();
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Down), 0), Is.True);
            Assert.That(Glitch.IsUnderground, Is.False);
        }

        [Test] public void WallCancelsButNoncollidingUnitDoesNotBlockDestination()
        {
            Set(glitchSettings, "cooldown", 1f);
            Vector2 original = player.Motor.Position;
            var wall = Block(new Vector2(-1.5f, 62f), new Vector2(0.2f, 4f));
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Right), 0), Is.False);
            Object.DestroyImmediate(wall);
            wall = Block(new Vector2(0.975f, 61.3f), new Vector2(0.5f, 1.5f));
            player.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(2f, 3f);
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Right), 0), Is.False);
            Assert.That(player.Motor.Position, Is.EqualTo(original));
            Assert.That(player.Motor.Velocity, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(Glitch.CooldownRemaining(0), Is.Zero);
            Object.DestroyImmediate(wall);
            var blocker = CreateEnemy(1f); Physics2D.SyncTransforms();
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Right), 0), Is.True);
            Assert.That(player.GetComponent<Collider2D>().Distance(blocker.GetComponent<Collider2D>()).isOverlapped, Is.True);
        }

        [Test] public void CeilingAndDisappearingTargetCancel()
        {
            Block(new Vector2(0f, 62.3f), new Vector2(2f, 0.2f));
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Up), 0), Is.False);
            var request = Request(GlitchDirection.Left);
            enemy.gameObject.SetActive(false);
            Assert.That(Glitch.TryExecute(request, 0), Is.False);
        }

        [Test] public void UndergroundPreservesRootBlocksDamageAndEnemyTargetingThenRestores()
        {
            PlacePlayer(new Vector2(-1.1f, 61.32f));
            var enemyCombat = enemy.GetComponent<UnitCombat2D>();
            enemyCombat.RefreshTarget(UnitKind.Player, 6f, 8f);
            Assert.That(enemyCombat.Target, Is.SameAs(Health));
            Vector2 original = player.Motor.Position;
            EnterUnderground();
            Assert.That(player.Motor.Position, Is.EqualTo(original));
            Assert.That(player.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
            Assert.That(Health.ApplyDamage(20), Is.Zero);
            enemyCombat.RefreshTarget(UnitKind.Player, 6f, 8f);
            Assert.That(enemyCombat.Target, Is.Null);
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Up), 0.5), Is.False);
            Glitch.Tick(2.01);
            AssertRestored();
            Assert.That(player.Motor.Position, Is.EqualTo(original));
            Assert.That(Health.ApplyDamage(5), Is.EqualTo(5));
        }

        private void AssertRestored()
        {
            Assert.That(Glitch.IsUnderground, Is.False);
            Assert.That(Health.DamageEnabled, Is.True);
            Assert.That(player.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
        }

        [TestCase(1f)]
        [TestCase(2.6f)]
        [TestCase(4.2f)]
        public void OverlappingEnemyUsesSideAttackWithoutLaunching(float targetHeight)
        {
            enemy.GetComponent<BoxCollider2D>().size = new Vector2(1f, targetHeight);
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(0f, 60.5f + targetHeight * .5f);
            PlacePlayer(new Vector2(0f, 61.32f)); Physics2D.SyncTransforms();
            Assert.That(Combat.TryAttack(enemy, 1f, false, 0), Is.True);
            Combat.Tick(.13);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
            Assert.That(Combat.Attack, Is.EqualTo(PlayerAttack.Side), "A higher center inside overlapping bodies is not a target above the player.");
            Assert.That(Movement.Velocity.y, Is.LessThanOrEqualTo(.01f));
        }

        [TestCase(-1.5f)]
        [TestCase(0f)]
        [TestCase(1.5f)]
        public void EmergenceKeepsBurrowColumnWhenTargetMoves(float targetShift)
        {
            EnterUnderground();
            Vector2 marker = Glitch.GroundMarkerPosition;
            enemy.GetComponent<Rigidbody2D>().position += Vector2.right * targetShift;
            Physics2D.SyncTransforms();
            Assert.That(Glitch.TryEmerge(), Is.True);
            AssertRestored();
            var bounds = player.GetComponent<Collider2D>().bounds;
            Assert.That(bounds.center.x, Is.EqualTo(marker.x).Within(.001f));
            Assert.That(bounds.min.y, Is.EqualTo(marker.y + .02f).Within(.001f));
        }

        [Test] public void BlockedBurrowColumnCancelsInsteadOfChoosingAnOpenSide()
        {
            EnterUnderground(); Vector2 original = player.Motor.Position;
            // A narrow ceiling blocks only the burrow column; both old side candidates are clear.
            Block(new Vector2(0f, 62f), new Vector2(.2f, .2f));
            Assert.That(Glitch.TryEmerge(), Is.False);
            AssertRestored(); Assert.That(player.Motor.Position, Is.EqualTo(original));
        }

        [Test] public void EmergenceCentersOffsetCapsuleAndHitsBothSidesOfBurrow()
        {
            player.GetComponent<CapsuleCollider2D>().offset = new Vector2(.2f, .1f);
            Physics2D.SyncTransforms();
            EnterUnderground(); Vector2 marker = Glitch.GroundMarkerPosition;
            var left = CreateEnemy(-1f); var right = CreateEnemy(1f);
            Physics2D.SyncTransforms();
            Assert.That(Glitch.TryEmerge(), Is.True);
            Physics2D.SyncTransforms();
            var bounds = player.GetComponent<Collider2D>().bounds;
            Assert.That(bounds.center.x, Is.EqualTo(marker.x).Within(.001f));
            Assert.That(bounds.min.y, Is.EqualTo(marker.y + .02f).Within(.001f));
            Assert.That(Combat.TryAttack(enemy, -1f, true, 0), Is.True);
            Assert.That(Combat.AttackBounds.center.x, Is.EqualTo(marker.x).Within(.001f));
            Combat.Tick(.13);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
            Assert.That(left.CurrentHealth, Is.EqualTo(190)); Assert.That(right.CurrentHealth, Is.EqualTo(190));
            Assert.That(left.GetComponent<GroundMovement2D>().Velocity.y, Is.EqualTo(combatSettings.LaunchSpeed));
            Assert.That(right.GetComponent<GroundMovement2D>().Velocity.y, Is.EqualTo(combatSettings.LaunchSpeed));
        }

        [Test] public void EmergenceKeepsCooldownAndFailureReturnsToOrigin()
        {
            Set(glitchSettings, "cooldown", 1f);
            EnterUnderground();
            Assert.That(Glitch.TryEmerge(), Is.True);
            AssertRestored();
            Assert.That(Glitch.CooldownRemaining(0.5), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(player.GetComponent<Collider2D>().bounds.center.x, Is.EqualTo(Glitch.GroundMarkerPosition.x).Within(.001f));
            PlacePlayer(new Vector2(-3f, 61.32f)); EnterUnderground(2);
            Vector2 original = player.Motor.Position;
            Block(new Vector2(0f, 62.1f), new Vector2(4f, 0.4f));
            Assert.That(Glitch.TryEmerge(), Is.False);
            AssertRestored(); Assert.That(player.Motor.Position, Is.EqualTo(original));
        }

        [Test] public void TargetLossAndDisableRestoreUndergroundAndCooldown()
        {
            Set(glitchSettings, "cooldown", 2f); EnterUnderground();
            enemy.gameObject.SetActive(false); Glitch.Tick(0.1); AssertRestored();
            Assert.That(Glitch.CooldownRemaining(0.1), Is.GreaterThan(0));
            enemy.gameObject.SetActive(true); EnterUnderground(3);
            player.enabled = false; AssertRestored();
            Assert.That(Glitch.CooldownRemaining(3), Is.Zero);
            player.enabled = true;
            Assert.That(player.transform.Find("Visual").localPosition.y, Is.EqualTo(-0.8f).Within(0.001f));
        }

        [Test] public void ZeroCooldownOverridesAndClearsPreviouslyRunningTimer()
        {
            Set(glitchSettings, "cooldown", 3f);
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Up), 0), Is.True);
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Right), 0.1), Is.False);
            Set(glitchSettings, "cooldown", 0f); Glitch.Tick(0.2);
            Assert.That(Glitch.CooldownRemaining(0.2), Is.Zero);
            Set(glitchSettings, "cooldown", 3f);
            Assert.That(Glitch.CooldownRemaining(0.2), Is.Zero);
        }

        [Test] public void ComboCrossesTargetsAndHitsEachVictimOnce()
        {
            PlacePlayer(new Vector2(-0.975f, 61.32f));
            Assert.That(Combat.TryAttack(enemy, 1f, false, 0), Is.True);
            Assert.That(Combat.CanReposition(0), Is.False);
            Combat.Tick(0.13); Combat.Tick(0.14);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
            Combat.Tick(0.2);
            Assert.That(Combat.CanReposition(0.2), Is.True);
            var other = CreateEnemy(3f); Physics2D.SyncTransforms();
            Assert.That(Glitch.TryExecute(new GlitchRequest { Target = other, Direction = GlitchDirection.Left }, 0.2), Is.True);
            Combat.CancelRecovery(); Physics2D.SyncTransforms();
            Assert.That(Combat.TryAttack(Glitch.ArrivalTarget, 1f, false, 0.21), Is.True);
            Assert.That(Combat.Strike, Is.EqualTo(2)); Combat.Tick(0.34);
            Assert.That(other.CurrentHealth, Is.EqualTo(190));
        }

        [Test] public void MissConsumesStrikeAndInterruptedWindupDoesNotDamage()
        {
            Assert.That(Combat.TryAttack(null, -1f, false, 0), Is.True);
            Combat.Tick(0.13); Combat.Tick(0.2); Combat.Tick(0.4);
            Assert.That(Combat.TryAttack(null, -1f, false, 0.41), Is.True);
            Assert.That(Combat.Strike, Is.EqualTo(2));
            Combat.Interrupt(0.42); Combat.Tick(0.6);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(200));
            Assert.That(Combat.CanAttack(0.6), Is.False);
            Assert.That(Combat.TryAttack(null, 1f, false, 0.7), Is.True);
            Assert.That(Combat.Strike, Is.EqualTo(1));
        }

        [Test] public void ThirdStrikePushesAndFixedEnemyDeclinesForcedMovement()
        {
            PlacePlayer(new Vector2(-0.975f, 61.32f));
            for (int i = 0; i < 3; i++)
            {
                double now = i * 0.5;
                Assert.That(Combat.TryAttack(enemy, 1f, false, now), Is.True);
                Combat.Tick(now + 0.13); Combat.Tick(now + 0.2); Combat.Tick(now + 0.4);
            }
            Assert.That(Movement.IsForcedMoving, Is.True);
            Assert.That(Movement.Velocity.x, Is.EqualTo(6f).Within(0.01f));
            Movement.Stop(); Assert.That(Movement.Velocity.x, Is.EqualTo(6f).Within(0.01f));
            Movement.CancelForcedMovement(); Set(enemyDefinition, "allowForcedMovement", false);
            Movement.ApplyForcedMovement(Vector2.up * 10f, 0.35f);
            Assert.That(Movement.IsForcedMoving, Is.False);
        }

        [Test] public void EmergenceHitsLaunchesAndConsumesComboStrike()
        {
            EnterUnderground(); Assert.That(Glitch.TryEmerge(), Is.True); Physics2D.SyncTransforms();
            Assert.That(Combat.TryAttack(Glitch.ArrivalTarget, -1f, true, 0), Is.True);
            Assert.That(Combat.Tick(0.13), Is.True);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
            Assert.That(Movement.Velocity.y, Is.EqualTo(combatSettings.LaunchSpeed).Within(0.01f));
            Combat.Tick(0.2); Combat.CancelRecovery();
            Assert.That(Combat.TryAttack(null, 1f, false, 0.21), Is.True);
            Assert.That(Combat.Strike, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator InputGlitchThenAttackAndJumpBufferReset()
        {
            Vector2 aim = camera.WorldToScreenPoint(enemy.transform.position + Vector3.left * 0.3f);
            input.Command = new PlayerCommand { AimScreenPosition = aim, GlitchPressed = true, AttackPressed = true, JumpPressed = true };
            yield return null; yield return Step; yield return null;
            Assert.That(Glitch.SuccessVersion, Is.EqualTo(1));
            Assert.That(player.Motor.Position.x, Is.GreaterThan(-1.1f));
            Assert.That(player.Motor.Velocity.y, Is.LessThanOrEqualTo(0.01f), "Old jump buffer cannot fire after relocation.");
            for (int i = 0; i < 12; i++) yield return Step;
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
            Assert.That(Glitch.SuccessVersion, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator SpaceCancelsUndergroundBeforeSimultaneousAttack()
        {
            EnterUnderground(Time.timeAsDouble);
            yield return null;
            input.Command = new PlayerCommand { JumpPressed = true, AttackPressed = true };
            yield return null; yield return Step; yield return null;
            AssertRestored(); Assert.That(Combat.Phase, Is.EqualTo(PlayerAttackPhase.Ready));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(200));
            Assert.That(player.transform.Find("Visual").GetComponent<SpriteRenderer>().enabled, Is.True);
        }

        [UnityTest] public IEnumerator TakingDamageCancelsAttackAndBlocksGlitchButAllowsWalking()
        {
            Combat.TryAttack(enemy, 1f, false, Time.timeAsDouble);
            Health.ApplyDamage(1);
            input.Command = new PlayerCommand { Move = Vector2.right };
            yield return null; yield return Step;
            Assert.That(Combat.Phase, Is.EqualTo(PlayerAttackPhase.Ready));
            Assert.That(Combat.CanReposition(Time.timeAsDouble), Is.False);
            Assert.That(player.Motor.Velocity.x, Is.GreaterThan(0f));
        }

        [Test] public void GroundedPositionSelectionRetainsLiftAndTerrainBlocksDamage()
        {
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(0f, 64f); Physics2D.SyncTransforms();
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Down), 0), Is.True); Physics2D.SyncTransforms();
            Assert.That(Combat.TryAttack(enemy, 1f, false, 0), Is.True);
            Assert.That(Combat.Attack, Is.EqualTo(PlayerAttack.Lift)); Combat.Tick(0.13);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190)); Assert.That(Movement.Velocity.y, Is.EqualTo(combatSettings.LaunchSpeed));
            Combat.Reset(); Movement.CancelForcedMovement();
            Block(new Vector2(0f, 63.4f), new Vector2(3f, 0.1f));
            Assert.That(Combat.TryAttack(enemy, 1f, false, 2), Is.True); Combat.Tick(2.13);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190), "Terrain blocks damage even when the attack box overlaps.");
        }

        [UnityTest] public IEnumerator SlamAfterSimultaneousTopGlitchInputHitsGroundOnLanding()
        {
            yield return TopGlitchSlam(true);
        }

        [UnityTest] public IEnumerator SlamAfterSeparateTopGlitchInputHitsGroundOnLanding()
        {
            yield return TopGlitchSlam(false);
        }

        [UnityTest] public IEnumerator SlamAfterRecoveryCancelTopGlitchPreservesSameFrameAttack()
        {
            yield return TopGlitchSlam(true, true);
        }

        [UnityTest] public IEnumerator FailedGlitchDoesNotLetSameFrameAttackBypassRecovery()
        {
            Set(combatSettings, "recovery", 1f);
            Assert.That(Combat.TryAttack(null, 1f, false, Time.timeAsDouble), Is.True);
            float readyDeadline = Time.realtimeSinceStartup + 2f;
            while (Combat.Phase != PlayerAttackPhase.Recovery && Time.realtimeSinceStartup < readyDeadline) yield return null;
            Assert.That(Combat.Phase, Is.EqualTo(PlayerAttackPhase.Recovery));
            Block(new Vector2(0f, 62.3f), new Vector2(2f, 0.2f));
            input.Command = new PlayerCommand
            {
                AimScreenPosition = camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * 0.35f),
                GlitchPressed = true, AttackPressed = true
            };
            yield return null; yield return Step; yield return null;
            Assert.That(Glitch.SuccessVersion, Is.Zero);
            Assert.That(Combat.Phase, Is.EqualTo(PlayerAttackPhase.Recovery));
            Assert.That(Combat.Strike, Is.EqualTo(1));
            Assert.That(enemy.DamageVersion, Is.Zero);
        }

        private IEnumerator TopGlitchSlam(bool simultaneous, bool recovery = false)
        {
            var enemyBody = enemy.GetComponent<Rigidbody2D>();
            enemyBody.gravityScale = 0;
            enemyBody.position = new Vector2(0, 64); Physics2D.SyncTransforms();
            var groundedTarget = CreateEnemy(1.3f);
            // Settle the fixture's interpolated Transform before converting its aim point to screen coordinates.
            yield return Step; yield return null;
            if (recovery)
            {
                Assert.That(Combat.TryAttack(null, 1f, false, Time.timeAsDouble), Is.True);
                float readyDeadline = Time.realtimeSinceStartup + 2f;
                while (Combat.Phase != PlayerAttackPhase.Recovery && Time.realtimeSinceStartup < readyDeadline) yield return null;
                Assert.That(Combat.Phase, Is.EqualTo(PlayerAttackPhase.Recovery));
                Assert.That(Combat.CanReposition(Time.timeAsDouble), Is.True);
            }
            input.Command = new PlayerCommand
            {
                AimScreenPosition = camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * 0.35f),
                GlitchPressed = true, AttackPressed = simultaneous
            };
            yield return null; yield return Step; yield return null;
            Assert.That(Glitch.SuccessVersion, Is.EqualTo(1));
            if (!simultaneous)
            {
                input.Command = new PlayerCommand { AttackPressed = true };
                yield return null; yield return Step; yield return null;
            }
            TestContext.WriteLine($"simultaneous={simultaneous}, recovery={recovery}, phase={Combat.Phase}, attack={Combat.Attack}, player={player.Motor.Position}, enemy={enemyBody.position}");
            Assert.That(Combat.Attack, Is.EqualTo(PlayerAttack.Slam));
            Assert.That(Combat.Strike, Is.EqualTo(recovery ? 2 : 1));
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!player.Shockwave.Visible && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.Shockwave.Visible, Is.True);
            Assert.That(groundedTarget.CurrentHealth, Is.EqualTo(190));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190), "The descending forward hit also catches the target below the player.");
            Assert.That(enemy.DamageVersion, Is.EqualTo(1));
            Assert.That(player.GetComponent<Collider2D>().bounds.min.y, Is.EqualTo(60.5f).Within(0.04f));
        }

        [Test] public void ExpiredComboRestartsAndGlitchEligibilityDoesNotPreventDamage()
        {
            PlacePlayer(new Vector2(-0.975f, 61.32f));
            Set(enemyDefinition, "allowGlitchTarget", false);
            Assert.That(Glitch.Select(enemy.transform.position).Target, Is.Null);
            Assert.That(Combat.TryAttack(null, 1f, false, 0), Is.True);
            Combat.Tick(0.13); Combat.Tick(0.2); Combat.Tick(0.4);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
            Assert.That(Combat.TryAttack(null, 1f, false, 2), Is.True);
            Assert.That(Combat.Strike, Is.EqualTo(1));
        }

        [Test] public void UndergroundRejectsSlopedMovingAndLayeredGround()
        {
            var floor = Physics2D.Raycast(enemy.transform.position, Vector2.down, 2f, 1).collider;
            floor.transform.rotation = Quaternion.Euler(0f, 0f, 2f); Physics2D.SyncTransforms();
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Down), 0), Is.False);
            floor.transform.rotation = Quaternion.identity;
            var moving = floor.gameObject.AddComponent<Rigidbody2D>(); moving.bodyType = RigidbodyType2D.Kinematic;
            Physics2D.SyncTransforms(); Assert.That(Glitch.TryExecute(Request(GlitchDirection.Down), 0), Is.False);
            Object.DestroyImmediate(moving);
            Block(new Vector2(0f, 59.2f), new Vector2(20f, 0.2f));
            Assert.That(Glitch.TryExecute(Request(GlitchDirection.Down), 0), Is.False);
        }

        [UnityTest] public IEnumerator ForcedMovementSurvivesFixedUpdatesAndStopsAfterDuration()
        {
            Movement.ApplyForcedMovement(new Vector2(6f, 10f), 0.35f);
            Vector2 before = Movement.Position;
            Movement.MoveTo(-10f, 2f); Movement.Stop();
            for (int i = 0; i < 5; i++) yield return Step;
            Assert.That(Movement.Position.x, Is.GreaterThan(before.x + 0.3f));
            Assert.That(Movement.Position.y, Is.GreaterThan(before.y + 0.3f));
            for (int i = 0; i < 18; i++) yield return Step;
            Assert.That(Movement.IsForcedMoving, Is.False);
            Assert.That(Movement.Velocity.x, Is.Zero.Within(0.01f));
        }

        [UnityTest] public IEnumerator EmergenceThroughInputLaunchesPlayerAndAllowsAirFollowup()
        {
            EnterUnderground(Time.timeAsDouble);
            float burrowX = Glitch.GroundMarkerPosition.x;
            input.Command = new PlayerCommand { AttackPressed = true };
            for (int i = 0; i < 12; i++) { yield return null; yield return Step; }
            AssertRestored();
            Assert.That(enemy.CurrentHealth, Is.EqualTo(190));
            Assert.That(player.Motor.Position.y, Is.GreaterThan(61.5f));
            Assert.That(player.GetComponent<Collider2D>().bounds.center.x, Is.EqualTo(burrowX).Within(.001f));
            Assert.That(player.Motor.Velocity.x, Is.Zero.Within(.001f));
            Assert.That(enemy.transform.position.y, Is.GreaterThan(61.2f));
            Assert.That(Combat.CanReposition(Time.timeAsDouble), Is.True);
            input.Command = new PlayerCommand
            {
                AimScreenPosition = camera.WorldToScreenPoint(enemy.transform.position + Vector3.left * 0.3f),
                GlitchPressed = true
            };
            yield return null; yield return Step; yield return null;
            Assert.That(Glitch.SuccessVersion, Is.EqualTo(2));
            Assert.That(Glitch.IsUnderground, Is.False);
        }

        [UnityTest] public IEnumerator ShiftBindingHoldFiresOnceAndDisabledInputIsEmpty()
        {
            var background = InputSystem.settings.backgroundBehavior;
            var editor = InputSystem.settings.editorInputBehaviorInPlayMode;
            Keyboard keyboard = null; Mouse mouse = null;
            var reader = new PlayerInputReader(player.GetComponent<PlayerInput>());
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                // Devices added before IgnoreFocus can start disabled in a background Editor.
                keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
                Assert.That(keyboard.enabled, Is.True);
                var component = player.GetComponent<PlayerInput>();
                component.enabled = false; component.enabled = true;
                component.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
                var action = component.actions.FindAction("Glitch", true);
                Assert.That(action.enabled, Is.True);
                Assert.That(action.controls.Count, Is.GreaterThan(0));
                // Flush synthetic modifier events explicitly, just as the existing binding tests do.
                // Feed the sampled command through the normal PlayerUnit input-source boundary.
                InputSystem.QueueStateEvent(mouse, new MouseState { position = camera.WorldToScreenPoint(enemy.transform.position + Vector3.left * 0.3f) });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                InputSystem.Update();
                Assert.That(keyboard.leftShiftKey.isPressed, Is.True);
                input.Command = reader.Sample();
                Assert.That(input.Command.HasAim, Is.True);
                Assert.That(input.Command.GlitchPressed, Is.True);
                // A held key in subsequent input updates must not become another request.
                for (int i = 0; i < 8; i++)
                {
                    InputSystem.Update();
                    Assert.That(reader.Sample().GlitchPressed, Is.False);
                }
                yield return null; yield return Step; yield return null;
                Assert.That(Glitch.SuccessVersion, Is.EqualTo(1));
                component.DeactivateInput();
                Assert.That(new PlayerInputReader(component).Sample(), Is.EqualTo(default(PlayerCommand)));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                component.ActivateInput();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                InputSystem.Update(); input.Command = reader.Sample();
                Assert.That(input.Command.GlitchPressed, Is.True);
                yield return null; yield return Step; yield return null;
                Assert.That(Glitch.SuccessVersion, Is.EqualTo(2));
            }
            finally
            {
                player.SetInputSource(input);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editor;
            }
        }

        [UnityTest] public IEnumerator VisualShowsAttackAreaAndUndergroundMarkerAndRestores()
        {
            var visual = player.transform.Find("Visual");
            Set(combatSettings, "activeDuration", 0.4f);
            PlacePlayer(new Vector2(-0.975f, 61.32f));
            Assert.That(Combat.TryAttack(enemy, 1f, false, Time.timeAsDouble), Is.True);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Combat.Phase != PlayerAttackPhase.Active && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(visual.Find("Attack area").GetComponent<SpriteRenderer>().enabled, Is.True);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                yield return Capture("Attack");
            Combat.Reset(); PlacePlayer(new Vector2(-3f, 61.32f));
            EnterUnderground(Time.timeAsDouble); yield return null; yield return null;
            Assert.That(visual.GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(visual.Find("Underground marker").GetComponent<SpriteRenderer>().enabled, Is.True);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                yield return Capture("Underground");
            Glitch.CancelUnderground(); yield return null; yield return null;
            Assert.That(visual.GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(visual.Find("Underground marker").GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(visual.localPosition.y, Is.EqualTo(-0.8f).Within(0.001f));
        }

        private IEnumerator Capture(string name, Vector2? aimWorld = null)
        {
            foreach (var item in created)
            {
                if (!(item is GameObject go) || go == null || go.name != "Glitch test obstacle" || go.transform.childCount != 0) continue;
                var drawing = new GameObject("Test floor drawing"); drawing.transform.SetParent(go.transform, false);
                var sprite = drawing.AddComponent<SpriteRenderer>();
                sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Block.png");
                sprite.sharedMaterial = player.transform.Find("Visual").GetComponent<SpriteRenderer>().sharedMaterial;
                sprite.color = new Color(0.25f, 0.3f, 0.4f);
                Vector2 size = go.GetComponent<BoxCollider2D>().size;
                drawing.transform.localScale = new Vector3(size.x / sprite.sprite.bounds.size.x, size.y / sprite.sprite.bounds.size.y, 1f);
            }
            var texture = new RenderTexture(800, 450, 24);
            var image = new Texture2D(800, 450, TextureFormat.RGB24, false);
            camera.transform.position = new Vector3(0f, 61.6f, -10f); camera.orthographicSize = 2.6f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.07f, 0.09f, 0.14f);
            camera.targetTexture = texture; camera.enabled = true;
            if (aimWorld.HasValue)
                input.Command = new PlayerCommand { HasAim = true, AimScreenPosition = camera.WorldToScreenPoint(aimWorld.Value) };
            yield return null; yield return null;
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 800, 450), 0, 0); image.Apply();
                string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Library/PrototypeValidation"));
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, "Glitch" + name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; camera.enabled = false;
                texture.Release(); Object.Destroy(texture); Object.Destroy(image);
            }
        }
    }
}
#endif
