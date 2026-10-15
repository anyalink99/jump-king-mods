using System;

namespace JumpKingJetpack
{
    internal static class JetpackPhysics
    {
        internal const int ThrustRampFrames = 10;
        internal const float FullThrustGravityMultiplier = 3f;
        internal const float MaximumUpwardSpeedScale = 0.7f;

        internal static float ApplyThrust(
            float verticalVelocity,
            float gravity,
            int heldFrames,
            float fullJumpSpeed)
        {
            ValidateFinite(verticalVelocity, "verticalVelocity");
            ValidatePositive(gravity, "gravity");
            ValidatePositive(fullJumpSpeed, "fullJumpSpeed");
            if (heldFrames < 1)
            {
                throw new ArgumentOutOfRangeException("heldFrames");
            }

            float maximumUpwardSpeed =
                fullJumpSpeed * MaximumUpwardSpeedScale;
            if (verticalVelocity <= -maximumUpwardSpeed)
            {
                return verticalVelocity;
            }

            float nextVelocity = verticalVelocity - Thrust(gravity, heldFrames);
            return Math.Max(nextVelocity, -maximumUpwardSpeed);
        }

        internal static float Thrust(float gravity, int heldFrames)
        {
            ValidatePositive(gravity, "gravity");
            if (heldFrames < 1)
            {
                throw new ArgumentOutOfRangeException("heldFrames");
            }

            float progress = Math.Min(heldFrames, ThrustRampFrames)
                / (float)ThrustRampFrames;
            float eased = progress * progress * (3f - 2f * progress);
            return gravity * FullThrustGravityMultiplier * eased;
        }

        private static void ValidatePositive(float value, string name)
        {
            if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static void ValidateFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
