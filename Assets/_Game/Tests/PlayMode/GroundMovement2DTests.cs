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
    public sealed class GroundMovement2DTests
    {
        private readonly List<Object> created = new List<Object>();
        private static readonly WaitForFixedUpdate FixedStep = new WaitForFixedUpdate();

        [TearDown] public void Cleanup()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        private void CreateGround(float width)
        {
            var ground = new GameObject("Common movement test ground");
            created.Add(ground);
            ground.transform.position = new Vector3(0f, 40f, 0f);
            ground.AddComponent<BoxCollider2D>().size = new Vector2(width, 1f);
        }

        private Unit CreateNpc(bool withMovement, bool stopAtLedges = true)
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            created.Add(definition);
            var data = new SerializedObject(definition);
            data.FindProperty("unitId").stringValue = "test.npc";
            data.FindProperty("displayName").stringValue = "Test NPC";
            data.FindProperty("kind").intValue = (int)UnitKind.Npc;
            data.ApplyModifiedPropertiesWithoutUndo();

            var instance = new GameObject("Common movement test NPC");
            instance.SetActive(false);
            created.Add(instance);
            instance.layer = 2;
            instance.transform.position = new Vector3(0f, 41.1f, 0f);
            var unit = instance.AddComponent<Unit>();
            var unitData = new SerializedObject(unit);
            unitData.FindProperty("definition").objectReferenceValue = definition;
            unitData.ApplyModifiedPropertiesWithoutUndo();
            Rigidbody2D body = null;
            if (withMovement)
            {
                body = instance.AddComponent<Rigidbody2D>();
                body.gravityScale = 4f;
                body.freezeRotation = true;
                instance.AddComponent<CapsuleCollider2D>().size = Vector2.one;
                var movement = instance.AddComponent<GroundMovement2D>();
                var movementData = new SerializedObject(movement);
                movementData.FindProperty("stopAtLedges").boolValue = stopAtLedges;
                movementData.ApplyModifiedPropertiesWithoutUndo();
            }

            var visual = new GameObject("Visual");
            visual.transform.SetParent(instance.transform, false);
            visual.AddComponent<SpriteRenderer>();
            var animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Game/Animations/Enemies/PatrolEnemy/PatrolEnemy.controller");
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            var unitVisual = visual.AddComponent<UnitVisual2D>();
            var visualData = new SerializedObject(unitVisual);
            visualData.FindProperty("body").objectReferenceValue = body;
            visualData.ApplyModifiedPropertiesWithoutUndo();
            instance.SetActive(true);
            return unit;
        }

        [UnityTest] public IEnumerator NpcWithoutFsmUsesCapsuleMovementAndCommonVisual()
        {
            CreateGround(30f);
            var npc = CreateNpc(true);
            var movement = npc.GetComponent<GroundMovement2D>();
            var animator = npc.GetComponentInChildren<Animator>();
            var sprite = npc.GetComponentInChildren<SpriteRenderer>();
            for (int i = 0; i < 10; i++) yield return FixedStep;
            Assert.That(npc.Kind, Is.EqualTo(UnitKind.Npc));
            Assert.That(npc.Fsm, Is.Null);
            Assert.That(npc.GetComponent<BoxCollider2D>(), Is.Null);
            Assert.That(animator.GetBool("Moving"), Is.False);

            movement.MoveTo(1f, 2f);
            for (int i = 0; i < 15; i++) yield return FixedStep;
            yield return null;
            Assert.That(movement.Position.x, Is.GreaterThan(0.2f));
            Assert.That(animator.GetBool("Moving"), Is.True);
            Assert.That(sprite.flipX, Is.True);
            for (int i = 0; i < 25; i++) yield return FixedStep;
            yield return null;
            Assert.That(movement.HasArrived, Is.True);
            Assert.That(movement.Position.x, Is.EqualTo(1f).Within(0.025f));
            Assert.That(movement.Velocity.x, Is.Zero.Within(0.001f));
            Assert.That(animator.GetBool("Moving"), Is.False);

            movement.MoveTo(-2f, 2f);
            for (int i = 0; i < 10; i++) yield return FixedStep;
            yield return null;
            Assert.That(movement.Velocity.x, Is.LessThan(0f));
            Assert.That(sprite.flipX, Is.False);
            movement.enabled = false;
            float stoppedX = movement.Position.x;
            movement.MoveTo(8f, 2f);
            movement.enabled = true;
            for (int i = 0; i < 10; i++) yield return FixedStep;
            Assert.That(movement.Position.x, Is.EqualTo(stoppedX).Within(0.01f));
            Assert.That(movement.HasArrived, Is.False, "Disabling clears the previous command and disabled commands are ignored.");
        }

        [UnityTest] public IEnumerator NpcStopsAtLedgeWithoutMakingItsOwnPatrolDecision()
        {
            CreateGround(4f);
            var npc = CreateNpc(true);
            var movement = npc.GetComponent<GroundMovement2D>();
            for (int i = 0; i < 10; i++) yield return FixedStep;
            movement.MoveTo(5f, 2f);
            for (int i = 0; i < 70; i++) yield return FixedStep;
            Assert.That(movement.IsBlocked, Is.True);
            Assert.That(movement.HasArrived, Is.False);
            Assert.That(movement.Position.x, Is.InRange(1f, 1.6f));
            Assert.That(movement.Position.y, Is.GreaterThan(40.7f));
            float stoppedX = movement.Position.x;
            for (int i = 0; i < 15; i++) yield return FixedStep;
            Assert.That(movement.Position.x, Is.EqualTo(stoppedX).Within(0.01f));
            movement.Stop();
            Assert.That(movement.IsBlocked, Is.False);
        }

        [UnityTest] public IEnumerator LedgeStoppingCanBeDisabledForAnotherNpc()
        {
            CreateGround(4f);
            var npc = CreateNpc(true, false);
            var movement = npc.GetComponent<GroundMovement2D>();
            for (int i = 0; i < 10; i++) yield return FixedStep;
            movement.MoveTo(5f, 2f);
            for (int i = 0; i < 95; i++) yield return FixedStep;
            Assert.That(movement.Position.x, Is.GreaterThan(3f));
            Assert.That(movement.Position.y, Is.LessThan(40.5f));
            Assert.That(movement.IsBlocked, Is.False);
        }

        [UnityTest] public IEnumerator StationaryNpcUsesCommonVisualWithoutPhysicsOrFsm()
        {
            var npc = CreateNpc(false);
            for (int i = 0; i < 5; i++) yield return FixedStep;
            Assert.That(npc.enabled, Is.True);
            Assert.That(npc.Fsm, Is.Null);
            Assert.That(npc.GetComponent<Rigidbody2D>(), Is.Null);
            Assert.That(npc.GetComponent<GroundMovement2D>(), Is.Null);
            Assert.That(npc.GetComponentInChildren<UnitVisual2D>().enabled, Is.True);
            var animator = npc.GetComponentInChildren<Animator>();
            Assert.That(animator.GetBool("Moving"), Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        }
    }
}
#endif
