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
        private IUnitDamageGuard damageGuard;

        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; private set; }
        public uint DamageVersion { get; private set; }
        public UnitDamageResult LastDamage { get; private set; }
        public event System.Action<UnitDamageResult> DamageApplied;
        public bool IsAlive => initialized && CurrentHealth > 0;
        public Unit Owner => owner;
        public bool DamageEnabled { get; private set; } = true;
        public bool CanReceiveDamage => DamageEnabled && IsAlive && isActiveAndEnabled && owner != null && owner.isActiveAndEnabled;

        public void SetDamageEnabled(bool value) => DamageEnabled = value;

        // Recovery is not a damage transaction and never revives an uninitialized/dead unit.
        public int RestoreToFull()
        {
            if (!IsAlive || !isActiveAndEnabled || owner == null || !owner.isActiveAndEnabled) return 0;
            int restored = MaxHealth - CurrentHealth;
            CurrentHealth = MaxHealth;
            return restored;
        }

        private void Awake()
        {
            owner = GetComponent<Unit>();
            damageGuard = GetComponent<IUnitDamageGuard>();
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

        // Context-free damage (tests/environment) retains its existing behavior.
        public int ApplyDamage(int amount, Vector2 sourcePosition, bool bypassDirectionalGuard = false)
        {
            if (amount < 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0 || !CanReceiveDamage) return 0;
            if (!bypassDirectionalGuard && damageGuard != null && damageGuard.BlocksDamage(sourcePosition)) return 0;
            return ApplyDamageCore(amount, (Vector2)transform.position - sourcePosition);
        }

        public int ApplyDamage(int amount)
            => ApplyDamageCore(amount, Vector2.zero);

        // Direction is presentation-only. Keep the caller's existing guard policy unchanged.
        public int ApplyDirectedDamage(int amount, Vector2 direction)
            => ApplyDamageCore(amount, direction);

        private int ApplyDamageCore(int amount, Vector2 direction)
        {
            if (amount < 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0 || !CanReceiveDamage) return 0;
            int applied = Mathf.Min(amount, CurrentHealth);
            if (!preserveHealthOnDamage) CurrentHealth -= applied;
            unchecked { DamageVersion++; }
            LastDamage = new UnitDamageResult(this, DamageVersion, applied, preserveHealthOnDamage ? 0 : applied,
                !IsAlive, direction);
            DamageApplied?.Invoke(LastDamage);
            if (!IsAlive && deactivateOnDeath) gameObject.SetActive(false);
            return applied;
        }
    }
}
