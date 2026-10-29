#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine.TestTools;
using ActionPlatformer.Presentation;
using System.Linq;
using System.Reflection;
using ActionPlatformer.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ActionPlatformer.Tests
{
    public sealed class MovementImpactVfxTests
    {
        private GameObject owner;
        private object observer;
        private VfxManager effects;
        private EffectOwnerHandle handle;
        private Sprite[] Load(string path) => AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/Effects/" + path)
            .OfType<Sprite>().OrderBy(s => s.name).ToArray();
        private void Call(string method, params object[] args)
        {
            if(method=="Land")effects.PlayDust(handle,(Vector2)args[0],(bool)args[1]);
            else if(method=="Reset") { effects.ResetOwner(handle); observer.GetType().GetMethod("Reset").Invoke(observer,null); }
        }
        private SpriteRenderer[] Visible() => effects.GetSessionRoot(handle).GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled && r.sprite != null).ToArray();
        [SetUp] public void Setup()
        {
            owner = new GameObject("VFX presentation test");
            var source = owner.AddComponent<SpriteRenderer>(); source.enabled = false;
            effects=EffectsRuntime.Vfx;
            var profile=AssetDatabase.LoadAssetAtPath<PlayerVfxProfile>("Assets/_Game/Data/Presentation/PlayerVfx.asset");
            handle=effects.RegisterOwner(owner.transform,profile,source);
            var type=typeof(PlayerVisual).Assembly.GetType("ActionPlatformer.Player.PlayerMovementObserver");
            Action<MovementDustKind,Vector2,bool> emit=(kind,point,flip)=>effects.PlayDust(handle,kind,point,flip);
            observer=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public,null,new object[]{emit},null);
        }
        [TearDown] public void Cleanup() { effects.ReleaseOwner(handle); Object.DestroyImmediate(owner); }
        private void Tick(Vector2 point,Vector2 velocity,bool ground,uint glitch=0,bool suppressed=false,double offset=0)
        {
            observer.GetType().GetMethod("Tick").Invoke(observer,new object[]{point,velocity,ground,suppressed,glitch,false,Time.unscaledTimeAsDouble+offset});
            effects.SendMessage("LateUpdate");
        }

        [UnityTest] public IEnumerator LandingDustStaysAtContactAndExpiresWithoutScaledTime()
        {
            Call("Land", new Vector2(2, 0), false);
            Tick(Vector2.zero, Vector2.zero, true);
            Assert.That(Visible().Length, Is.EqualTo(1));
            var renderer = Visible()[0];
            Assert.That(renderer.sprite.name, Does.StartWith("GroundDust_"));
            owner.transform.position = new Vector3(10, 2);
            Tick(new Vector2(10, 2), Vector2.zero, true, offset: .08);
            Assert.That(renderer.transform.position.x, Is.EqualTo(2).Within(.001));
            float scale = Time.timeScale;
            try { Time.timeScale = 0; yield return new WaitForSecondsRealtime(.35f); Assert.That(Visible(), Is.Empty); }
            finally { Time.timeScale = scale; }
        }
        [Test] public void GlitchAndUndergroundTransitionsDoNotGenerateLandingDust()
        {
            Tick(Vector2.zero, Vector2.down * 10, false);
            Tick(Vector2.right, Vector2.zero, true, glitch: 1);
            Assert.That(Visible(), Is.Empty);
            Tick(Vector2.zero, Vector2.down * 10, false, glitch: 1, suppressed: true);
            Tick(Vector2.zero, Vector2.zero, true, glitch: 1);
            Assert.That(Visible(), Is.Empty);
        }
        [Test] public void RepeatedDustIsBoundedAndResetClearsAllPuffs()
        {
            for (int i = 0; i < 40; i++) Call("Land", new Vector2(i, 0), false);
            Tick(Vector2.zero, Vector2.zero, true);
            Assert.That(Visible().Length, Is.EqualTo(8));
            Call("Reset"); Assert.That(Visible(), Is.Empty);
            Tick(Vector2.zero, Vector2.zero, true); Assert.That(Visible(), Is.Empty);
        }
        [Test] public void PlayerPrefabHasAllImportedFramesAndKeepsExistingActionEffects()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab");
            var visual = new SerializedObject(prefab.GetComponentInChildren<PlayerVisual>());
            var data = new SerializedObject(visual.FindProperty("vfxProfile").objectReferenceValue);
            foreach (var pair in new[] { ("jumpDustFrames", 8), ("groundDustFrames", 7), ("hitImpactFrames", 8), ("finisherImpactFrames", 8) })
            {
                var field = data.FindProperty(pair.Item1); Assert.That(field.arraySize, Is.EqualTo(pair.Item2));
                for (int i = 0; i < field.arraySize; i++) Assert.That(field.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
            }
            Assert.That(data.FindProperty("basicAttackEffectFrames").arraySize, Is.GreaterThan(0));
            Assert.That(data.FindProperty("glitchEffectFrames").arraySize, Is.GreaterThan(0));
        }
    }
}
#endif
