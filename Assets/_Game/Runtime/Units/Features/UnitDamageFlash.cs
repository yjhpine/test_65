using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class UnitDamageFlash : MonoBehaviour
    {
        // Visual-only shudder: at least this long, and for as long as the hit stop keeps the world frozen.
        private const float MinimumShake = .05f, MaximumShake = .2f, ShakeRate = 40f;
        private UnitHealth health;
        private SpriteRenderer sprite;
        private double started = double.NegativeInfinity;
        private MaterialPropertyBlock properties;
        private bool shakes;
        private Vector3 restPosition;
        private float shakeSide = 1f;
        private void Awake()
        {
            properties = new MaterialPropertyBlock(); sprite = GetComponent<SpriteRenderer>(); health = GetComponentInParent<UnitHealth>();
            // Only the unit's own Visual moves; part renderers under it (turret head and base) follow it.
            shakes = health != null && transform.parent == health.transform;
            restPosition = transform.localPosition;
        }
        private void OnEnable() { if (health != null) health.DamageApplied += OnDamage; }
        private void OnDamage(UnitDamageResult result)
        {
            if (result.AcceptedDamage <= 0) return;
            started = Time.unscaledTimeAsDouble;
            // Pushed away from the attacker first, along the hit's horizontal direction.
            shakeSide = result.HasDirection && result.Direction.x < 0f ? -1f : 1f;
        }
        private void LateUpdate()
        {
            float age = (float)(Time.unscaledTimeAsDouble - started);
            Apply(Mathf.Clamp01(1f - age / .06f));
            if (!shakes) return;
            bool shaking = age >= 0f && (age < MinimumShake || (Time.timeScale <= 0f && age < MaximumShake));
            float pixel = sprite.sprite != null ? 1f / sprite.sprite.pixelsPerUnit : 1f / 16f;
            float side = Mathf.FloorToInt(age * ShakeRate) % 2 == 0 ? shakeSide : -shakeSide;
            transform.localPosition = shaking ? restPosition + Vector3.right * (side * pixel) : restPosition;
        }
        private void Apply(float amount)
        {
            if (sprite == null || properties == null) return;
            sprite.GetPropertyBlock(properties); properties.SetFloat("_HitFlash", amount); sprite.SetPropertyBlock(properties);
        }
        private void OnDisable()
        {
            if (health != null) health.DamageApplied -= OnDamage;
            started = double.NegativeInfinity; Apply(0);
            if (shakes) transform.localPosition = restPosition;
        }
    }
}
