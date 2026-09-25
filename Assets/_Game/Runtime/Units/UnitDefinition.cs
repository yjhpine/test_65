using ActionPlatformer.Units.Fsm;
using UnityEngine;

namespace ActionPlatformer.Units
{
    [CreateAssetMenu(menuName = "Action Platformer/Units/Unit Definition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        [SerializeField] private string unitId;
        [SerializeField] private string displayName;
        [SerializeField] private UnitKind kind;
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0)] private int attackPower = 10;
        [SerializeField] private FsmDefinition fsm;
        [SerializeField] private bool allowGlitchTarget;
        [SerializeField] private bool allowForcedMovement = true;

        public string UnitId => unitId;
        public string DisplayName => displayName;
        public UnitKind Kind => kind;
        public int MaxHealth => maxHealth;
        public int AttackPower => attackPower;
        public FsmDefinition Fsm => fsm;
        public bool AllowGlitchTarget => allowGlitchTarget;
        public bool AllowForcedMovement => allowForcedMovement;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                error = "Unit ID is required.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = "Unit display name is required.";
                return false;
            }
            if (kind == UnitKind.Unspecified || !System.Enum.IsDefined(typeof(UnitKind), kind))
            {
                error = "Select a supported unit kind.";
                return false;
            }
            if (maxHealth < 1)
            {
                error = "Unit maximum health must be at least 1.";
                return false;
            }
            if (attackPower < 0)
            {
                error = "Unit attack power cannot be negative.";
                return false;
            }
            error = null;
            return true;
        }
    }
}
