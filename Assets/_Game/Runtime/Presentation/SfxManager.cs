using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ActionPlatformer.Audio;
using ActionPlatformer.Player;
namespace ActionPlatformer.Presentation
{
    public readonly struct SoundCue
    {
        public readonly AudioClip Clip; public readonly float Volume;
        public SoundCue(AudioClip clip,float volume) { Clip=clip; Volume=volume; }
    }
    [DefaultExecutionOrder(10000)]
    public sealed class SfxManager : MonoBehaviour
    {
        private sealed class VoiceBank { public GameObject Root; public ShortSoundPool Pool; public bool Spatial; public EnemySfxProfile Enemy; }
        private EffectSessions<VoiceBank> sessions;
        private readonly List<VoiceBank> idle=new List<VoiceBank>();
        private void Awake() { sessions=new EffectSessions<VoiceBank>(GetInstanceID()); }
        private void OnEnable() { SceneManager.sceneUnloaded+=OnSceneUnloaded; }
        private void OnDisable() { SceneManager.sceneUnloaded-=OnSceneUnloaded; ClearAll(); }
        private void OnApplicationQuit() => EffectsRuntime.Quitting();
        public EffectOwnerHandle RegisterOwner(Transform owner, PlayerSfxDefinition profile) => profile==null ? default : Register(owner,false,null);
        public EffectOwnerHandle RegisterOwner(Transform owner, EnemySfxProfile profile) => profile==null ? default : Register(owner,true,profile);
        private EffectOwnerHandle Register(Transform owner,bool spatial,EnemySfxProfile enemy)
        {
            if(owner==null || !owner.gameObject.activeInHierarchy)return default;
            int i=idle.FindIndex(b=>b.Spatial==spatial); VoiceBank bank;
            if(i>=0) { bank=idle[i]; idle.RemoveAt(i); }
            else {
                var go=new GameObject(spatial?"Enemy SFX session":"Player SFX session"); go.transform.SetParent(transform,false);
                bank=new VoiceBank { Root=go,Spatial=spatial,Pool=new ShortSoundPool(go.transform,spatial?2:6,spatial) };
            }
            bank.Enemy=enemy; bank.Root.SetActive(true); bank.Root.transform.position=owner.position;
            return sessions.Add(owner,bank);
        }
        public bool IsValid(EffectOwnerHandle handle) => sessions.Get(handle)!=null;
        public Transform GetSessionRoot(EffectOwnerHandle handle) => sessions.Get(handle)?.Value.Root.transform;
        public void Play(EffectOwnerHandle handle, SoundCue cue)
        {
            var s=sessions.Get(handle); if(s==null || s.Owner==null || !s.Owner.gameObject.activeInHierarchy)return;
            s.Value.Root.transform.position=s.Owner.position; s.Value.Pool.Play(cue.Clip,cue.Volume);
        }
        public void PlayShot(EffectOwnerHandle handle)
        {
            var p=sessions.Get(handle)?.Value.Enemy;
            if(p!=null)Play(handle,new SoundCue(p.ShotSound,p.ShotVolume));
        }
        public void ResetOwner(EffectOwnerHandle handle) => sessions.Get(handle)?.Value.Pool.Stop();
        public void ReleaseOwner(EffectOwnerHandle handle) { var s=sessions.Get(handle); if(s!=null)Release(s); }
        private void Release(EffectSessions<VoiceBank>.Slot s)
        {
            var b=s.Value; b.Pool.Stop(); b.Enemy=null; b.Root.SetActive(false); b.Root.transform.position=Vector3.zero;
            idle.Add(b); sessions.Clear(s);
        }
        private void LateUpdate()
        {
            foreach(var s in sessions.Slots)if(s.Value!=null) {
                if(s.Owner==null || !s.Owner.gameObject.activeInHierarchy)Release(s);
                else s.Value.Root.transform.position=s.Owner.position;
            }
        }
        private void OnSceneUnloaded(Scene scene)
        {
            foreach(var s in sessions.Slots)if(s.Value!=null && (s.Owner==null || s.Owner.gameObject.scene.handle==scene.handle || s.Scene==scene.handle))Release(s);
            foreach(var b in idle)Destroy(b.Root); idle.Clear();
        }
        private void ClearAll()
        {
            if(sessions==null)return;
            foreach(var s in sessions.Slots)if(s.Value!=null)Release(s);
            foreach(var b in idle)if(b.Root!=null)Destroy(b.Root); idle.Clear();
        }
    }
}
