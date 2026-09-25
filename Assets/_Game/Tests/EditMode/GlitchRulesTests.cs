using ActionPlatformer.Player;
using NUnit.Framework;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class GlitchRulesTests
    {
        [TestCase(-1f, 0f, GlitchDirection.Left)]
        [TestCase(1f, 0f, GlitchDirection.Right)]
        [TestCase(0f, 1f, GlitchDirection.Up)]
        [TestCase(0f, -1f, GlitchDirection.Down)]
        [TestCase(1f, 1f, GlitchDirection.Right)]
        [TestCase(-1f, -1f, GlitchDirection.Left)]
        public void DirectionUsesTargetCenterAndHorizontalDiagonalTie(float x, float y, GlitchDirection expected)
        {
            var center = new Vector2(8f, 4f);
            Assert.That(GlitchUtility.Direction(center, center + new Vector2(x, y), Vector2.zero, 0.1f), Is.EqualTo(expected));
        }

        [Test] public void CenterUsesPlayersSide()
        {
            Assert.That(GlitchUtility.Direction(Vector2.zero, Vector2.zero, Vector2.left, 0.1f), Is.EqualTo(GlitchDirection.Left));
            Assert.That(GlitchUtility.Direction(Vector2.zero, Vector2.zero, Vector2.right, 0.1f), Is.EqualTo(GlitchDirection.Right));
        }

        [Test] public void PlacementRespectsColliderOffsetDifferentSizesAndFeet()
        {
            var player = new Bounds(new Vector3(1f, 2f), new Vector3(0.6f, 1.6f));
            var target = new Bounds(new Vector3(5f, 0.5f), Vector3.one);
            Vector2 root = new Vector2(0f, 1f);
            Vector2 right = GlitchUtility.Placement(player, root, target, GlitchDirection.Right, 0.15f);
            Vector2 center = right + (Vector2)player.center - root;
            Assert.That(center.x - player.extents.x - target.max.x, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(center.y - player.extents.y, Is.EqualTo(target.min.y + 0.02f).Within(0.001f));
            Vector2 up = GlitchUtility.Placement(player, root, target, GlitchDirection.Up, 0.15f);
            Assert.That(up.y + player.center.y - root.y - player.extents.y - target.max.y, Is.EqualTo(0.15f).Within(0.001f));
        }
    }
}
