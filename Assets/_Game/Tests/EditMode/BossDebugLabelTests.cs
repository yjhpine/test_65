using ActionPlatformer.Units.Bosses;
using NUnit.Framework;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class BossDebugLabelTests
    {
        private sealed class FakeState : IBossActionState
        {
            public BossState State { get; set; }
            public uint StateVersion => 1;
            public BossPatternDefinition Pattern { get; set; }
            public float StateElapsed => 0;
            public float Progress { get; set; }
            public float Facing => 1;
            public int Phase { get; set; }
            public BossTelegraph Telegraph => default;
            public string LastSelectionTrace => null;
            public BossFsmDefinition Settings => null;
        }

        private StubBossPattern pattern;
        [SetUp] public void Create() { pattern = ScriptableObject.CreateInstance<StubBossPattern>(); pattern.name = "Slam"; }
        [TearDown] public void Destroy() => Object.DestroyImmediate(pattern);

        [TestCase(BossState.Telegraph, "Telegraph · Slam 45%")]
        [TestCase(BossState.Execute, "Execute · Slam 45%")]
        [TestCase(BossState.Recover, "Recover · Slam 45%")]
        public void PatternStatesNameThePatternAndItsProgress(BossState state, string firstLine)
        {
            string text = BossDebugLabel.Describe(new FakeState { State = state, Pattern = pattern, Progress = .449f, Phase = 1 }, 150, 300);
            Assert.AreEqual(firstLine + "\nP1 · HP 150/300", text);
        }

        [TestCase(BossState.Dormant, "Dormant")]
        [TestCase(BossState.Approach, "Approach")]
        [TestCase(BossState.Staggered, "Staggered 30%")]
        [TestCase(BossState.PhaseShift, "PhaseShift → P2 30%")]
        [TestCase(BossState.Death, "Death 30%")]
        public void OtherStatesShowTheStateWithoutAPattern(BossState state, string firstLine)
        {
            string text = BossDebugLabel.Describe(new FakeState { State = state, Pattern = pattern, Progress = .3f, Phase = 2 }, 0, 300);
            Assert.AreEqual(firstLine + "\nP2 · HP 0/300", text);
        }

        [Test] public void UnboundBossAndMissingPatternStayReadable()
        {
            Assert.AreEqual("(no brain)", BossDebugLabel.Describe(null, 300, 300));
            StringAssert.StartsWith("Execute · ? 0%", BossDebugLabel.Describe(new FakeState { State = BossState.Execute }, 300, 300));
        }

        [Test] public void AttackStagesHaveDistinctColors()
        {
            var telegraph = BossDebugLabel.ColorFor(BossState.Telegraph);
            Assert.AreNotEqual(telegraph, BossDebugLabel.ColorFor(BossState.Execute));
            Assert.AreNotEqual(telegraph, BossDebugLabel.ColorFor(BossState.Recover));
            Assert.AreNotEqual(BossDebugLabel.ColorFor(BossState.Execute), BossDebugLabel.ColorFor(BossState.Recover));
        }
    }
}
