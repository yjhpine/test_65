#if UNITY_EDITOR
using ActionPlatformer.Units.Bosses;
using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    // Test pattern with fixed phase lengths and public call counters, so tests can drive and observe a boss run.
    public sealed class ScriptedBossPattern : BossPatternDefinition
    {
        public float telegraphTime = .3f;
        public float executeTime = .3f;
        public bool blockAll;
        public readonly List<ScriptedRun> Runs = new List<ScriptedRun>();

        public override BossPatternRun CreateRun(BossContext context)
        {
            var run = new ScriptedRun(this, context);
            Runs.Add(run);
            return run;
        }

        public sealed class ScriptedRun : BossPatternRun
        {
            private readonly ScriptedBossPattern owner;
            private float telegraphElapsed, executeElapsed;
            private bool telegraphing;
            public int BeginTelegraphCalls, BeginExecuteCalls, EndCalls, CancelCalls, DiedCalls, StoppedCalls;

            public ScriptedRun(ScriptedBossPattern owner, BossContext context) : base(context) { this.owner = owner; }

            public override void BeginTelegraph() { BeginTelegraphCalls++; telegraphElapsed = 0; telegraphing = true; }
            public override bool TickTelegraph(float dt) { telegraphElapsed += dt; return telegraphElapsed >= owner.telegraphTime; }
            public override void BeginExecute() { BeginExecuteCalls++; executeElapsed = 0; telegraphing = false; }
            public override bool TickExecute(float dt) { executeElapsed += dt; return executeElapsed >= owner.executeTime; }
            public override void End() { EndCalls++; telegraphing = false; }
            public override void Cancel() { CancelCalls++; telegraphing = false; }
            public override void OnBossDied() { DiedCalls++; }
            public override void OnBossStopped() { StoppedCalls++; }
            public override float Progress => telegraphing ? Mathf.Clamp01(telegraphElapsed / Mathf.Max(.0001f, owner.telegraphTime)) :
                Mathf.Clamp01(executeElapsed / Mathf.Max(.0001f, owner.executeTime));
            public override bool BlocksDamage(Vector2 source) => owner.blockAll;

            public override BossTelegraph Telegraph => telegraphing
                ? new BossTelegraph { Visible = true, Center = Context.Combat.Center + Vector2.right * Context.Facing * 1.5f, Size = Vector2.one }
                : default;
        }
    }
}
#endif
