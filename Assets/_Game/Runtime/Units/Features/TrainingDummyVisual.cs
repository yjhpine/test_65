using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    // Cosmetic hit feedback only. UnitHealth and GroundMovement2D own gameplay state.
    public sealed class TrainingDummyVisual : MonoBehaviour
    {
        [SerializeField] private UnitHealth health;
        [SerializeField, Min(0.01f)] private float hitDuration = 0.15f;
        private SpriteRenderer[] parts;
        private Color[] originalColors;
        private uint observedDamage;
        private float hitRemaining;

        private void Awake()
        {
            parts = GetComponentsInChildren<SpriteRenderer>();
            originalColors = new Color[parts.Length];
            for (int i = 0; i < parts.Length; i++) originalColors[i] = parts[i].color;
            if (health == null)
            {
                Debug.LogError("Training dummy visual needs a UnitHealth reference.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (health != null) observedDamage = health.DamageVersion;
        }

        private void LateUpdate()
        {
            if (health == null) return;
            if (health.DamageVersion != observedDamage)
            {
                observedDamage = health.DamageVersion;
                hitRemaining = hitDuration;
            }
            float blend = Mathf.Clamp01(hitRemaining / Mathf.Max(0.01f, hitDuration));
            for (int i = 0; i < parts.Length; i++)
                parts[i].color = Color.Lerp(originalColors[i], new Color(1f, 0.4f, 0.15f), blend);
            hitRemaining = Mathf.Max(0f, hitRemaining - Time.deltaTime);
        }

        private void OnDisable()
        {
            hitRemaining = 0f;
            if (parts == null) return;
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) parts[i].color = originalColors[i];
        }
    }
}
