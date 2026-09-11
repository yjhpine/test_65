using ActionPlatformer.Player;
using NUnit.Framework;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class MovementRulesTests
    {
        [Test] public void BufferedPressSurvivesUntilPhysicsAndIsConsumedOnce()
        {
            var buffer = new InputBuffer(); buffer.Push(1.0);
            Assert.That(buffer.TryConsume(1.04, 0.13), Is.True);
            Assert.That(buffer.TryConsume(1.06, 0.13), Is.False);
        }
        [Test] public void ExpiredPressDoesNotFireOnALaterLanding()
        {
            var buffer = new InputBuffer(); buffer.Push(1.0);
            Assert.That(buffer.TryConsume(1.2, 0.13), Is.False);
        }
        [Test] public void ClearingInputPreventsGhostPressAfterDisable()
        {
            var buffer = new InputBuffer(); buffer.Push(1.0); buffer.Clear();
            Assert.That(buffer.IsPending(1.01, 0.13), Is.False);
        }
        [Test] public void NewPressReplacesAnExpiredPress()
        {
            var buffer = new InputBuffer(); buffer.Push(1.0); buffer.Push(2.0);
            Assert.That(buffer.TryConsume(2.02, 0.13), Is.True);
        }
        [Test] public void JumpSpeedMatchesConfiguredHeight()
        {
            float velocity = MovementMath.JumpSpeed(3.2f, 32f);
            Assert.That(velocity * velocity / (2f * 32f), Is.EqualTo(3.2f).Within(0.0001f));
        }
        [Test] public void GroundBrakingStopsFasterThanAirCoasting()
        {
            var tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            try
            {
                float airborne = MovementMath.HorizontalSpeed(10f, 0f, false, tuning, 0.02f);
                float grounded = MovementMath.HorizontalSpeed(10f, 0f, true, tuning, 0.02f);
                Assert.That(airborne, Is.GreaterThan(9.8f));
                Assert.That(grounded, Is.LessThan(8f));
            }
            finally { Object.DestroyImmediate(tuning); }
        }
    }
}
