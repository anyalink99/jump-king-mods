namespace MorphBallMod
{
    internal sealed class MorphWallBounceState
    {
        private int wallDirection;
        private bool bounceSeen;
        private bool sliding;

        internal bool Sliding { get { return sliding; } }
        internal int WallDirection { get { return wallDirection; } }

        internal void BeginFrame(
            int heldDirection,
            bool grounded,
            bool stickyMode)
        {
            if (stickyMode
                || heldDirection == 0
                || (wallDirection != 0
                    && heldDirection != wallDirection))
            {
                Reset();
                return;
            }
            if (grounded && !bounceSeen)
            {
                Reset();
            }
        }

        internal bool ObserveCollision(
            int travelDirection,
            int heldDirection)
        {
            if (travelDirection == 0 || heldDirection != travelDirection)
            {
                Reset();
                return false;
            }
            if (bounceSeen && wallDirection == travelDirection)
            {
                sliding = true;
                return true;
            }
            wallDirection = travelDirection;
            bounceSeen = true;
            sliding = false;
            return false;
        }

        internal void ReleaseIfDetached(bool adjacent)
        {
            if (sliding && !adjacent)
            {
                Reset();
            }
        }

        internal void Reset()
        {
            wallDirection = 0;
            bounceSeen = false;
            sliding = false;
        }
    }
}
