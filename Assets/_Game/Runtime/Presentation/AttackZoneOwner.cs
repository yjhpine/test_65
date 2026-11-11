using UnityEngine;
namespace ActionPlatformer.Presentation
{
    // What a monster visual holds to show its attack danger zone. The renderer belongs to VfxManager;
    // the visual only says where the attack lands and how close it is (urgency 0..1), once per frame.
    public sealed class AttackZoneOwner
    {
        private VfxManager effects;
        private EffectOwnerHandle handle;
        public Transform SessionRoot => effects == null ? null : effects.GetSessionRoot(handle);
        public void Ensure(Transform owner, AttackZoneProfile profile, SpriteRenderer source)
        {
            if (profile == null || (effects != null && effects.IsValid(handle))) return;
            effects = EffectsRuntime.Vfx;
            if (effects != null) handle = effects.RegisterOwner(owner, profile, source);
        }
        public void Show(Vector2 center, Vector2 size, float angle, float urgency)
        {
            if (effects != null) effects.ShowAttackZone(handle, center, size, angle, urgency);
        }
        public void Hide() { if (effects != null) effects.HideAttackZone(handle); }
        public void Release()
        {
            if (effects != null) { effects.HideAttackZone(handle); effects.ReleaseOwner(handle); }
            effects = null; handle = default;
        }
    }
}
