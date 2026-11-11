using UnityEngine;
namespace ActionPlatformer.Units.Features
{
    // Scene-facing adapter; all beam renderers and playback belong to the global VFX manager.
    [DisallowMultipleComponent]
    public sealed class EnemyLaserVisual : MonoBehaviour
    {
        private EnemyVisual2D visual;
        private EnemyVisual2D Visual => visual!=null?visual:(visual=GetComponent<EnemyVisual2D>());
        public bool IsConfigured => Visual!=null && Visual.EffectsProfile!=null && Visual.EffectsProfile.HasLaser;
        public Color TelegraphColor => IsConfigured?Visual.EffectsProfile.TelegraphColor:Color.white;
        public Transform SessionRoot => Visual?.Effects?.GetSessionRoot(Visual.EffectOwner);
        public void Initialize(SpriteRenderer source) { visual=GetComponent<EnemyVisual2D>(); }
        public void ShowCharge(EnemyBeam beam,Vector2 emitter,bool vertical,float progress)
        { if(isActiveAndEnabled)Visual?.Effects?.SubmitEnemyBeam(Visual.EffectOwner,beam,emitter,vertical,progress,false); }
        public bool ShowShot(EnemyBeam beam,Vector2 emitter,bool vertical,float progress)
            => isActiveAndEnabled && IsConfigured && Visual.Effects!=null && Visual.Effects.SubmitEnemyBeam(Visual.EffectOwner,beam,emitter,vertical,progress,true);
        public void Hide() => Visual?.Effects?.HideEnemy(Visual.EffectOwner);
        private void OnDisable() => Hide();
    }
}
