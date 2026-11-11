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
        [UnityTest] public IEnumerator HitDebrisOutlivesTheContactAndKillsBurstFromTheBody()
        {
            var target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/TrainingDummy.prefab"),
                new Vector3(40, 40), Quaternion.identity);
            try
            {
                var health = target.GetComponent<ActionPlatformer.Units.Features.UnitHealth>();
                var body = target.GetComponent<Collider2D>();
                Physics2D.SyncTransforms();
                SpriteRenderer Burst() => Visible().SingleOrDefault(r => r.name.StartsWith("Contact burst"));
                var point = new Vector2(39.6f, 40f);
                effects.PlayContact(handle, new PlayerHitPresentation(1, PlayerAttack.Side, health, point, Vector2.left, 10, 0, false));
                effects.SendMessage("LateUpdate");
                Assert.That(Burst(), Is.Not.Null);
                Assert.That(Burst().sprite.name, Does.StartWith("glitch-hit-debris-"));
                Assert.That(Burst().flipX, Is.True, "Debris sprays the way the hit travels.");
                Assert.That(Vector2.Distance(Burst().transform.position, point), Is.LessThan(.001f));
                Assert.That(Burst().bounds.size.x, Is.EqualTo(56f / 18f).Within(.01f), "Drawn at the player art's pixel size.");
                yield return new WaitForSecondsRealtime(.2f); effects.SendMessage("LateUpdate");
                Assert.That(Burst(), Is.Not.Null, "Debris lingers after the contact flash.");
                Assert.That(Visible().Any(r => r.name.StartsWith("Contact fragments")), Is.False);
                yield return new WaitForSecondsRealtime(.35f); effects.SendMessage("LateUpdate");
                Assert.That(Burst(), Is.Null);
                effects.PlayContact(handle, new PlayerHitPresentation(2, PlayerAttack.Side, health, point, Vector2.right, 10, 10, true));
                effects.SendMessage("LateUpdate");
                Assert.That(Burst().sprite.name, Does.StartWith("glitch-shatter-"));
                Assert.That(Vector2.Distance(Burst().transform.position, body.bounds.center), Is.LessThan(.001f), "A kill bursts where the body stood.");
                effects.ResetOwner(handle);
                Assert.That(Burst(), Is.Null);
            }
            finally { Object.DestroyImmediate(target); }
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
