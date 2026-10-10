namespace CasualJumping
{
    internal sealed class LandingJumpBuffer
    {
        private bool buffered;

        internal void Update(
            bool airborne,
            bool jumpHeld,
            bool jumpPressed)
        {
            if (!jumpHeld)
            {
                Reset();
                return;
            }
            if (airborne && jumpPressed)
            {
                buffered = true;
            }
        }

        internal bool TryConsumeOnGround(bool isOnGround, bool jumpHeld)
        {
            if (!isOnGround || !jumpHeld || !buffered)
            {
                return false;
            }
            Reset();
            return true;
        }

        internal void Reset()
        {
            buffered = false;
        }
    }
}
