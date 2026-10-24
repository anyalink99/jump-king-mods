namespace CasualJumping
{
    internal sealed class CoyoteJumpWindow
    {
        internal const int DurationFrames = 6;

        private bool wasSupported;
        private int remainingFrames;

        internal bool Available { get { return remainingFrames > 0; } }

        internal void Update(
            bool enabled,
            bool supported,
            bool assistedJumpActive)
        {
            if (!enabled)
            {
                Reset();
                return;
            }
            if (supported)
            {
                wasSupported = true;
                remainingFrames = 0;
                return;
            }
            if (wasSupported)
            {
                remainingFrames = assistedJumpActive ? 0 : DurationFrames;
            }
            else if (remainingFrames > 0)
            {
                remainingFrames--;
            }
            wasSupported = false;
        }

        internal bool TryConsume(bool jumpPressed)
        {
            if (!jumpPressed || remainingFrames <= 0) return false;
            remainingFrames = 0;
            return true;
        }

        internal void Reset()
        {
            wasSupported = false;
            remainingFrames = 0;
        }
    }
}
