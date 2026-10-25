using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class UnitDamageFlash : MonoBehaviour
    {
        private UnitHealth health;
        private SpriteRenderer sprite;
        private double started = double.NegativeInfinity;
        private MaterialPropertyBlock properties;
        private void Awake() { properties = new MaterialPropertyBlock(); sprite = GetComponent<SpriteRenderer>(); health = GetComponentInParent<UnitHealth>(); }
        private void OnEnable() { if (health != null) health.DamageApplied += OnDamage; }
        private void OnDamage(UnitDamageResult result) { if (result.AcceptedDamage > 0) started = Time.unscaledTimeAsDouble; }
        private void LateUpdate() => Apply(Mathf.Clamp01(1f - (float)(Time.unscaledTimeAsDouble - started) / .06f));
        private void Apply(float amount)
        {
            if (sprite == null || properties == null) return;
            sprite.GetPropertyBlock(properties); properties.SetFloat("_HitFlash", amount); sprite.SetPropertyBlock(properties);
        }
        private void OnDisable()
        {
            if (health != null) health.DamageApplied -= OnDamage;
            started = double.NegativeInfinity; Apply(0);
        }
    }
}
