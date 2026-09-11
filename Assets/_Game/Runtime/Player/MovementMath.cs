using UnityEngine;

namespace ActionPlatformer.Player
{
    public static class MovementMath
    {
        public static float JumpSpeed(float height, float gravity)
        {
            return Mathf.Sqrt(2f * Mathf.Max(0f, height) * Mathf.Max(0f, gravity));
        }

        public static float HorizontalSpeed(float current, float input, bool grounded,
            PlayerTuning tuning, float deltaTime)
        {
            float target = Mathf.Clamp(input, -1f, 1f) * tuning.RunSpeed;
            float acceleration = grounded
                ? (Mathf.Abs(input) < 0.01f ? tuning.GroundBraking : tuning.GroundAcceleration)
                : tuning.AirAcceleration;

            // Preserve gentle coasting when directional input is released in the air.
            if (!grounded && Mathf.Abs(input) < 0.01f)
                acceleration = 3f;

            return Mathf.MoveTowards(current, target, acceleration * deltaTime);
        }
    }

    /// <summary>A press survives render/physics timing differences and is consumed at most once.</summary>
    public sealed class InputBuffer
    {
        private double pressedAt = double.NegativeInfinity;
        public void Push(double now) => pressedAt = now;
        public void Clear() => pressedAt = double.NegativeInfinity;
        public bool IsPending(double now, double window) => now >= pressedAt && now - pressedAt <= window;
        public bool TryConsume(double now, double window)
        {
            bool pending = IsPending(now, window);
            Clear();
            return pending;
        }
    }
}
