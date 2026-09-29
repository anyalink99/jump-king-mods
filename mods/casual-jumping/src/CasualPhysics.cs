using System;

namespace CasualJumping
{
    internal static class CasualPhysics
    {
        internal const int TakeoffFrameCount = 6;
        internal const int SustainedAscentFrames = 36;
        internal const float EarlyReleaseGravityMultiplier = 2.5f;
        internal const float CasualPlusHorizontalSpeedScale = 70f / 77f;

        private static readonly float[] TakeoffPeakFractions =
        {
            2f / 7f,
            3.25f / 7f,
            4.4f / 7f,
            5.4f / 7f,
            6.3f / 7f,
            1f,
        };

        internal static float BallisticRise(float upwardSpeed, float gravity)
        {
            ValidatePositive(upwardSpeed, "upwardSpeed");
            ValidatePositive(gravity, "gravity");
            float rise = 0f;
            float speed = upwardSpeed;
            while (speed > 0f)
            {
                rise += speed;
                speed -= gravity;
            }
            return rise;
        }

        internal static int PoweredFrameCount(float fullUpwardSpeed, float gravity)
        {
            ValidatePositive(fullUpwardSpeed, "fullUpwardSpeed");
            ValidatePositive(gravity, "gravity");
            int stockAscentFrames = 0;
            float speed = fullUpwardSpeed;
            while (speed > 0f)
            {
                stockAscentFrames++;
                speed -= gravity;
            }
            return 1 + stockAscentFrames;
        }

        internal static float TakeoffUpwardSpeed(
            int takeoffFrame,
            float fullUpwardSpeed,
            float gravity)
        {
            ValidatePositive(fullUpwardSpeed, "fullUpwardSpeed");
            ValidatePositive(gravity, "gravity");
            if (takeoffFrame < 1 || takeoffFrame > TakeoffFrameCount)
            {
                throw new ArgumentOutOfRangeException("takeoffFrame");
            }
            float fullRise = BallisticRise(fullUpwardSpeed, gravity);
            float peakSpeed = fullRise * 7f / 153f;
            return peakSpeed * TakeoffPeakFractions[takeoffFrame - 1];
        }

        internal static float HeldAscentGravity(
            float fullUpwardSpeed,
            float gravity)
        {
            float fullRise = BallisticRise(fullUpwardSpeed, gravity);
            float peakSpeed = TakeoffUpwardSpeed(
                TakeoffFrameCount,
                fullUpwardSpeed,
                gravity);
            float takeoffRise = 0f;
            for (int frame = 1; frame <= TakeoffFrameCount; frame++)
            {
                takeoffRise += TakeoffUpwardSpeed(
                    frame,
                    fullUpwardSpeed,
                    gravity);
            }
            float sustainedRise = fullRise - takeoffRise;
            float triangularFrames =
                SustainedAscentFrames * (SustainedAscentFrames + 1f) / 2f;
            return (SustainedAscentFrames * peakSpeed - sustainedRise)
                / triangularFrames;
        }

        internal static float ApplyHeldAscentGravity(
            float verticalVelocity,
            float normalGravity,
            float heldGravity)
        {
            ValidateFinite(verticalVelocity, "verticalVelocity");
            ValidatePositive(normalGravity, "normalGravity");
            ValidatePositive(heldGravity, "heldGravity");
            if (verticalVelocity >= 0f || heldGravity >= normalGravity)
            {
                return verticalVelocity;
            }
            return verticalVelocity - (normalGravity - heldGravity);
        }

        internal static float ApplyEarlyReleaseGravity(
            float verticalVelocity,
            float gravity)
        {
            ValidateFinite(verticalVelocity, "verticalVelocity");
            ValidatePositive(gravity, "gravity");
            return verticalVelocity < 0f
                ? verticalVelocity
                    + gravity * (EarlyReleaseGravityMultiplier - 1f)
                : verticalVelocity;
        }

        internal static float ApplyDirectionalControl(
            float velocity,
            int direction,
            float speedLimit,
            float acceleration)
        {
            ValidateFinite(velocity, "velocity");
            ValidatePositive(speedLimit, "speedLimit");
            ValidatePositive(acceleration, "acceleration");
            if (direction > 0 && velocity < speedLimit)
            {
                return Math.Min(velocity + acceleration, speedLimit);
            }
            if (direction < 0 && velocity > -speedLimit)
            {
                return Math.Max(velocity - acceleration, -speedLimit);
            }
            return velocity;
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
