#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ActionPlatformer.Player;
using ActionPlatformer.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    public sealed class UnitLifecycleTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown] public void Cleanup()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        private UnitDefinition CreateDefinition(UnitKind kind, TestFsmDefinition fsm = null)
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            created.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("unitId").stringValue = "test.unit";
            serialized.FindProperty("displayName").stringValue = "Test Unit";
            serialized.FindProperty("kind").intValue = (int)kind;
            serialized.FindProperty("fsm").objectReferenceValue = fsm;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private Unit CreateUnit(UnitDefinition definition)
        {
            var instance = new GameObject("Test unit");
            instance.SetActive(false);
            created.Add(instance);
            var unit = instance.AddComponent<Unit>();
            var serialized = new SerializedObject(unit);
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            instance.SetActive(true);
            return unit;
        }

        [UnityTest] public IEnumerator SharedDefinitionCreatesIndependentStatesAndRestartsAfterDisable()
        {
            var fsmDefinition = ScriptableObject.CreateInstance<TestFsmDefinition>();
            created.Add(fsmDefinition);
            var definition = CreateDefinition(UnitKind.Monster, fsmDefinition);
            var first = CreateUnit(definition);
            var second = CreateUnit(definition);
            Assert.That(first.Fsm, Is.Null, "FSM starts in Start, after feature Awake initialization.");
            yield return null;
            yield return null;

            Assert.That(first.Definition, Is.SameAs(second.Definition));
            Assert.That(first.Kind, Is.EqualTo(UnitKind.Monster));
            Assert.That(first.Fsm, Is.Not.SameAs(second.Fsm));
            var initial = (TestFsmDefinition.ProbeState)first.Fsm.CurrentState;
            var other = (TestFsmDefinition.ProbeState)second.Fsm.CurrentState;
            Assert.That(initial, Is.Not.SameAs(other));
            Assert.That(initial.Owner, Is.SameAs(first));
            Assert.That(other.Owner, Is.SameAs(second));
            Assert.That(initial.Elapsed, Is.GreaterThan(0f));

            var next = new TestFsmDefinition.ProbeState(first);
            initial.Next = next;
            yield return null;
            yield return null;
            Assert.That(first.Fsm.CurrentState, Is.SameAs(next));
            Assert.That(second.Fsm.CurrentState, Is.SameAs(other));
            Assert.That(initial.Exits, Is.EqualTo(1));
            first.gameObject.SetActive(false);
            Assert.That(first.Fsm.IsRunning, Is.False);
            Assert.That(next.Exits, Is.EqualTo(1));
            float otherElapsed = other.Elapsed;
            yield return null;
            yield return null;
            Assert.That(other.Elapsed, Is.GreaterThan(otherElapsed));

            first.gameObject.SetActive(true);
            Assert.That(first.Fsm.CurrentState, Is.SameAs(initial));
            Assert.That(initial.Enters, Is.EqualTo(2));
            Assert.That(initial.Elapsed, Is.Zero);
            first.enabled = false;
            Assert.That(initial.Exits, Is.EqualTo(2));
            first.enabled = true;
            Assert.That(initial.Enters, Is.EqualTo(3));
            Object.DestroyImmediate(first.gameObject);
            Assert.That(initial.Exits, Is.EqualTo(3));
            Assert.That(second.Fsm.IsRunning, Is.True);
        }

        [UnityTest] public IEnumerator DefinitionWithoutFsmNeedsNoPlayerOrPhysicsComponents()
        {
            var definition = CreateDefinition(UnitKind.Npc);
            var unit = CreateUnit(definition);
            yield return null;
            yield return null;
            Assert.That(unit.Definition.UnitId, Is.EqualTo("test.unit"));
            Assert.That(unit.Kind, Is.EqualTo(UnitKind.Npc));
            Assert.That(unit.Fsm, Is.Null);
            Assert.That(unit.enabled, Is.True);
            Assert.That(unit.GetComponents<Component>().Length, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator MissingDefinitionDisablesTheUnitAndCannotStartAnFsm()
        {
            LogAssert.Expect(LogType.Error, "Unit definition is missing.");
            var unit = CreateUnit(null);
            Assert.That(unit.enabled, Is.False);
            unit.enabled = true;
            yield return null;
            Assert.That(unit.enabled, Is.False);
            Assert.That(unit.Fsm, Is.Null);
        }

        [UnityTest] public IEnumerator FailedPlayerInitializationCannotBeReenabledOrStartAnFsm()
        {
            var fsmDefinition = ScriptableObject.CreateInstance<TestFsmDefinition>();
            created.Add(fsmDefinition);
            var definition = CreateDefinition(UnitKind.Player, fsmDefinition);
            var instance = new GameObject("Player with missing tuning");
            instance.SetActive(false);
            created.Add(instance);
            var playerInput = instance.AddComponent<PlayerInput>();
            playerInput.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Game/Data/PrototypeControls.inputactions");
            playerInput.defaultActionMap = "Player";
            var player = instance.AddComponent<PlayerUnit>();
            instance.GetComponent<Rigidbody2D>().simulated = false;
            var serialized = new SerializedObject(player);
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            LogAssert.Expect(LogType.Error, "Player tuning is missing.");
            instance.SetActive(true);
            Assert.That(player.enabled, Is.False);
            Assert.That(player.Motor, Is.Null);

            player.enabled = true;
            Assert.That(player.enabled, Is.False, "Failed initialization must block component reactivation.");
            yield return null;
            yield return null;
            Assert.That(player.Fsm, Is.Null);

            instance.SetActive(false);
            player.enabled = true;
            instance.SetActive(true);
            Assert.That(player.enabled, Is.False, "Failed initialization must also block GameObject reactivation.");
            yield return null;
            yield return null;
            Assert.That(player.Motor, Is.Null);
            Assert.That(player.Fsm, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(UnitKind.Player)]
        [TestCase(UnitKind.Monster)]
        [TestCase(UnitKind.Npc)]
        public void ValidDefinitionIdentifiesEachUnitCategory(UnitKind kind)
        {
            var definition = CreateDefinition(kind);
            Assert.That(definition.TryValidate(out string error), Is.True, error);
            Assert.That(definition.Kind, Is.EqualTo(kind));
        }

        [Test] public void InvalidDefinitionRejectsMissingIdentityAndUnspecifiedKind()
        {
            var definition = CreateDefinition(UnitKind.Unspecified);
            Assert.That(definition.TryValidate(out _), Is.False);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("kind").intValue = (int)UnitKind.Player;
            serialized.FindProperty("unitId").stringValue = " ";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(definition.TryValidate(out _), Is.False);
            serialized.FindProperty("unitId").stringValue = "player";
            serialized.FindProperty("displayName").stringValue = "";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(definition.TryValidate(out _), Is.False);
        }
    }
}
#endif
