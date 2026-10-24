namespace CasualJumping
{
    internal sealed class SurfaceControlState
    {
        private bool slopeControlLocked;
        private int slopeBlockedDirection;
        private int slopeMissedFrames;
        private int wallDirection;
        private bool wallBounceSeen;
        private bool wallSliding;

        internal bool SlopeControlLocked
        {
            get { return slopeControlLocked; }
        }

        internal bool WallSliding
        {
            get { return wallSliding; }
        }

        internal int WallDirection
        {
            get { return wallDirection; }
        }

        internal void BeginFrame(
            int heldDirection,
            bool startsOnSlope,
            int blockedSlopeDirection,
            bool isOnGround)
        {
            if (startsOnSlope)
            {
                ObserveSlopeContact(blockedSlopeDirection);
            }
            if (isOnGround && !startsOnSlope)
            {
                ClearSlopeContact();
            }
            if (isOnGround || heldDirection == 0
                || (wallDirection != 0 && heldDirection != wallDirection))
            {
                ClearWallContact();
            }
        }

        internal void ObserveSlopeContact(int blockedDirection)
        {
            slopeControlLocked = true;
            slopeMissedFrames = 0;
            if (blockedDirection != 0)
            {
                slopeBlockedDirection = blockedDirection;
            }
            ClearWallContact();
        }

        internal bool AllowsAirControl(int direction)
        {
            if (wallSliding)
            {
                return false;
            }
            if (!slopeControlLocked)
            {
                return true;
            }
            return direction != 0
                && slopeBlockedDirection != 0
                && direction != slopeBlockedDirection;
        }

        internal bool ObserveWallCollision(
            int travelDirection,
            int heldDirection)
        {
            if (travelDirection == 0 || heldDirection != travelDirection)
            {
                ClearWallContact();
                return false;
            }
            if (wallBounceSeen && wallDirection == travelDirection)
            {
                wallSliding = true;
                return true;
            }
            wallDirection = travelDirection;
            wallBounceSeen = true;
            wallSliding = false;
            return false;
        }

        internal void ReleaseSlopeIfDetached(bool slopeStillAdjacent)
        {
            if (!slopeControlLocked)
            {
                return;
            }
            if (slopeStillAdjacent)
            {
                slopeMissedFrames = 0;
                return;
            }

            slopeMissedFrames++;
            if (slopeMissedFrames >= 2)
            {
                ClearSlopeContact();
            }
        }

        internal void ReleaseWallIfDetached(bool wallStillAdjacent)
        {
            if (wallSliding && !wallStillAdjacent)
            {
                ClearWallContact();
            }
        }

        internal void Reset()
        {
            ClearSlopeContact();
            ClearWallContact();
        }

        private void ClearSlopeContact()
        {
            slopeControlLocked = false;
            slopeBlockedDirection = 0;
            slopeMissedFrames = 0;
        }

        private void ClearWallContact()
        {
            wallDirection = 0;
            wallBounceSeen = false;
            wallSliding = false;
        }
    }
}
