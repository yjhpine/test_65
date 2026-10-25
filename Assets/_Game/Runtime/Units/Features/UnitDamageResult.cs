using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    // A completed damage transaction, including training targets that accept hits without losing HP.
    public readonly struct UnitDamageResult
    {
        public readonly UnitHealth Target;
        public readonly uint Version;
        public readonly int AcceptedDamage, HealthLost;
        public readonly bool Killed, HasDirection;
        public readonly Vector2 Direction;
        public UnitDamageResult(UnitHealth target, uint version, int acceptedDamage, int healthLost,
            bool killed, Vector2 direction)
        {
            Target = target; Version = version; AcceptedDamage = acceptedDamage; HealthLost = healthLost;
            Killed = killed; HasDirection = direction.sqrMagnitude > .000001f;
            Direction = HasDirection ? direction.normalized : Vector2.zero;
        }
    }
}
