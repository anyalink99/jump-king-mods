using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WorldsmithExtension
{
    internal static class SteamInstallations
    {
        internal static IEnumerable<string> Libraries(string steam)
        {
            if (String.IsNullOrWhiteSpace(steam)) return new string[0];
            var paths = new List<string> { steam };
            string file = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            string text = "";
            try { if (File.Exists(file)) text = File.ReadAllText(file); }
            catch (IOException) { /* the primary library can still work */ }
            catch (UnauthorizedAccessException) { /* the editor can ask for its executable */ }
            foreach (Match match in Regex.Matches(text, "\"(?:path|[0-9]+)\"\\s*\"([^\"]+)\""))
            {
                string path = match.Groups[1].Value.Replace("\\\\", "\\");
                if (Path.IsPathRooted(path)) paths.Add(path);
            }
            return paths.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        internal static string Find(string steam, string folder, string executable)
        {
            return Libraries(steam).Select(p => Path.Combine(p, "steamapps", "common", folder))
                .FirstOrDefault(p => File.Exists(Path.Combine(p, executable)));
        }

        internal static string Game(string bundle, string host, string steam)
        {
            string config = Path.Combine(bundle, "game-path.txt");
            if (File.Exists(config)) return Path.GetFullPath(File.ReadAllText(config).Trim());
            string adjacent = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(host)), "Jump King");
            if (File.Exists(Path.Combine(adjacent, "JumpKing.exe"))) return adjacent;
            return Find(steam, "Jump King", "JumpKing.exe") ?? "";
        }
    }
}
