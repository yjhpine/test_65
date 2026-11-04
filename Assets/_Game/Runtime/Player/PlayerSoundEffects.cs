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
        private double lastHit = double.NegativeInfinity, lastHurt = double.NegativeInfinity;
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
        // Strikes of a live chain rise a little; every swing and hit varies slightly so repeats never sound identical.
        public const float StrikeRise = .05f, SwingSpread = .04f, HitSpread = .06f, HeavySpread = .03f, HeavyDrop = .97f;
        private static float Vary(float spread) => 1f + Random.Range(-spread, spread);
        private void Play(AudioClip clip,float volume,float pitch=1f) { EnsureSession(); manager?.Play(handle,new SoundCue(clip,volume,pitch)); }
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
                {
                    // The combo's strong strike always uses the heavy swing, whatever its attack kind, a touch lower.
                    bool basic = (attack.Kind == PlayerAttack.Side || attack.Kind == PlayerAttack.Air) && !attack.Finisher;
                    float pitch = attack.Finisher ? HeavyDrop * Vary(HeavySpread)
                        : (1f + StrikeRise * Mathf.Max(0, attack.Strike - 1)) * Vary(basic ? SwingSpread : HeavySpread);
                    Play(basic ? Next(settings.Swings, ref swingIndex) : settings.HeavySwing, settings.SwingVolume, pitch);
                }
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
            Play(heavy ? settings.HeavyHit : Next(settings.Hits, ref hitIndex), settings.HitVolume,
                heavy ? HeavyDrop * Vary(HeavySpread) : Vary(HitSpread));
        }
        public void Land(Vector2 point, bool slam)
        {
            if (settings == null) return;
            Play(slam ? settings.SlamLanding : Next(settings.Landings, ref landingIndex),
                slam ? settings.HitVolume : settings.LandingVolume);
        }
        public void PlayGlitch() { if (settings != null) Play(settings.Glitch, settings.GlitchVolume); }
        // Losing health. Blows landing together sound once.
        public void Hurt()
        {
            if (settings == null || Time.unscaledTimeAsDouble - lastHurt < .05) return;
            lastHurt = Time.unscaledTimeAsDouble;
            Play(settings.Hurt, settings.HurtVolume, Vary(HeavySpread));
        }
        public void Reset(IPlayerActionState state)
        {
            manager?.ResetOwner(handle); attackVersion = state?.AttackPresentation.Version ?? 0;
            glitchVersion = state?.GlitchPresentation.Version ?? 0;
            swingIndex = hitIndex = landingIndex = 0; lastHit = lastHurt = double.NegativeInfinity;
        }
    }
}
