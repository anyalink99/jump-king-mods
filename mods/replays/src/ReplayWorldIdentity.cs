using System;
using System.IO;
using JumpKing;
using JumpKing.Level;
using JumpKing.Workshop;

namespace Replays
{
    internal static class ReplayWorldIdentity
    {
        internal static ReplayWorld Current()
        {
            Level level = Game1.instance.contentManager.level;
            string name = level == null || string.IsNullOrWhiteSpace(level.Name)
                ? "Jump King"
                : level.Name.Trim();
            string author = level == null || string.IsNullOrWhiteSpace(level.Author)
                ? "Nexile"
                : level.Author.Trim();
            ulong workshopId = level == null ? 0UL : level.ID;
            int screens = LevelManager.TotalScreens;
            string key = workshopId != 0UL
                ? "workshop:" + workshopId
                : "level:"
                    + Normalize(name)
                    + "|"
                    + Normalize(author)
                    + "|"
                    + screens;
            return new ReplayWorld
            {
                Key = key,
                Name = name,
                Author = author,
                Revision = Revision(),
                TotalScreens = screens
            };
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string Revision()
        {
            string root = Game1.instance.contentManager.root;
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException(
                    "Jump King content root is unavailable");
            if (!Path.IsPathRooted(root))
            {
                string game = Path.GetDirectoryName(typeof(Game1).Assembly.Location);
                root = Path.Combine(game, root);
            }
            return JKRuntime.World.MapIdentity.ReplayV1(root);
        }
    }
}
