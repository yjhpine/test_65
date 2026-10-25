using ActionPlatformer.Flow;
using NUnit.Framework;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class StageTransferRouteTests
    {
        [TestCase(1f, .4f)] [TestCase(36f, .6f)] [TestCase(90f, 1.1f)]
        public void DurationUsesWholeRouteAndBounds(float length, float duration)
        {
            var route = new StageTransferRoute(new[] { Vector2.zero, Vector2.right * length });
            Assert.That(route.Duration, Is.EqualTo(duration).Within(.0001f));
        }
        [Test] public void DistanceContinuesAcrossWaypointsAndOnlyTailSlows()
        {
            var route = new StageTransferRoute(new[] { Vector2.zero, new Vector2(10, 0), new Vector2(10, 10), new Vector2(20, 10) });
            float previous = -1;
            for (int i = 0; i <= 100; i++)
            {
                float progress = StageTransferRoute.Progress(i / 100f);
                Assert.That(progress, Is.GreaterThanOrEqualTo(previous)); previous = progress;
            }
            Assert.That(StageTransferRoute.Progress(.4f) - StageTransferRoute.Progress(.3f),
                Is.EqualTo(StageTransferRoute.Progress(.7f) - StageTransferRoute.Progress(.6f)).Within(.0001f));
            Assert.That(StageTransferRoute.Progress(1f) - StageTransferRoute.Progress(.9f), Is.LessThan(.1f));
            Assert.That(route.Evaluate(route.Duration, out var passed), Is.EqualTo(new Vector2(20, 10)));
            Assert.That(passed, Is.EqualTo(3));
        }
        [Test] public void DuplicatePointsStillFinishWithoutNaN()
        {
            var route = new StageTransferRoute(new[] { Vector2.one, Vector2.one, Vector2.one });
            Assert.That(route.Evaluate(.4f, out var passed), Is.EqualTo(Vector2.one));
            Assert.That(passed, Is.EqualTo(2));
        }
    }
}
