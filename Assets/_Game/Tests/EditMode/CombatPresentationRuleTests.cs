using ActionPlatformer.Player;
using NUnit.Framework;

namespace ActionPlatformer.Tests
{
    public sealed class CombatPresentationRuleTests
    {
        [Test] public void AlternationUsesAttackStartsInclusiveBoundaryAndKindNotStrike()
        {
            var rule = new PlayerAttackAlternation();
            Assert.That(rule.Begin(PlayerAttack.Side, 1), Is.Zero);
            Assert.That(rule.Begin(PlayerAttack.Side, 1.4), Is.EqualTo(1));
            Assert.That(rule.Begin(PlayerAttack.Side, 1.6), Is.Zero);
            Assert.That(rule.Begin(PlayerAttack.Side, 2.001), Is.Zero);
            Assert.That(rule.Begin(PlayerAttack.Air, 2.1), Is.Zero);
            Assert.That(rule.Begin(PlayerAttack.Air, 2.2), Is.EqualTo(1));
            rule.Begin(PlayerAttack.Lift, 2.3);
            Assert.That(rule.Begin(PlayerAttack.Air, 2.4), Is.Zero);
            rule.Reset();
            Assert.That(rule.Begin(PlayerAttack.Air, 2.5), Is.Zero);
        }
    }
}
