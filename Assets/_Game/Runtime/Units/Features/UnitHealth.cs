using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Unit))]
    public sealed class UnitHealth : MonoBehaviour
    {
        [Tooltip("Turn off when a death state manages the delay before deactivation.")]
        [SerializeField] private bool deactivateOnDeath = true;
        [Tooltip("Accept hits and damage reactions without losing health, for training targets.")]
        [SerializeField] private bool preserveHealthOnDamage;
        private Unit owner;
        private bool initialized;

        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; private set; }
        public uint DamageVersion { get; private set; }
        public bool IsAlive => initialized && CurrentHealth > 0;
        public Unit Owner => owner;
        public bool DamageEnabled { get; private set; } = true;
        public bool CanReceiveDamage => DamageEnabled && IsAlive && isActiveAndEnabled && owner != null && owner.isActiveAndEnabled;

        public void SetDamageEnabled(bool value) => DamageEnabled = value;

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
            if (amount == 0 || !CanReceiveDamage) return 0;
            int applied = Mathf.Min(amount, CurrentHealth);
            if (!preserveHealthOnDamage) CurrentHealth -= applied;
            unchecked { DamageVersion++; }
            if (!IsAlive && deactivateOnDeath) gameObject.SetActive(false);
            return applied;
        }
    }
}
