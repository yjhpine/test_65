using UnityEngine;
namespace ActionPlatformer.Units.Features
{
    public interface IUnitDamageGuard { bool BlocksDamage(Vector2 sourcePosition); }
    public interface IUnitRepositionState { uint RepositionVersion { get; } }
}
