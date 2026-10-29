using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
namespace ActionPlatformer.Presentation
{
    public enum MovementDustKind { Jump,Brake,Trail,Landing,SlamLanding }
    public enum EnemyEffectShape { Shield, Scan, Beam }
    [DefaultExecutionOrder(10000)]
    public sealed class VfxManager : MonoBehaviour
    {
        private struct ShapeRequest { public bool Visible; public Vector2 Position,Size; public float Angle; public Color Color; }
        private sealed class Bank
        {
            public GameObject Root; public Object Profile; public Material Material; public int Layer,Order;
            public PlayerActionEffects Action; public PlayerContactEffects Contact; public PlayerMovementEffects Movement;
            public EnemyBeamEffects Laser; public SpriteRenderer[] Shapes;
            public IPlayerActionState Actions; public int Variant,Frame=-1; public bool BlockAttack;
            public ShapeRequest[] Requests=new ShapeRequest[3];
            public bool Shot,Charge,Vertical; public EnemyBeam Beam; public Vector2 Emitter; public float Progress;
        }
        private EffectSessions<Bank> sessions;
        private readonly HashSet<Object> invalidProfiles=new HashSet<Object>();
        private readonly List<Bank> idle=new List<Bank>();
        private void Awake() { sessions=new EffectSessions<Bank>(GetInstanceID()); }
        private void OnEnable() { SceneManager.sceneUnloaded+=OnSceneUnloaded; }
        private void OnDisable() { SceneManager.sceneUnloaded-=OnSceneUnloaded; ClearAll(); }
        private void OnApplicationQuit() => EffectsRuntime.Quitting();
        public bool IsValid(EffectOwnerHandle handle) => sessions.Get(handle)!=null;
        public Transform GetSessionRoot(EffectOwnerHandle handle) => sessions.Get(handle)?.Value.Root.transform;
        private Bank Allocate(Transform owner,Object profile,SpriteRenderer source)
        {
            int i=idle.FindIndex(b=>b.Profile==profile && b.Material==source.sharedMaterial && b.Layer==source.sortingLayerID && b.Order==source.sortingOrder);
            Bank bank;
            if(i>=0) { bank=idle[i]; idle.RemoveAt(i); }
            else {
                var root=new GameObject(profile.name+" VFX session"); root.transform.SetParent(transform,false);
                bank=new Bank { Root=root,Profile=profile,Material=source.sharedMaterial,Layer=source.sortingLayerID,Order=source.sortingOrder };
                if(profile is PlayerVfxProfile p) {
                    bank.Action=new PlayerActionEffects(root.transform,source,p.BasicAttackEffectFrames,p.GlitchEffectFrames,p.BasicAttackEffectSize,p.GlitchEffectSize,p.GlitchEffectFps,p.GlitchTrailSpacing,p.GlitchTrailInterval,p.ActionEffectMaterial);
                    bank.Contact=new PlayerContactEffects(root.transform,source,p.GlitchEffectFrames,p.ActionEffectMaterial,p.HitImpactFrames,p.FinisherImpactFrames);
                    bank.Movement=new PlayerMovementEffects(root.transform,source,p.JumpDustFrames,p.GroundDustFrames,p.ActionEffectMaterial);
                } else if(profile is EnemyVfxProfile e) {
                    bank.Laser=new EnemyBeamEffects(root.transform,e,source);
                    bank.Shapes=new SpriteRenderer[3];
                    for(int n=0;n<3;n++) {
                        var go=new GameObject(new[]{"Energy shield","Scan progress","Attack telegraph"}[n]); go.transform.SetParent(root.transform,false);
                        var r=go.AddComponent<SpriteRenderer>(); r.sprite=e.ShapeSprite; r.sharedMaterial=source.sharedMaterial; r.sortingOrder=source.sortingOrder+2; r.enabled=false; bank.Shapes[n]=r;
                    }
                }
            }
            bank.Root.SetActive(true); Sync(bank,owner); return bank;
        }
        public EffectOwnerHandle RegisterOwner(Transform owner,PlayerVfxProfile profile,SpriteRenderer source)
            => owner==null || profile==null || source==null || !Validate(profile) ? default : sessions.Add(owner,Allocate(owner,profile,source));
        public EffectOwnerHandle RegisterOwner(Transform owner,EnemyVfxProfile profile,SpriteRenderer source)
            => owner==null || profile==null || source==null || !Validate(profile) ? default : sessions.Add(owner,Allocate(owner,profile,source));
        private bool Validate(Object profile)
        {
            bool valid=profile is PlayerVfxProfile p?EffectProfileValidation.Valid(p):EffectProfileValidation.Valid((EnemyVfxProfile)profile);
            if(!valid && invalidProfiles.Add(profile))Debug.LogWarning("Invalid VFX profile; presentation skipped: "+profile.name,profile);
            return valid;
        }
        private static void Sync(Bank b,Transform owner) { b.Root.transform.SetPositionAndRotation(owner.position,owner.rotation); b.Root.transform.localScale=owner.lossyScale; }
        private Bank Get(EffectOwnerHandle h)
        {
            var s=sessions.Get(h); if(s==null || s.Owner==null || !s.Owner.gameObject.activeInHierarchy)return null;
            Sync(s.Value,s.Owner); return s.Value;
        }
        public void SubmitAttack(EffectOwnerHandle h,IPlayerActionState actions,int variant,bool blocked)
        { var b=Get(h); if(b==null)return; b.Actions=actions; b.Variant=variant; b.BlockAttack=blocked; b.Frame=Time.frameCount; }
        public void PlayTransferSpark(EffectOwnerHandle h,Vector2 point) => Get(h)?.Action?.Burst(point);
        public void PlayContact(EffectOwnerHandle h,PlayerHitPresentation hit) { if(hit.Damage>0)Get(h)?.Contact?.Spawn(hit); }
        public void PlayDust(EffectOwnerHandle h,Vector2 point,bool slam)
        {
            var b=Get(h); if(b==null)return; b.Movement?.Land(point,slam);
            if(slam && b.Profile is PlayerVfxProfile p && (p.GroundDustFrames==null || p.GroundDustFrames.Length==0))b.Contact?.Land(point);
        }
        public void PlayDust(EffectOwnerHandle h,MovementDustKind kind,Vector2 point,bool flip=false)
            => Get(h)?.Movement?.Play(kind,point,flip);
        public void PlayGlitch(EffectOwnerHandle h,PlayerGlitchPresentation glitch) => Get(h)?.Action?.PlayGlitch(glitch);
        public void BeginEnemyFrame(EffectOwnerHandle h)
        {
            var b=Get(h); if(b==null)return;
            for(int i=0;i<3;i++)b.Requests[i]=default;
            b.Shot=b.Charge=false; b.Frame=Time.frameCount;
        }
        public void SubmitShape(EffectOwnerHandle h,EnemyEffectShape shape,Vector2 position,Vector2 size,float angle,Color color)
        { var b=Get(h); if(b!=null)b.Requests[(int)shape]=new ShapeRequest { Visible=true,Position=position,Size=size,Angle=angle,Color=color }; }
        public bool SubmitEnemyBeam(EffectOwnerHandle h,EnemyBeam beam,Vector2 emitter,bool vertical,float progress,bool shot)
        {
            var b=Get(h); if(b?.Laser==null || !b.Laser.IsConfigured)return false;
            b.Frame=Time.frameCount; b.Shot=shot; b.Charge=!shot; b.Beam=beam; b.Emitter=emitter; b.Vertical=vertical; b.Progress=progress; return true;
        }
        public void HideEnemy(EffectOwnerHandle h)
        { var b=Get(h); if(b==null)return; BeginEnemyFrame(h); b.Laser?.Hide(); if(b.Shapes!=null)foreach(var r in b.Shapes)r.enabled=false; }
        private void LateUpdate()
        {
            foreach(var s in sessions.Slots) {
                if(s.Value==null)continue;
                if(s.Owner==null || !s.Owner.gameObject.activeInHierarchy) { Release(s); continue; }
                var b=s.Value; Sync(b,s.Owner);
                b.Contact?.Tick(Time.unscaledTimeAsDouble); b.Movement?.Render(Time.unscaledTimeAsDouble);
                if(b.Action!=null) {
                    if(b.Frame==Time.frameCount)b.Action.Tick(b.Actions,Time.unscaledDeltaTime,b.Variant,b.BlockAttack);
                    else b.Action.Reset(b.Actions?.GlitchPresentation.Version??0);
                }
                if(b.Shapes==null)continue;
                b.Laser.Hide();
                for(int i=0;i<3;i++) {
                    var r=b.Shapes[i]; var q=b.Requests[i]; r.enabled=b.Frame==Time.frameCount && q.Visible && r.sprite!=null;
                    if(!r.enabled)continue;
                    r.color=q.Color; r.transform.SetPositionAndRotation(new Vector3(q.Position.x,q.Position.y,b.Root.transform.position.z),Quaternion.Euler(0,0,q.Angle));
                    var size=r.sprite.bounds.size; r.transform.localScale=new Vector3(q.Size.x/size.x,q.Size.y/size.y,1);
                }
                if(b.Frame!=Time.frameCount)continue;
                if(b.Shot)b.Laser.ShowShot(b.Beam,b.Emitter,b.Vertical,b.Progress);
                else if(b.Charge)b.Laser.ShowCharge(b.Beam,b.Emitter,b.Vertical,b.Progress);
            }
        }
        public void ResetOwner(EffectOwnerHandle h,uint glitchVersion=0)
        { var s=sessions.Get(h); if(s!=null)Reset(s.Value,glitchVersion); }
        private static void Reset(Bank b,uint version)
        {
            b.Action?.Reset(version); b.Contact?.Reset(); b.Movement?.Reset(); b.Laser?.Hide();
            if(b.Shapes!=null)foreach(var r in b.Shapes)r.enabled=false;
            b.Actions=null; b.Frame=-1; b.Shot=b.Charge=false;
            for(int i=0;i<3;i++)b.Requests[i]=default;
        }
        public void ReleaseOwner(EffectOwnerHandle h) { var s=sessions.Get(h); if(s!=null)Release(s); }
        private void Release(EffectSessions<Bank>.Slot s)
        {
            var b=s.Value; Reset(b,0); b.Root.SetActive(false); b.Root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); b.Root.transform.localScale=Vector3.one;
            idle.Add(b); sessions.Clear(s);
        }
        private void OnSceneUnloaded(Scene scene)
        {
            foreach(var s in sessions.Slots)if(s.Value!=null && (s.Owner==null || s.Scene==scene.handle))Release(s);
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
