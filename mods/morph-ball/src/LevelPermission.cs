using System;

namespace MorphBallMod
{
    internal static class LevelPermission
    {
        internal const string BallKingTag = "AllowBallKing";

        internal static bool AllowsBallKing(string[] tags)
        {
            if (tags == null)
            {
                return false;
            }
            foreach (string tag in tags)
            {
                if (string.Equals(
                        tag,
                        BallKingTag,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
