using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace WorldsmithExtension
{
    internal static class LegacyFolderImport
    {
        static DateTime refreshAt;
        static List<WorkshopFolder> saved = new List<WorkshopFolder>();
        internal static void Invalidate() { refreshAt = DateTime.MinValue; }
        internal static List<WorkshopFolder> Read()
        {
            if (DateTime.UtcNow < refreshAt) return saved.ToList();
            var paths = new List<string> { Engine.Host };
            string steam = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
            if (!String.IsNullOrEmpty(steam))
            {
                paths.Add(Path.Combine(steam, "steamapps", "common", "Jump King Workshop"));
                string libraries = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraries))
                    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(libraries), "\"path\"\\s*\"([^\"]+)\""))
                        paths.Add(Path.Combine(match.Groups[1].Value.Replace("\\\\", "\\"), "steamapps", "common", "Jump King Workshop"));
            }
            var result = new List<WorkshopFolder>();
            foreach (string folder in paths.Distinct(StringComparer.OrdinalIgnoreCase))
                try { result.AddRange(WorkshopLibrary.ReadLegacy(Path.Combine(folder, "cached_saved_folders.set"))); }
                catch (Exception error) { Engine.Log("Cannot read Legacy folder links: " + error.Message); }
            saved = result;
            refreshAt = DateTime.UtcNow.AddSeconds(5);
            return saved.ToList();
        }
    }
}
