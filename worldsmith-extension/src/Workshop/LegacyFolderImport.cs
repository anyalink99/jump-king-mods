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
            paths.AddRange(SteamInstallations.Libraries(steam).Select(p => Path.Combine(p, "steamapps", "common", "Jump King Workshop")));
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
