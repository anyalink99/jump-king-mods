using System.IO;
using JumpKing;

namespace Replays
{
    internal static class ReplayPaths
    {
        internal static readonly string Root = Path.Combine(
            Path.GetDirectoryName(typeof(Game1).Assembly.Location),
            "Content",
            "Replays");

        internal static readonly string Files = Path.Combine(Root, "Files");
        internal static readonly string Settings = Path.Combine(
            Root,
            "Replays.Settings.xml");
    }
}
