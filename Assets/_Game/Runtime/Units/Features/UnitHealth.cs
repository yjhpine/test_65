using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Unit))]
    public sealed class UnitHealth : MonoBehaviour
    {
        [Tooltip("Turn off when a death state manages the delay before deactivation.")]
        [SerializeField] private bool deactivateOnDeath = true;
        private Unit owner;
        private bool initialized;

        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; private set; }
        public uint DamageVersion { get; private set; }
        public bool IsAlive => initialized && CurrentHealth > 0;
        public Unit Owner => owner;

        private void Awake()
        {
            owner = GetComponent<Unit>();
            if (owner.Definition == null || !owner.Definition.TryValidate(out _))
            {
                Debug.LogError("Unit health needs a valid Unit Definition.", this);
                enabled = false;
                return;
            }
            MaxHealth = owner.Definition.MaxHealth;
            CurrentHealth = MaxHealth;
            initialized = true;
        }

        public int ApplyDamage(int amount)
        {
            if (amount < 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0 || !IsAlive || !isActiveAndEnabled || !owner.isActiveAndEnabled) return 0;
            int applied = Mathf.Min(amount, CurrentHealth);
            CurrentHealth -= applied;
            unchecked { DamageVersion++; }
            if (!IsAlive && deactivateOnDeath) gameObject.SetActive(false);
            return applied;
        }
    }
}
