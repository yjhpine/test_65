namespace ActionPlatformer.Units.Bosses
{
    // Read-only view of the boss brain for the visual layer and editor tooling.
    public interface IBossActionState
    {
        BossState State { get; }
        // Increases on every state entry, so a visual can restart an animation that re-enters the same state.
        uint StateVersion { get; }
        BossPatternDefinition Pattern { get; }
        float StateElapsed { get; }
        float Progress { get; }
        float Facing { get; }
        int Phase { get; }
        BossTelegraph Telegraph { get; }
        string LastSelectionTrace { get; }
        BossFsmDefinition Settings { get; }
    }
}
