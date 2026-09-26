using System;
using JumpKing;

namespace MoreItems
{
    internal static class RewinderLevelPermission
    {
        internal const string RewinderTag = "AllowRewinder";
        internal static bool AllowsRewinder()
        {
            string[] tags = Game1.instance.contentManager.level == null ? null : Game1.instance.contentManager.level.Info.Tags;
            if (tags == null) return false;
            foreach (string tag in tags) if (string.Equals(tag, RewinderTag, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
