using System.Linq;
using ActionPlatformer.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class PlayerVisualAssetTests
    {
        [Test] public void PlayerEffectsHaveAllOrderedFramesAndDoNotAddGameplayComponents()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab");
            var visual = prefab.GetComponentInChildren<PlayerVisual>();
            var data = new SerializedObject(new SerializedObject(visual).FindProperty("vfxProfile").objectReferenceValue);
            foreach (var pair in new[] { new[] { "basicAttackEffectFrames", "white_slash" }, new[] { "glitchEffectFrames", "white_spark" } })
            {
                var frames = data.FindProperty(pair[0]); Assert.That(frames.arraySize, Is.EqualTo(16));
                for (int i = 0; i < frames.arraySize; i++)
                {
                    var frame = (Sprite)frames.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(frame, Is.Not.Null);
                    Assert.That(frame.name, Is.EqualTo(pair[1] + "_" + i.ToString("D2")));
                    Assert.That(frame.rect.size, Is.EqualTo(new Vector2(128,128)));
                    Assert.That(frame.pivot, Is.EqualTo(new Vector2(64,64)));
                    Assert.That(frame.pixelsPerUnit, Is.EqualTo(128f));
                    Assert.That(AssetDatabase.GetAssetPath(frame), Does.StartWith("Assets/_Game/Art/Effects/CombatFxFree/"));
                }
            }
            Assert.That(visual.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(visual.GetComponent<Animator>().applyRootMotion, Is.False);
        }
        [Test] public void PlayerActionsHaveCompleteSpriteOnlyClipsAndGuardedLocomotion()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab");
            Assert.That(prefab, Is.Not.Null);
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero);
            var visual = prefab.transform.Find("Visual");
            var animator = visual.GetComponent<Animator>();
            Assert.That(animator.applyRootMotion, Is.False);
            var sprite = visual.GetComponent<SpriteRenderer>().sprite;
            Assert.That(sprite.name, Is.EqualTo("idle_v4_01"));
            float feetY = visual.localPosition.y + (17f - sprite.pivot.y) / sprite.pixelsPerUnit;
            var collider = prefab.GetComponent<CapsuleCollider2D>();
            Assert.That(feetY, Is.EqualTo(collider.offset.y - collider.size.y * .5f).Within(.001f));
            Assert.That(new SerializedObject(visual.GetComponent<PlayerVisual>()).FindProperty("showAttackArea").boolValue, Is.False);
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var machine = controller.layers[0].stateMachine;
            string[] names = { "Idle","Run","Jump","Fall","Attack1","Attack2","Attack3","AirAttack1","AirAttack2","AirAttack3","Lift","Emergence","SlamHover","SlamFall","SlamLand","Hit","Die" };
            foreach (string name in names)
            {
                var state = machine.states.Single(s => s.state.name == name).state;
                var clip = state.motion as AnimationClip; Assert.That(clip, Is.Not.Null, name);
                Assert.That(clip.isLooping, Is.EqualTo(new[] { "Idle", "Run", "SlamFall" }.Contains(name)), name);
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
                    Assert.That(path, Does.StartWith("Assets/Art/"));
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
            var guide = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Game/Cinematics/GuideIdle.controller");
            Assert.That(AssetDatabase.GetAssetPath(guide.animationClips[0]), Is.EqualTo("Assets/_Game/Animations/Player/Idle.anim"));
            var guideBinding = AnimationUtility.GetObjectReferenceCurveBindings(guide.animationClips[0]).Single();
            Assert.That(AssetDatabase.GetAssetPath(AnimationUtility.GetObjectReferenceCurve(guide.animationClips[0], guideBinding)[0].value),
                Does.StartWith("Assets/_Game/Art/Characters/Adventurer/"));
        }
    }
}
