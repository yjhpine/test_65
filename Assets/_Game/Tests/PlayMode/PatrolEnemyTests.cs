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
    public sealed class PatrolEnemyTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private static readonly WaitForFixedUpdate FixedStep = new WaitForFixedUpdate();

        [TearDown] public void Cleanup()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        private void CreateBlock(Vector2 position, Vector2 size)
        {
            var block = new GameObject("Patrol test ground");
            created.Add(block);
            block.transform.position = position;
            block.AddComponent<BoxCollider2D>().size = size;
        }

        private Unit Spawn(float x)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/PatrolEnemy.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab, new Vector3(x, 41.1f, 0f), Quaternion.identity);
            created.Add(instance);
            return instance.GetComponent<Unit>();
        }

        [UnityTest] public IEnumerator PrefabWaitsPatrolsAndRestartsWithoutSharingState()
        {
            CreateBlock(new Vector2(0f, 40f), new Vector2(40f, 1f));
            var first = Spawn(0f);
            var second = Spawn(8f);
            var movement = first.GetComponent<GroundMovement2D>();
            var secondMovement = second.GetComponent<GroundMovement2D>();
            yield return null;
            yield return null;
            Assert.That(first.Kind, Is.EqualTo(UnitKind.Monster));
            Assert.That(first.Definition.UnitId, Is.EqualTo("enemy.patrol"));
            Assert.That(first.Definition, Is.SameAs(second.Definition));
            Assert.That(first.Definition.Fsm, Is.TypeOf<PatrolFsmDefinition>());
            Assert.That(first.Fsm, Is.Not.SameAs(second.Fsm));
            Assert.That(first.Fsm.CurrentState, Is.Not.SameAs(second.Fsm.CurrentState));
            var initial = first.Fsm.CurrentState;
            for (int i = 0; i < 10; i++) yield return FixedStep;
            Assert.That(movement.Velocity.x, Is.Zero.Within(0.001f));
            for (int i = 0; i < 40; i++) yield return FixedStep;
            Assert.That(movement.Position.x, Is.GreaterThan(0.4f));
            Assert.That(movement.Velocity.x, Is.GreaterThan(0f));

            first.enabled = false;
            float stoppedX = movement.Position.x;
            for (int i = 0; i < 10; i++) yield return FixedStep;
            Assert.That(first.Fsm.IsRunning, Is.False);
            Assert.That(movement.Position.x, Is.EqualTo(stoppedX).Within(0.01f));
            Assert.That(secondMovement.Velocity.x, Is.GreaterThan(0f));
            first.enabled = true;
            Assert.That(first.Fsm.CurrentState, Is.SameAs(initial));
            for (int i = 0; i < 10; i++) yield return FixedStep;
            Assert.That(movement.Velocity.x, Is.Zero.Within(0.001f));

            bool walkedRight = false, walkedLeft = false, facedLeft = false, waited = false;
            var visual = first.transform.Find("Visual");
            var spriteRenderer = visual.GetComponent<SpriteRenderer>();
            for (int i = 0; i < 280; i++)
            {
                yield return FixedStep;
                walkedRight |= movement.Velocity.x > 0.1f;
                walkedLeft |= movement.Velocity.x < -0.1f;
                waited |= walkedRight && Mathf.Abs(movement.Velocity.x) < 0.001f;
                facedLeft |= movement.Velocity.x < -0.1f && !spriteRenderer.flipX;
            }
            Assert.That(walkedRight && walkedLeft && waited && facedLeft, Is.True);
            Assert.That(first.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(first.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest] public IEnumerator UnitPrefabAnimatesIdleAndRunAndResetsAfterReactivation()
        {
            CreateBlock(new Vector2(0f, 40f), new Vector2(30f, 1f));
            var unit = Spawn(0f);
            Assert.That(unit, Is.TypeOf<Unit>());
            Assert.That(unit.GetComponent<Unit>(), Is.SameAs(unit));
            var visual = unit.transform.Find("Visual");
            var spriteRenderer = visual.GetComponent<SpriteRenderer>();
            var animator = visual.GetComponent<Animator>();
            Assert.That(spriteRenderer, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(unit.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(1));
            Vector3 visualPosition = visual.localPosition;
            var idleFrames = new HashSet<Sprite>();
            for (int i = 0; i < 20; i++)
            {
                yield return FixedStep;
                if (animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) idleFrames.Add(spriteRenderer.sprite);
            }
            Assert.That(idleFrames.Count, Is.GreaterThan(1));
            var runFrames = new HashSet<Sprite>();
            for (int i = 0; i < 50; i++)
            {
                yield return FixedStep;
                if (animator.GetCurrentAnimatorStateInfo(0).IsName("Run")) runFrames.Add(spriteRenderer.sprite);
            }
            Assert.That(runFrames.Count, Is.GreaterThan(1));
            Assert.That(animator.GetBool("Moving"), Is.True);
            Assert.That(spriteRenderer.flipX, Is.True, "Pig source art faces left; rightward travel must flip it.");
            unit.enabled = false;
            for (int i = 0; i < 5; i++) yield return FixedStep;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            Assert.That(animator.GetBool("Moving"), Is.False);
            unit.gameObject.SetActive(false);
            unit.enabled = true;
            unit.gameObject.SetActive(true);
            for (int i = 0; i < 5; i++) yield return FixedStep;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            Assert.That(animator.GetBool("Moving"), Is.False);
            Assert.That(visual.localPosition, Is.EqualTo(visualPosition));
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(unit.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest] public IEnumerator WallStopsThePatrolAndStartsTheOppositeLeg()
        {
            CreateBlock(new Vector2(0f, 40f), new Vector2(30f, 1f));
            CreateBlock(new Vector2(1.7f, 41.75f), new Vector2(0.2f, 2.5f));
            var unit = Spawn(0f);
            var movement = unit.GetComponent<GroundMovement2D>();
            bool walkedRight = false, walkedLeft = false;
            float furthestX = 0f;
            for (int i = 0; i < 180; i++)
            {
                yield return FixedStep;
                furthestX = Mathf.Max(furthestX, movement.Position.x);
                walkedRight |= movement.Velocity.x > 0.1f;
                walkedLeft |= movement.Velocity.x < -0.1f;
            }
            Assert.That(walkedRight && walkedLeft, Is.True);
            Assert.That(furthestX, Is.LessThan(1.2f));
        }

        [UnityTest] public IEnumerator LedgeDetectionKeepsThePatrolOnItsPlatform()
        {
            CreateBlock(new Vector2(0f, 40f), new Vector2(4f, 1f));
            var unit = Spawn(0f);
            var movement = unit.GetComponent<GroundMovement2D>();
            bool walkedRight = false, walkedLeft = false;
            for (int i = 0; i < 300; i++)
            {
                yield return FixedStep;
                walkedRight |= movement.Velocity.x > 0.1f;
                walkedLeft |= movement.Velocity.x < -0.1f;
                Assert.That(Mathf.Abs(movement.Position.x), Is.LessThan(1.65f));
                Assert.That(movement.Position.y, Is.GreaterThan(40.7f));
            }
            Assert.That(walkedRight && walkedLeft, Is.True);
        }
    }
}
#endif
