using UnityEngine;
namespace ActionPlatformer.Units.Features
{
    public interface IUnitDamageGuard { bool BlocksDamage(Vector2 sourcePosition); }
    public interface IUnitRepositionState { uint RepositionVersion { get; } }
    // The camera a unit sees the stage through; enemies that need to be on screen to recognize it read this.
    public interface IUnitViewSource { Camera View { get; } }
}
