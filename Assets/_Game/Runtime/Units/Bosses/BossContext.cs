using ActionPlatformer.Units.Features;

namespace ActionPlatformer.Units.Bosses
{
    // Per-instance wiring handed to pattern runs; holds no authored data of its own.
    public sealed class BossContext
    {
        public BossContext(Unit owner, UnitHealth health, BossCombat2D combat, IBossMover mover, BossBlackboard board, BossFsmDefinition settings)
        {
            Owner = owner; Health = health; Combat = combat; Mover = mover; Board = board; Settings = settings;
            Facing = 1;
        }

        public Unit Owner { get; }
        public UnitHealth Health { get; }
        public BossCombat2D Combat { get; }
        public IBossMover Mover { get; }
        public BossBlackboard Board { get; }
        public BossFsmDefinition Settings { get; }
        public float Facing { get; set; }
        // Set by the brain; lets a definition's CanStart read its own per-boss run (for example a count of live minions).
        internal System.Func<BossPatternDefinition, BossPatternRun> RunProvider { get; set; }
        public BossPatternRun RunOf(BossPatternDefinition pattern) => RunProvider?.Invoke(pattern);
    }
}
