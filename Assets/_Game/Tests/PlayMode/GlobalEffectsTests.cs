#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ActionPlatformer.Player;
using ActionPlatformer.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ActionPlatformer.Tests
{
    public sealed class GlobalEffectsTests
    {
        private readonly List<GameObject> owners=new List<GameObject>();
        private readonly List<EffectOwnerHandle> visualHandles=new List<EffectOwnerHandle>(),soundHandles=new List<EffectOwnerHandle>();
        private VfxManager Vfx=>EffectsRuntime.Vfx;
        private SfxManager Sfx=>EffectsRuntime.Sfx;
        private PlayerVfxProfile VisualProfile=>AssetDatabase.LoadAssetAtPath<PlayerVfxProfile>("Assets/_Game/Data/Presentation/PlayerVfx.asset");
        private PlayerSfxDefinition SoundProfile=>AssetDatabase.LoadAssetAtPath<PlayerSfxDefinition>("Assets/_Game/Audio/Combat/PlayerSfx.asset");
        private GameObject Owner()
        { var o=new GameObject("Effect owner test");o.AddComponent<SpriteRenderer>().enabled=false;owners.Add(o);return o; }
        private EffectOwnerHandle Visual(GameObject o)
        {var h=Vfx.RegisterOwner(o.transform,VisualProfile,o.GetComponent<SpriteRenderer>());visualHandles.Add(h);return h;}
        private EffectOwnerHandle Sound(GameObject o)
        {var h=Sfx.RegisterOwner(o.transform,SoundProfile);soundHandles.Add(h);return h;}
        private SpriteRenderer[] Visible(EffectOwnerHandle h)=>Vfx.GetSessionRoot(h).GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled&&r.sprite!=null).ToArray();
        [TearDown] public void Cleanup()
        {
            foreach(var h in visualHandles)Vfx.ReleaseOwner(h);
            foreach(var h in soundHandles)Sfx.ReleaseOwner(h);
            foreach(var o in owners)if(o!=null)Object.DestroyImmediate(o);
            visualHandles.Clear();soundHandles.Clear();owners.Clear();
        }
        [Test] public void BootstrapIsUniqueAndResourcesAreNotChildrenOfActors()
        {
            var o=Owner();var v=Visual(o);var a=Sound(o);
            Assert.That(Object.FindObjectsByType<VfxManager>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<SfxManager>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(Vfx.GetSessionRoot(v).IsChildOf(Vfx.transform),Is.True);
            Assert.That(Sfx.GetSessionRoot(a).IsChildOf(Sfx.transform),Is.True);
            Assert.That(o.GetComponentsInChildren<AudioSource>(),Is.Empty);
            Assert.That(o.GetComponentsInChildren<SpriteRenderer>().Length,Is.EqualTo(1));
        }
        [Test] public void ReleasedHandlesCannotAffectReusedBanksAndOwnersKeepSeparateQuotas()
        {
            var first=Owner();var h=Visual(first);var root=Vfx.GetSessionRoot(h);
            Vfx.ReleaseOwner(h);var other=Visual(first);Assert.That(Vfx.GetSessionRoot(other),Is.SameAs(root));
            Vfx.PlayDust(other,Vector2.right,false);Vfx.ResetOwner(h);Vfx.ReleaseOwner(h);Vfx.SendMessage("LateUpdate");
            Assert.That(Visible(other).Length,Is.EqualTo(1));
            var a=Sound(first);var audioRoot=Sfx.GetSessionRoot(a);Sfx.ReleaseOwner(a);var a2=Sound(first);
            Assert.That(Sfx.GetSessionRoot(a2),Is.SameAs(audioRoot));
            var second=Sound(Owner());Sfx.Play(second,new SoundCue(SoundProfile.Glitch,.3f));
            for(int i=0;i<30;i++)Sfx.Play(a2,new SoundCue(SoundProfile.HeavyHit,.2f));
            Sfx.ResetOwner(a);Sfx.ReleaseOwner(a);
            Assert.That(Sfx.GetSessionRoot(a2).GetComponentsInChildren<AudioSource>().Length,Is.EqualTo(6));
            Assert.That(Sfx.GetSessionRoot(second).GetComponentsInChildren<AudioSource>().Any(s=>s.clip==SoundProfile.Glitch),Is.True);
            Assert.That(Sfx.GetSessionRoot(a2).GetComponentsInChildren<AudioSource>().Any(s=>s.clip!=null),Is.True);
        }
        [UnityTest] public IEnumerator DestroyedAndInactiveOwnersReleaseTheirResources()
        {
            var o=Owner();var h=Visual(o);var a=Sound(o);Vfx.PlayDust(h,Vector2.zero,false);
            o.SetActive(false);yield return null;yield return null;
            Assert.That(Vfx.IsValid(h)||Sfx.IsValid(a),Is.False);
            o.SetActive(true);var next=Visual(o);var sound=Sound(o);
            Vfx.SendMessage("LateUpdate");Assert.That(Visible(next),Is.Empty);
            Object.Destroy(o);yield return null;yield return null;
            Assert.That(Vfx.IsValid(next)||Sfx.IsValid(sound),Is.False);
        }
        [UnityTest] public IEnumerator SceneUnloadKeepsManagersAndOtherSceneSessions()
        {
            var kept=Owner();var keep=Visual(kept);var keepSound=Sound(kept);
            var scene=SceneManager.CreateScene("Effects lifetime test");var removed=Owner();SceneManager.MoveGameObjectToScene(removed,scene);
            var gone=Visual(removed);var goneSound=Sound(removed);var manager=Vfx;
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return null; // Unity destroys the retired banks at the end of the unload frame.
            Assert.That(Vfx,Is.SameAs(manager));Assert.That(Vfx.IsValid(keep)&&Sfx.IsValid(keepSound),Is.True);
            Assert.That(Vfx.IsValid(gone)||Sfx.IsValid(goneSound),Is.False);
            Assert.That(Vfx.transform.Cast<Transform>().All(t=>t.gameObject.activeSelf),Is.True,"Unused banks must be trimmed at scene unload.");
        }
        [UnityTest] public IEnumerator DustExpiresDuringPauseAndListenerPauseRejectsNewSounds()
        {
            var o=Owner();var h=Visual(o);var a=Sound(o);float scale=Time.timeScale;bool pause=AudioListener.pause;
            try {
                Vfx.PlayDust(h,Vector2.one,false);Time.timeScale=0;AudioListener.pause=true;
                Sfx.Play(a,new SoundCue(SoundProfile.Glitch,.3f));Vfx.SendMessage("LateUpdate");
                Assert.That(Visible(h).Length,Is.EqualTo(1));Assert.That(Sfx.GetSessionRoot(a).GetComponentsInChildren<AudioSource>().All(s=>s.clip==null),Is.True);
                yield return new WaitForSecondsRealtime(.35f);Assert.That(Visible(h),Is.Empty);
            }finally{Time.timeScale=scale;AudioListener.pause=pause;}
        }
        [UnityTest] public IEnumerator MissingContinuousSubmissionHidesEnemyBeamOnNextFrame()
        {
            var o=Owner();var profile=AssetDatabase.LoadAssetAtPath<EnemyVfxProfile>("Assets/_Game/Data/Presentation/FlyingDroneVfx.asset");
            var h=Vfx.RegisterOwner(o.transform,profile,o.GetComponent<SpriteRenderer>());visualHandles.Add(h);
            Vfx.SubmitEnemyBeam(h,new ActionPlatformer.Units.Features.EnemyBeam(Vector2.zero,Vector2.right*3,.1f),Vector2.zero,false,.5f,true);
            Vfx.SendMessage("LateUpdate");Assert.That(Visible(h).Length,Is.GreaterThan(0));
            yield return null;yield return null;Assert.That(Visible(h),Is.Empty);
        }
    }
}
#endif
