using System.Linq;
using ActionPlatformer.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class PlayerAdventurerAssetTests
    {
        [Test] public void PlayerActionsHaveCompleteSpriteOnlyClipsAndGuardedLocomotion()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab");
            Assert.That(prefab, Is.Not.Null);
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero);
            var visual = prefab.transform.Find("Visual");
            var animator = visual.GetComponent<Animator>();
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(new SerializedObject(visual.GetComponent<PlayerVisual>()).FindProperty("showAttackArea").boolValue, Is.False);
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var machine = controller.layers[0].stateMachine;
            string[] names = { "Idle","Run","Jump","Fall","Attack1","Attack2","Attack3","AirAttack1","AirAttack2","AirAttack3","Lift","Emergence","SlamHover","SlamFall","SlamLand","Hit","Die" };
            foreach (string name in names)
            {
                var state = machine.states.Single(s => s.state.name == name).state;
                var clip = state.motion as AnimationClip; Assert.That(clip, Is.Not.Null, name);
                Assert.That(AnimationUtility.GetCurveBindings(clip), Is.Empty, "No transform or gameplay curves: " + name);
                Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty);
                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                Assert.That(bindings.Length, Is.EqualTo(1));
                Assert.That(bindings[0].type, Is.EqualTo(typeof(SpriteRenderer)));
                Assert.That(bindings[0].propertyName, Is.EqualTo("m_Sprite"));
                foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]))
                {
                    Assert.That(key.value, Is.TypeOf<Sprite>(), name);
                    string path = AssetDatabase.GetAssetPath(key.value);
                    Assert.That(path, Does.StartWith("Assets/_Game/Art/Characters/Adventurer/"));
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(18f));
                    Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                }
                if (!new[]{"Idle","Run","Jump","Fall","SlamFall"}.Contains(name))
                {
                    Assert.That(state.timeParameterActive, Is.True, name);
                    Assert.That(state.timeParameter, Is.EqualTo("ActionTime"));
                }
            }
            foreach (var transition in machine.anyStateTransitions)
                Assert.That(transition.conditions.Any(c => c.parameter == "ActionOverride" && c.mode == AnimatorConditionMode.IfNot), Is.True);
        }
    }
}
