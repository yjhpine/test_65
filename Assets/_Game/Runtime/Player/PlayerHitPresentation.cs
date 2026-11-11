using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Player
{
    public readonly struct PlayerHitPresentation
    {
        public readonly uint AttackVersion;
        public readonly PlayerAttack Kind;
        public readonly UnitHealth Target;
        public readonly int TargetId, Damage, HealthLost;
        public readonly Vector2 Point, LocalPoint, Direction;
        public readonly bool Killed;
        public PlayerHitPresentation(uint version, PlayerAttack kind, UnitHealth target, Vector2 point,
            Vector2 direction, int damage, int healthLost, bool killed)
        {
            AttackVersion = version; Kind = kind; Target = target; TargetId = target.GetInstanceID();
            Point = point; LocalPoint = target.transform.InverseTransformPoint(point); Direction = direction;
            Damage = damage; HealthLost = healthLost; Killed = killed;
        }
    }

    // Independent of Strike/IsFinisher: changing presentation never changes the gameplay combo.
    public sealed class PlayerAttackAlternation
    {
        private bool hasPrevious;
        private PlayerAttack previousKind;
        private double previousStart;
        public int Variant { get; private set; }
        public int Begin(PlayerAttack kind, double startedAt)
        {
            bool basic = kind == PlayerAttack.Side || kind == PlayerAttack.Air;
            double interval = startedAt - previousStart;
            Variant = basic && hasPrevious && kind == previousKind && interval >= 0 && interval <= .4000001
                ? 1 - Variant : 0;
            hasPrevious = basic; previousKind = kind; previousStart = startedAt;
            return Variant;
        }
        public void Reset() { hasPrevious = false; Variant = 0; }
    }
}
