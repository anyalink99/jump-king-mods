namespace MorphBallMod
{
    internal static class BallFormState
    {
        public const int ApiVersion = 1;

        public static bool Enabled { get; internal set; }
        public static bool Morphed { get; internal set; }
        public static bool Attached { get; internal set; }
        public static int JumpSequence { get; internal set; }

        internal static void NotifyJump()
        {
            unchecked
            {
                JumpSequence++;
            }
        }

        internal static void Reset()
        {
            Enabled = false;
            Morphed = false;
            Attached = false;
        }
    }
}
