namespace CasualJumping
{
    internal static class JumpSupport
    {
        internal static bool IsSupported(
            bool isOnGround,
            bool isOnSand,
            bool isTouchingSand)
        {
            return isOnGround || isOnSand || isTouchingSand;
        }
    }
}
