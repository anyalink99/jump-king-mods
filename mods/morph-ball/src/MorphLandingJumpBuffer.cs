namespace MorphBallMod
{
    internal sealed class MorphLandingJumpBuffer
    {
        private bool buffered;

        internal bool Buffered { get { return buffered; } }

        internal void Update(
            bool enabled,
            bool airborne,
            bool jumpHeld,
            bool jumpPressed)
        {
            if (!jumpHeld)
            {
                Reset();
                return;
            }
            if (enabled && airborne && jumpPressed)
            {
                buffered = true;
            }
        }

        internal bool TryConsume(bool attached, bool jumpHeld)
        {
            if (!attached || !jumpHeld || !buffered)
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
