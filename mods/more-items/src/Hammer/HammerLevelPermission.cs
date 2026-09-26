using System;
using JumpKing;

namespace MoreItems
{
    internal static class HammerLevelPermission
    {
        internal const string HammerTag = "AllowHammer";

        internal static bool AllowsHammer()
        {
            var content = Game1.instance == null ? null : Game1.instance.contentManager;
            return AllowsHammer(content == null || content.level == null ? null : content.level.Info.Tags);
        }

        internal static bool AllowsHammer(string[] tags)
        {
            if (tags == null) return false;
            foreach (string tag in tags)
                if (string.Equals(tag, HammerTag, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
