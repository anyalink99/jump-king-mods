using System;

namespace JumpKingJetpack
{
    internal static class LevelPermission
    {
        internal const string JetpackTag = "AllowJetpack";

        internal static bool AllowsJetpack(string[] tags)
        {
            if (tags == null)
            {
                return false;
            }
            foreach (string tag in tags)
            {
                if (string.Equals(
                    tag,
                    JetpackTag,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
