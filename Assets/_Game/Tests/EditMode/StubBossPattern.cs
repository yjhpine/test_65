using ActionPlatformer.Units.Bosses;
using System;
using UnityEditor;

namespace ActionPlatformer.Tests
{
    public sealed class StubBossPattern : BossPatternDefinition
    {
        public bool allowStart = true;
        public void SetCooldown(float seconds)
        {
            var so = new SerializedObject(this);
            so.FindProperty("cooldown").floatValue = seconds;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public override bool CanStart(BossSelection s) => allowStart;
        public override BossPatternRun CreateRun(BossContext context) => throw new NotSupportedException("EditMode tests never run patterns.");
    }
}
