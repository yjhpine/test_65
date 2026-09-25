#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using ActionPlatformer.Units.Fsm;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    public sealed class UnitCombatFsmTests
    {
        private readonly List<Object> created = new List<Object>();
        private Unit enemy;
        private UnitHealth target;
        private GroundUnitFsmDefinition settings;
        private UnitCombat2D Combat => enemy.GetComponent<UnitCombat2D>();
        private UnitHealth Health => enemy.GetComponent<UnitHealth>();
        private string State => enemy.Fsm.CurrentState?.GetType().Name;

        [UnitySetUp] public IEnumerator SetUp()
        {
            Block(new Vector2(0f, 40f), new Vector2(100f, 1f));
            settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GroundUnitFsmDefinition>(
                "Assets/_Game/Data/Fsm/PatrolEnemyFsm.asset"));
            created.Add(settings);
            var definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                "Assets/_Game/Data/Units/PatrolEnemyDefinition.asset"));
            created.Add(definition);
            // Health assertions in this fixture use a 30 HP enemy, independently of live balancing.
            var definitionData = new SerializedObject(definition);
            definitionData.FindProperty("maxHealth").intValue = 30;
            definitionData.ApplyModifiedPropertiesWithoutUndo();
            Assign(definition, "fsm", settings);
            var parent = new GameObject("Inactive combat test setup");
            parent.SetActive(false);
            created.Add(parent);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/PatrolEnemy.prefab");
            var instance = Object.Instantiate(prefab, new Vector3(0f, 41.1f, 0f), Quaternion.identity, parent.transform);
            enemy = instance.GetComponent<Unit>();
            Assign(enemy, "definition", definition);
            target = CreateTarget(20f);
            parent.SetActive(true);
            yield return null;
            yield return null;
            enemy.Fsm.Stop();
            enemy.Fsm.Start();
        }

        [TearDown] public void Cleanup()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        private static void Assign(Object owner, string property, Object value)
        {
            var data = new SerializedObject(owner);
            data.FindProperty(property).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private UnitHealth CreateTarget(float x, UnitDefinition sharedDefinition = null)
        {
            var definition = sharedDefinition;
            if (definition == null)
            {
                definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                    "Assets/_Game/Data/Units/PlayerDefinition.asset"));
                created.Add(definition);
            }
            var instance = new GameObject("Combat test target");
            instance.SetActive(false);
            instance.layer = 2;
            instance.transform.position = new Vector3(x, 41.1f, 0f);
            created.Add(instance);
            Assign(instance.AddComponent<Unit>(), "definition", definition);
            instance.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            instance.AddComponent<BoxCollider2D>();
            var health = instance.AddComponent<UnitHealth>();
            var data = new SerializedObject(health);
            data.FindProperty("deactivateOnDeath").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            instance.SetActive(true);
            return health;
        }

        private GameObject Block(Vector2 position, Vector2 size)
        {
            var block = new GameObject("Combat test environment");
            created.Add(block);
            block.transform.position = position;
            block.AddComponent<BoxCollider2D>().size = size;
            return block;
        }

        private void PlaceTarget(float offset)
        {
            target.transform.position = enemy.transform.position + Vector3.right * offset;
            Physics2D.SyncTransforms();
            Combat.RefreshTarget(UnitKind.Player, 6f, 8f);
        }

        private void StartAttack()
        {
            PlaceTarget(1.1f);
            enemy.Fsm.Tick(0f);
            Assert.That(State, Is.EqualTo("AttackState"));
            enemy.Fsm.Tick(0f); // Start the windup, without advancing the cooldown clock.
        }

        [UnityTest] public IEnumerator ChasesPhysicallyRetainsTargetUntilLoseRangeAndReturnsToPatrol()
        {
            PlaceTarget(4f);
            float startX = enemy.transform.position.x;
            for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            Assert.That(State, Is.EqualTo("ChaseState"));
            Assert.That(enemy.transform.position.x, Is.GreaterThan(startX + 0.1f));
            PlaceTarget(7f);
            Assert.That(Combat.Target, Is.SameAs(target), "An acquired target remains beyond the acquisition radius.");
            PlaceTarget(9f);
            Assert.That(Combat.Target, Is.Null);
            enemy.Fsm.Tick(0f);
            Assert.That(State, Is.EqualTo("WaitState"));
            Assert.That(enemy.GetComponent<Rigidbody2D>().linearVelocity.x, Is.Zero.Within(0.001f));
        }

        [Test] public void AttackWaitsForWindupDamagesOnceAndRespectsCooldown()
        {
            StartAttack();
            enemy.Fsm.Tick(0.19f);
            Assert.That(target.CurrentHealth, Is.EqualTo(100));
            enemy.Fsm.Tick(0.02f);
            Assert.That(target.CurrentHealth, Is.EqualTo(95));
            enemy.Fsm.Tick(0.7f);
            Assert.That(target.CurrentHealth, Is.EqualTo(95));
            enemy.Fsm.Tick(0.1f);
            Assert.That(target.CurrentHealth, Is.EqualTo(95));
            enemy.Fsm.Tick(0.21f);
            Assert.That(target.CurrentHealth, Is.EqualTo(90));
        }

        [Test] public void AttackMissesWhenVictimLeavesRangeDuringWindup()
        {
            StartAttack();
            PlaceTarget(3f);
            enemy.Fsm.Tick(0.21f);
            Assert.That(target.CurrentHealth, Is.EqualTo(100));
            enemy.Fsm.Tick(0f);
            Assert.That(State, Is.EqualTo("ChaseState"));
        }

        [Test] public void WallsBlockAcquisitionAndBlockAnAlreadyPreparedAttack()
        {
            var wall = Block(new Vector2(0.55f, 42f), new Vector2(0.1f, 4f));
            PlaceTarget(1.1f);
            Assert.That(Combat.Target, Is.Null);
            wall.SetActive(false);
            StartAttack();
            wall.SetActive(true);
            Physics2D.SyncTransforms();
            // Direct impact validation must reject the hit even before the next target scan.
            Assert.That(Combat.TryHit(target, 1.2f), Is.False);
            enemy.Fsm.Tick(0.21f);
            Assert.That(target.CurrentHealth, Is.EqualTo(100));
        }

        [Test] public void HitInterruptsAttackAndRepeatedDamageRestartsStun()
        {
            StartAttack();
            Health.ApplyDamage(1);
            enemy.Fsm.Tick(0f);
            Assert.That(State, Is.EqualTo("HitState"));
            enemy.Fsm.Tick(0.15f);
            Health.ApplyDamage(1);
            enemy.Fsm.Tick(0f);
            enemy.Fsm.Tick(0.15f);
            Assert.That(State, Is.EqualTo("HitState"));
            Assert.That(target.CurrentHealth, Is.EqualTo(100));
            enemy.Fsm.Tick(0.11f);
            Assert.That(State, Is.EqualTo("AttackState"));
            enemy.Fsm.Tick(0f);
            Assert.That(target.CurrentHealth, Is.EqualTo(100), "Interrupted windup cannot apply deferred damage.");
            Assert.That(Health.CurrentHealth, Is.EqualTo(28));
        }

        [Test] public void DeathOverridesHitStopsMovementAndDeactivatesAfterConfiguredDelay()
        {
            StartAttack();
            enemy.GetComponent<GroundMovement2D>().MoveTo(3f, 2f);
            Assert.That(Health.ApplyDamage(999), Is.EqualTo(30));
            enemy.Fsm.Tick(0f);
            Assert.That(State, Is.EqualTo("DeathState"));
            Assert.That(Combat.Target, Is.Null);
            Assert.That(enemy.GetComponent<Rigidbody2D>().linearVelocity.x, Is.Zero.Within(0.001f));
            Assert.That(Health.ApplyDamage(1), Is.Zero);
            enemy.Fsm.Tick(0.79f);
            Assert.That(enemy.gameObject.activeSelf, Is.True);
            enemy.Fsm.Tick(0.02f);
            Assert.That(enemy.gameObject.activeSelf, Is.False);
            Assert.That(enemy.Fsm.IsRunning, Is.False);
            Assert.That(target.CurrentHealth, Is.EqualTo(100));
            enemy.gameObject.SetActive(true);
            enemy.Fsm.Tick(0f);
            Assert.That(State, Is.EqualTo("DeathState"));
            Assert.That(Health.CurrentHealth, Is.Zero, "Reactivation is not a resurrection API.");
        }

        [Test] public void ZeroDeathDelayCanDeactivateSafelyDuringStateEntry()
        {
            var data = new SerializedObject(settings);
            data.FindProperty("deathDelay").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
            Health.ApplyDamage(30);
            Assert.DoesNotThrow(() => enemy.Fsm.Tick(0f));
            Assert.That(enemy.gameObject.activeSelf, Is.False);
            Assert.That(enemy.Fsm.IsRunning, Is.False);
            Assert.That(enemy.Fsm.CurrentState, Is.Null);
        }

        [Test] public void HealthIsPerInstanceAndInactiveOrDeadUnitsCannotBeDamaged()
        {
            var second = CreateTarget(25f, target.Owner.Definition);
            Assert.That(target.ApplyDamage(7), Is.EqualTo(7));
            Assert.That(target.CurrentHealth, Is.EqualTo(93));
            Assert.That(second.CurrentHealth, Is.EqualTo(100));
            Assert.That(target.Owner.Definition.MaxHealth, Is.EqualTo(100));
            target.Owner.enabled = false;
            Assert.That(target.ApplyDamage(5), Is.Zero);
            target.Owner.enabled = true;
            Assert.That(target.ApplyDamage(1000), Is.EqualTo(93));
            Assert.That(target.ApplyDamage(5), Is.Zero);
            Assert.That(target.CurrentHealth, Is.Zero);
        }

        [UnityTest] public IEnumerator AttackAnimationRepeatsFacesTargetAndRestsDuringCooldown()
        {
            PlaceTarget(-1.1f);
            var visual = enemy.transform.Find("Visual");
            var animator = visual.GetComponent<Animator>();
            var renderer = visual.GetComponent<SpriteRenderer>();
            var frames = new HashSet<Sprite>();
            Vector3 initialVisualPosition = visual.localPosition;
            int attackEntries = 0;
            bool wasAttacking = false, rested = false;
            float until = Time.time + 1.65f;
            while (Time.time < until)
            {
                yield return null;
                bool attacking = animator.GetCurrentAnimatorStateInfo(0).IsName("Attack");
                if (attacking && !wasAttacking) attackEntries++;
                if (attacking)
                {
                    frames.Add(renderer.sprite);
                    Assert.That(renderer.flipX, Is.False, "Stationary attacks must face the target on the left.");
                }
                rested |= attackEntries > 0 && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle");
                wasAttacking = attacking;
            }
            Assert.That(attackEntries, Is.GreaterThanOrEqualTo(2));
            Assert.That(frames.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(rested, Is.True);
            Assert.That(target.CurrentHealth, Is.EqualTo(90));
            Assert.That(visual.localPosition, Is.EqualTo(initialVisualPosition));
            Assert.That(animator.applyRootMotion, Is.False);
        }

        [UnityTest] public IEnumerator HitAnimationInterruptsAttackRestartsOnDamageAndResetsOnDisable()
        {
            StartAttack();
            var animator = enemy.GetComponentInChildren<Animator>();
            var renderer = enemy.GetComponentInChildren<SpriteRenderer>();
            yield return WaitForPresentation(() => animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"), "Attack entry");
            Health.ApplyDamage(1);
            yield return WaitForPresentation(() => animator.GetCurrentAnimatorStateInfo(0).IsName("Hit"), "Hit interruption");
            enemy.Fsm.Tick(0.16f);
            yield return WaitForPresentation(() => renderer.sprite.name == "Hit_1", "Hit final frame");
            Health.ApplyDamage(1);
            yield return WaitForPresentation(() => renderer.sprite.name == "Hit_0", "Repeated hit restarts frame zero");
            Assert.That(target.CurrentHealth, Is.EqualTo(100));
            enemy.enabled = false;
            yield return WaitForPresentation(() => animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Disabled FSM clears Hit");
            target.gameObject.SetActive(false);
            enemy.gameObject.SetActive(false);
            enemy.enabled = true;
            enemy.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        }

        private static IEnumerator WaitForPresentation(System.Func<bool> condition, string reason)
        {
            // FSM Update, visual LateUpdate and Animator evaluation do not run in the test coroutine's phase.
            float until = Time.time + 0.15f;
            while (!condition() && Time.time < until) yield return null;
            Assert.That(condition(), Is.True, reason);
        }

        [UnityTest] public IEnumerator DeathAnimationPlaysAllFramesBeforeDeactivation()
        {
            var animator = enemy.GetComponentInChildren<Animator>();
            var renderer = enemy.GetComponentInChildren<SpriteRenderer>();
            var frames = new HashSet<Sprite>();
            Health.ApplyDamage(30);
            float started = Time.time;
            while (enemy.gameObject.activeSelf && Time.time - started < 1.2f)
            {
                yield return null;
                if (enemy.gameObject.activeSelf && animator.GetCurrentAnimatorStateInfo(0).IsName("Death"))
                    frames.Add(renderer.sprite);
            }
            Assert.That(frames.Count, Is.EqualTo(4));
            Assert.That(enemy.gameObject.activeSelf, Is.False);
            Assert.That(Time.time - started, Is.GreaterThanOrEqualTo(0.8f));
            foreach (var sprite in frames) Assert.That(sprite.name, Does.StartWith("Dead_"));
        }

        [Test] public void InvalidTransitionSettingsAreRejected()
        {
            Assert.That(settings.TryValidate(out _), Is.True);
            var data = new SerializedObject(settings);
            data.FindProperty("attackRange").floatValue = 7f;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(settings.TryValidate(out _), Is.False);
            data.FindProperty("attackRange").floatValue = 1.2f;
            data.FindProperty("attackWindup").floatValue = 2f;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(settings.TryValidate(out _), Is.False);
            data.FindProperty("attackWindup").floatValue = 0.2f;
            data.FindProperty("deathDelay").floatValue = float.NaN;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(settings.TryValidate(out _), Is.False);
        }
    }
}
#endif
