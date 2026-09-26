using System;

namespace CasualJumping
{
    internal static class LevelPermission
    {
        internal const string CasualTag = "AllowCasualJumping";

        internal static bool AllowsCasual(string[] tags)
        {
            return Contains(tags, CasualTag);
        }

        private static bool Contains(string[] tags, string requiredTag)
        {
            if (tags == null)
            {
                return false;
            }
            foreach (string tag in tags)
            {
                if (string.Equals(
                    tag,
                    requiredTag,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
