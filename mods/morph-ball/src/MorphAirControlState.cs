using System;

namespace MorphBallMod
{
    internal sealed class MorphAirControlState
    {
        private bool initialized;
        private float controlledVelocity;

        internal float Apply(
            float totalVelocity,
            int direction,
            float speedLimit,
            float acceleration,
            float neutralDeceleration)
        {
            if (!initialized)
            {
                controlledVelocity = totalVelocity;
                initialized = true;
            }

            float externalVelocity = totalVelocity - controlledVelocity;
            float target = direction * speedLimit;
            float step = direction == 0
                ? neutralDeceleration
                : acceleration;
            controlledVelocity = MoveTowards(
                controlledVelocity,
                target,
                step);
            return controlledVelocity + externalVelocity;
        }

        internal void Reset()
        {
            initialized = false;
            controlledVelocity = 0f;
        }

        private static float MoveTowards(
            float current,
            float target,
            float maximumDelta)
        {
            if (Math.Abs(target - current) <= maximumDelta)
            {
                return target;
            }
            return current + Math.Sign(target - current) * maximumDelta;
        }
    }
}
