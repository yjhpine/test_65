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
        // Advances with DamageVersion only for hits that stop an attack in progress.
        public uint InterruptVersion { get; private set; }
        // Advances each time a directional guard (shield) blocks an attempt; blocked attempts deal no damage.
        public uint GuardBlockVersion { get; private set; }
        public UnitDamageResult LastDamage { get; private set; }
        public event System.Action<UnitDamageResult> DamageApplied;
        public bool IsAlive => initialized && CurrentHealth > 0;
        public Unit Owner => owner;
        public bool DamageEnabled { get; private set; } = true;
        public bool CanReceiveDamage => DamageEnabled && IsAlive && isActiveAndEnabled && owner != null && owner.isActiveAndEnabled;

        public void SetDamageEnabled(bool value) => DamageEnabled = value;

        // Ignores positioned and directed (enemy) damage, e.g. the player's protection after a hit. Context-free damage
        // still applies, as it already bypasses the directional guard. Targeting (CanReceiveDamage) is unchanged.
        public bool Invulnerable { get; private set; }
        public void SetInvulnerable(bool value) => Invulnerable = value;

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
        // An interrupting hit (launch/slam) also stops the target's attack in progress; see InterruptVersion.
        public int ApplyDamage(int amount, Vector2 sourcePosition, bool bypassDirectionalGuard = false, bool interruptsAttack = false)
        {
            if (amount < 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0 || !CanReceiveDamage || Invulnerable) return 0;
            if (!bypassDirectionalGuard && damageGuard != null && damageGuard.BlocksDamage(sourcePosition))
            {
                unchecked { GuardBlockVersion++; }
                return 0;
            }
            return ApplyDamageCore(amount, (Vector2)transform.position - sourcePosition, interruptsAttack);
        }

        public int ApplyDamage(int amount)
            => ApplyDamageCore(amount, Vector2.zero);

        // Direction is presentation-only. Keep the caller's existing guard policy unchanged.
        public int ApplyDirectedDamage(int amount, Vector2 direction, bool light = false)
        {
            if (amount < 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            return Invulnerable ? 0 : ApplyDamageCore(amount, direction, false, light);
        }

        private int ApplyDamageCore(int amount, Vector2 direction, bool interruptsAttack = false, bool light = false)
        {
            if (amount < 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0 || !CanReceiveDamage) return 0;
            int applied = Mathf.Min(amount, CurrentHealth);
            if (!preserveHealthOnDamage) CurrentHealth -= applied;
            unchecked { DamageVersion++; if (interruptsAttack) InterruptVersion++; }
            LastDamage = new UnitDamageResult(this, DamageVersion, applied, preserveHealthOnDamage ? 0 : applied,
                !IsAlive, direction, light);
            DamageApplied?.Invoke(LastDamage);
            if (!IsAlive && deactivateOnDeath) gameObject.SetActive(false);
            return applied;
        }
    }
}
