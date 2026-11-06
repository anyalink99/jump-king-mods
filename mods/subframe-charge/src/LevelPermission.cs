using JKRuntime.Input;
using System;

namespace SubframeCharge
{
    internal static class LevelPermission
    {
        internal const string SubframeChargeTag =
            "AllowSubframeCharge";

        internal static bool AllowsSubframeCharge(string[] tags)
        {
            if (tags == null)
            {
                return false;
            }
            foreach (string tag in tags)
            {
                if (string.Equals(
                        tag,
                        SubframeChargeTag,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
