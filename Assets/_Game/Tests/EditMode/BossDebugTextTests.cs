using ActionPlatformer.Editor;
using ActionPlatformer.Units.Bosses;
using NUnit.Framework;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class BossDebugTextTests
    {
        private sealed class FakeState : IBossActionState
        {
            public BossState State { get; set; } = BossState.Telegraph;
            public uint StateVersion => 1;
            public BossPatternDefinition Pattern { get; set; }
            public float StateElapsed => 0;
            public float Progress => 0;
            public float Facing => 1;
            public int Phase { get; set; }
            public BossTelegraph Telegraph => default;
            public string LastSelectionTrace { get; set; }
            public BossFsmDefinition Settings => null;
        }

        [Test] public void FormatShowsStatePatternAndPhase()
        {
            var pattern = ScriptableObject.CreateInstance<StubBossPattern>();
            try
            {
                pattern.name = "Slam";
                string text = BossDebugText.Format(new FakeState { Pattern = pattern, Phase = 2, State = BossState.Execute });
                StringAssert.Contains("Boss State: Execute", text);
                StringAssert.Contains("Boss Pattern: Slam", text);
                StringAssert.Contains("Boss Phase: 2", text);
            }
            finally { Object.DestroyImmediate(pattern); }
        }

        [Test] public void MissingPatternAndTraceHaveReadableFallbacks()
        {
            var state = new FakeState();
            StringAssert.Contains("Boss Pattern: None", BossDebugText.Format(state));
            Assert.AreEqual(BossDebugText.NoTrace, BossDebugText.Trace(state));
            state.LastSelectionTrace = "Selector -> 패턴: Melee";
            Assert.AreEqual("Selector -> 패턴: Melee", BossDebugText.Trace(state));
            Assert.AreEqual("", BossDebugText.Format(null));
            Assert.AreEqual(BossDebugText.NoTrace, BossDebugText.Trace(null));
        }
    }
}
