using System;
using System.IO;

namespace MultiplayerExpansion
{
    internal sealed class SessionMapIdentity
    {
        private readonly string root;
        internal SessionMapIdentity(string initialRoot) { root = Normalize(initialRoot); }
        private static string Normalize(string value) { return Path.GetFullPath(value).TrimEnd('\\', '/'); }
        internal ulong? Resolve(string currentRoot, ulong? savedLevel)
        {
            // Debug can retain a previous Workshop ID in its save snapshot
            return string.Equals(root, Normalize(currentRoot), StringComparison.OrdinalIgnoreCase) ? (ulong?)0 : savedLevel;
        }
    }
}
