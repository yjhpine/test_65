using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    // Shared, read-only configuration. Per-fight state lives in the BossPatternRun it creates.
    public abstract class BossPatternDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [Min(0)] [SerializeField] private float cooldown;
        [Min(0)] [SerializeField] private float recovery = 0.5f;
        [SerializeField] private bool interruptibleDuringTelegraph = true;
        [SerializeField] private bool interruptibleDuringExecute;
        [SerializeField] private string telegraphAnimation;
        [SerializeField] private string executeAnimation;
        [SerializeField] private string recoverAnimation;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public float Cooldown => cooldown;
        public float Recovery => recovery;
        public bool InterruptibleDuringTelegraph => interruptibleDuringTelegraph;
        public bool InterruptibleDuringExecute => interruptibleDuringExecute;
        public string TelegraphAnimation => telegraphAnimation;
        public string ExecuteAnimation => executeAnimation;
        public string RecoverAnimation => recoverAnimation;

        public virtual bool CanStart(BossSelection s) => true;
        public abstract BossPatternRun CreateRun(BossContext context);

        public virtual bool TryValidate(out string error)
        {
            if (cooldown < 0 || float.IsNaN(cooldown)) { error = $"{DisplayName}: cooldown은 0 이상이어야 합니다."; return false; }
            if (recovery < 0 || float.IsNaN(recovery)) { error = $"{DisplayName}: recovery는 0 이상이어야 합니다."; return false; }
            error = null;
            return true;
        }
    }

    // Per-boss state of one pattern. The brain creates it once per boss and pattern and reuses it for every execution,
    // so reset per-execution state in BeginTelegraph/BeginExecute, never only in the constructor. Tick* return true once that phase has finished.
    public abstract class BossPatternRun
    {
        protected BossPatternRun(BossContext context) { Context = context; }

        protected BossContext Context { get; }

        public abstract void BeginTelegraph();
        public abstract bool TickTelegraph(float dt);
        public abstract void BeginExecute();
        public abstract bool TickExecute(float dt);
        public virtual void End() { }
        public virtual void Cancel() { }
        // Called once for every cached run (active or not) when the boss enters Death.
        public virtual void OnBossDied() { }
        // Called for every cached run when the fight stops (disable / FSM stop), after the active run was cancelled.
        public virtual void OnBossStopped() { }
        public virtual float Progress => 0;
        public virtual BossTelegraph Telegraph => default;
        public virtual bool BlocksDamage(Vector2 source) => false;
    }
}
