using System;
using System.IO;
using System.Linq;

namespace WorldsmithExtension
{
    internal static class GeneratedHistory
    {
        internal static IDisposable Lease(string directory)
        {
            Files.RequirePhysicalPath(directory);
            Directory.CreateDirectory(directory);
            return new HistoryLease(directory);
        }

        private sealed class HistoryLease : IDisposable
        {
            private readonly string path;
            private FileStream stream;
            internal HistoryLease(string directory)
            {
                // the lock stays outside the tree so Windows can rename or remove it
                path = directory + ".active";
                Files.RequirePhysicalPath(path);
                stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            }
            public void Dispose()
            {
                if (stream == null) return;
                stream.Dispose(); stream = null;
                try { File.Delete(path); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        internal static void Prune(string data, string area, string prefix, int keep, DateTime now)
        {
            if (keep < 1 || !(area == "builds" && prefix == "" || area == "tests" && (prefix == "build-" || prefix == "previous-")))
                throw new ArgumentException("Unknown generated history category.");
            string root = Path.GetFullPath(Path.Combine(data, area));
            if (!Directory.Exists(root)) return;
            try
            {
                Files.RequirePhysicalPath(root);
                var candidates = new DirectoryInfo(root).GetDirectories().Where(d => IsGenerated(d.Name, prefix))
                    .OrderByDescending(d => d.LastWriteTimeUtc).ThenBy(d => d.Name).Skip(keep).ToArray();
                foreach (var directory in candidates)
                {
                    if (directory.LastWriteTimeUtc > now.AddHours(-1)) continue;
                    try
                    {
                        if (!String.Equals(Path.GetDirectoryName(Path.GetFullPath(directory.FullName)), root, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("Generated history escaped its data directory.");
                        RequireDisposableTree(directory);
                        // another editor's build or open publication panel owns this lease
                        using (var lease = new HistoryLease(directory.FullName))
                            directory.Delete(true);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static bool IsGenerated(string name, string prefix)
        {
            Guid id;
            return name.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParseExact(name.Substring(prefix.Length), "N", out id);
        }

        private static void RequireDisposableTree(DirectoryInfo directory)
        {
            Files.RequirePhysicalPath(directory.FullName);
            foreach (var entry in directory.GetFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked generated content.");
                string name = entry.Name.ToLowerInvariant();
                if (name == ".keep" || name == "saves" || name == "savesperma" || name == "replays" || name.EndsWith(".sav") || name.EndsWith(".jkr"))
                    throw new IOException("Generated folder contains user state.");
                var child = entry as DirectoryInfo;
                if (child != null) RequireDisposableTree(child);
            }
        }
    }
}
