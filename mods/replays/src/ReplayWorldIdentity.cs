using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
            string[] names = { "level.xnb", "slopes.xnb" };
            using (MemoryStream aggregate = new MemoryStream())
            {
                int files = 0;
                foreach (string name in names)
                {
                    string path = Path.Combine(root, name);
                    if (!File.Exists(path)) continue;
                    files++;
                    byte[] label = Encoding.UTF8.GetBytes(name + "\0");
                    aggregate.Write(label, 0, label.Length);
                    using (FileStream file = File.OpenRead(path))
                    using (SHA256 fileHash = SHA256.Create())
                    {
                        byte[] fileDigest = fileHash.ComputeHash(file);
                        aggregate.Write(fileDigest, 0, fileDigest.Length);
                    }
                }
                if (files == 0)
                    throw new InvalidOperationException(
                        "Replay world assets are unavailable at " + root);
                aggregate.Position = 0;
                byte[] digest;
                using (SHA256 hash = SHA256.Create())
                    digest = hash.ComputeHash(aggregate);
                StringBuilder text = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest) text.Append(value.ToString("x2"));
                return text.ToString();
            }
        }
    }
}
