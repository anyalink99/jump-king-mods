namespace CasualJumping
{
    internal sealed class AirControlState
    {
        private bool initialized;
        private float controlledVelocity;

        internal float Apply(
            float totalVelocity,
            int direction,
            float speedLimit,
            float acceleration)
        {
            if (!initialized)
            {
                controlledVelocity = totalVelocity;
                initialized = true;
            }

            float externalVelocity = totalVelocity - controlledVelocity;
            controlledVelocity = CasualPhysics.ApplyDirectionalControl(
                controlledVelocity,
                direction,
                speedLimit,
                acceleration);
            return controlledVelocity + externalVelocity;
        }

        internal void Reset()
        {
            initialized = false;
            controlledVelocity = 0f;
        }
    }
}
