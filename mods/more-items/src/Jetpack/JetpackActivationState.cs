namespace JumpKingJetpack
{
    internal sealed class JetpackActivationState
    {
        internal bool Active { get; private set; }
        internal bool BlockedUntilLanding { get; private set; }
        internal bool SuppressLandingJump { get; private set; }
        internal int HeldFrames { get; private set; }

        internal bool Update(
            bool enabled,
            bool airborne,
            bool supported,
            bool jumpHeld,
            bool jumpPressed,
            bool launchedThisFrame)
        {
            if (!enabled)
            {
                Reset();
                return false;
            }

            if (!jumpHeld)
            {
                SuppressLandingJump = false;
            }

            if (!airborne || supported)
            {
                Active = false;
                BlockedUntilLanding = false;
                HeldFrames = 0;
                return false;
            }

            if (BlockedUntilLanding)
            {
                Active = false;
                HeldFrames = 0;
                return false;
            }

            if (jumpPressed && !launchedThisFrame)
            {
                Active = true;
                SuppressLandingJump = true;
                HeldFrames = 0;
            }
            if (!Active || !jumpHeld)
            {
                Active = false;
                HeldFrames = 0;
                return false;
            }

            HeldFrames++;
            return true;
        }

        internal void Stop(bool blockUntilLanding)
        {
            Active = false;
            BlockedUntilLanding = blockUntilLanding;
            HeldFrames = 0;
        }

        internal void Reset()
        {
            Active = false;
            BlockedUntilLanding = false;
            SuppressLandingJump = false;
            HeldFrames = 0;
        }
    }
}
