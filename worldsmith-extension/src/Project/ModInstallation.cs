using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WorldsmithExtension
{
    internal static class ModInstallation
    {
        internal static string SelectTarget(string game, string content)
        {
            string[] assemblies = Directory.GetFiles(content, "*.dll").Select(Path.GetFileName).Where(name => !name.EndsWith("Api.dll", StringComparison.OrdinalIgnoreCase) && !new[]{"0Harmony.dll", "JKRuntime.dll", "MonoGame.Framework.dll", "Steamworks.NET.dll"}.Contains(name, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (assemblies.Length == 0 && File.Exists(Path.Combine(content, "JKRuntime.dll")))
                assemblies = new[]{"JKRuntime.dll"};
            if (assemblies.Length == 0)
                throw new InvalidDataException("No primary mod assembly was found in this package.");
            string local = Path.Combine(game, "Content", "JKMods");
            string workshop = Path.Combine(Directory.GetParent(game).Parent.FullName, "workshop", "content", "1061090");
            var candidates = new List<string>();
            foreach (string parent in new[]{workshop, local})
            {
                if (!Directory.Exists(parent))
                    continue;
                foreach (string folder in Directory.GetDirectories(parent))
                {
                    if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                        continue;
                    if (assemblies.Any(name => File.Exists(Path.Combine(folder, name))))
                        candidates.Add(folder);
                }
            }

            if (Directory.Exists(local) && assemblies.Any(name => File.Exists(Path.Combine(local, name))))
                throw new IOException("A loose copy of this mod exists directly in Content/JKMods. Move it into its own mod folder before testing.");
            if (candidates.Count > 1)
                throw new IOException("Multiple installed copies of this mod were found. Keep one active copy before testing.");
            return candidates.Count == 1 ? candidates[0] : Path.Combine(local, "Worldsmith-" + Path.GetFileNameWithoutExtension(assemblies.OrderBy(x => x).First()));
        }

        internal static void Install(string content, string target, string backup, Action<string> status, Action<string, string> copy = null)
        {
            copy = copy ?? ((source, destination) =>
            {
                using (var input = File.OpenRead(source)) Files.Atomic(destination, stream => input.CopyTo(stream));
            });
            var changed = new List<Tuple<string, string>>();
            try
            {
                foreach (string source in Files.Sources(content))
                {
                    string relative = Files.Relative(content, source);
                    string destination = CheckedPath(target, Path.Combine(target, relative));
                    string name = Path.GetFileName(relative).ToLowerInvariant();
                    if (File.Exists(destination) && (name.Contains("settings") || name == "config.json" || name == "config.xml"))
                        continue;
                    string previous = null;
                    if (File.Exists(destination))
                    {
                        previous = Path.Combine(backup, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(previous));
                        File.Copy(destination, previous, false);
                    }

                    status("Installing test mod: " + relative);
                    changed.Add(Tuple.Create(destination, previous));
                    copy(source, destination);
                }
            }
            catch (Exception failure)
            {
                var errors = new List<Exception> { failure };
                foreach (var file in changed.AsEnumerable().Reverse())
                {
                    try
                    {
                        CheckedPath(target, file.Item1);
                        if (file.Item2 != null) copy(file.Item2, file.Item1);
                        else if (File.Exists(file.Item1)) File.Delete(file.Item1);
                    }
                    catch (Exception rollback) { errors.Add(new IOException("Could not restore " + file.Item1, rollback)); }
                }
                if (errors.Count > 1) throw new IOException("Test installation failed and some files could not be restored. Backups: " + backup, new AggregateException(errors));
                throw;
            }
        }

        static string CheckedPath(string root, string path)
        {
            Files.Relative(root, path);
            Files.RequirePhysicalPath(path);
            return path;
        }
    }
}
