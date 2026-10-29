using UnityEngine;
using ActionPlatformer.Presentation;

namespace ActionPlatformer.Player
{
    internal sealed class PlayerSoundEffects
    {
        private readonly PlayerSfxDefinition settings;
        private readonly Transform owner;
        private SfxManager manager;
        private EffectOwnerHandle handle;
        private uint attackVersion, glitchVersion;
        private int swingIndex, hitIndex, landingIndex;
        private double lastHit = double.NegativeInfinity;
        public PlayerSoundEffects(Transform owner, PlayerSfxDefinition settings)
        {
            this.settings = settings;
            this.owner = owner;
            EnsureSession();
        }
        public Transform SessionRoot => manager?.GetSessionRoot(handle);
        private void EnsureSession()
        {
            if(manager!=null && manager.IsValid(handle))return;
            manager=EffectsRuntime.Sfx; if(manager!=null)handle=manager.RegisterOwner(owner,settings);
        }
        private void Play(AudioClip clip,float volume) { EnsureSession(); manager?.Play(handle,new SoundCue(clip,volume)); }
        public void Release() { manager?.ReleaseOwner(handle); handle=default; }
        private static AudioClip Next(AudioClip[] clips, ref int index)
            => clips != null && clips.Length > 0 ? clips[index++ % clips.Length] : null;
        public void Observe(IPlayerActionState state)
        {
            if (state == null || settings == null) return;
            var attack = state.AttackPresentation;
            if (attack.Version != attackVersion)
            {
                attackVersion = attack.Version;
                if (state.AttackPhase != PlayerAttackPhase.Ready && state.ReactionPresentation.Kind != PlayerReaction.Die)
                    Play(attack.Kind == PlayerAttack.Side || attack.Kind == PlayerAttack.Air
                        ? Next(settings.Swings, ref swingIndex) : settings.HeavySwing, settings.SwingVolume);
            }
            if (state.GlitchPresentation.Version != glitchVersion)
            {
                glitchVersion = state.GlitchPresentation.Version;
                PlayGlitch();
            }
        }
        public void Hit(PlayerHitPresentation hit)
        {
            if (settings == null || hit.Damage <= 0 || Time.unscaledTimeAsDouble - lastHit < .04) return;
            lastHit = Time.unscaledTimeAsDouble;
            bool heavy = hit.Killed || hit.Kind == PlayerAttack.Slam || hit.Kind == PlayerAttack.Shockwave;
            Play(heavy ? settings.HeavyHit : Next(settings.Hits, ref hitIndex), settings.HitVolume);
        }
        public void Land(Vector2 point, bool slam)
        {
            if (settings == null) return;
            Play(slam ? settings.SlamLanding : Next(settings.Landings, ref landingIndex),
                slam ? settings.HitVolume : settings.LandingVolume);
        }
        public void PlayGlitch() { if (settings != null) Play(settings.Glitch, settings.GlitchVolume); }
        public void Reset(IPlayerActionState state)
        {
            manager?.ResetOwner(handle); attackVersion = state?.AttackPresentation.Version ?? 0;
            glitchVersion = state?.GlitchPresentation.Version ?? 0;
            swingIndex = hitIndex = landingIndex = 0; lastHit = double.NegativeInfinity;
        }
    }
}
