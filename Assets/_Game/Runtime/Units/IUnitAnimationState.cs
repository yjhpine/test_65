namespace ActionPlatformer.Units
{
    public enum UnitAnimation { Locomotion, Attack, Hit, Death }

    // Optional presentation contract. Gameplay states never reference an Animator or clip.
    public interface IUnitAnimationState
    {
        UnitAnimation Animation { get; }
        uint AnimationVersion { get; }
        float AnimationElapsed { get; }
        // Attack: time until impact. Hit/Death: time until the state finishes.
        float AnimationDuration { get; }
        float FacingDirection { get; }
    }
}
