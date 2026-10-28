using System;
using System.IO;
using System.Linq;
using System.Text;
using JumpKingModTools;

namespace WorldsmithExtension
{
    internal static class StorageTests
    {
        internal static void Run(string temporary, Action<bool, string> assert)
        {
            string root = Path.Combine(temporary, "storage");
            Directory.CreateDirectory(root);
            string log = Path.Combine(root, "bounded.log");
            // emulate large logs left by an older release, not just fresh files
            foreach (string suffix in new[] { "", ".1", ".2" })
                File.WriteAllText(log + suffix, new string('x', BoundedTextLog.MaximumBytes + 100) + "RECENT", new UTF8Encoding(false));
            BoundedTextLog.Append(log, "new entry");
            assert(File.ReadAllText(log + ".1").EndsWith("RECENT"), "log migration retains the newest bytes of a legacy oversized log");
            for (int i = 0; i < 900; i++) BoundedTextLog.Append(log, new string('\u0416', 8192) + " " + i);
            assert(Directory.GetFiles(root, "bounded.log*").Length == 3, "log history stays at three generations");
            foreach (string path in Directory.GetFiles(root, "bounded.log*"))
            {
                assert(new FileInfo(path).Length <= BoundedTextLog.MaximumBytes, "each log stays within its byte budget");
                new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path));
            }
            assert(File.ReadAllText(log).Contains("899"), "rotation keeps the latest event");
            string builds = Path.Combine(root, "builds");
            string[] folders = Enumerable.Range(0, 7).Select(i => Path.Combine(builds, Guid.NewGuid().ToString("N"))).ToArray();
            for (int i = 0; i < folders.Length; i++)
            {
                Directory.CreateDirectory(folders[i]);
                File.WriteAllText(Path.Combine(folders[i], "marker"), "generated");
                Directory.SetLastWriteTimeUtc(folders[i], DateTime.UtcNow.AddDays(-3).AddMinutes(i));
            }
            var active = GeneratedHistory.Lease(folders[0]);
            Directory.SetLastWriteTimeUtc(folders[0], DateTime.UtcNow.AddDays(-4));
            Directory.CreateDirectory(Path.Combine(folders[1], "Saves"));
            Directory.SetLastWriteTimeUtc(folders[1], DateTime.UtcNow.AddDays(-3));
            string manual = Path.Combine(builds, "manual-backup");
            Directory.CreateDirectory(manual);
            GeneratedHistory.Prune(root, "builds", "", 3, DateTime.UtcNow);
            assert(Directory.Exists(folders[0]) && Directory.Exists(folders[1]) && Directory.Exists(manual), "cleanup preserves active builds, saves and unrecognized directories");
            assert(!Directory.Exists(folders[2]) && !Directory.Exists(folders[3]), "cleanup removes old generated builds beyond the count limit");
            active.Dispose();
            Directory.SetLastWriteTimeUtc(folders[0], DateTime.UtcNow.AddDays(-4));
            GeneratedHistory.Prune(root, "builds", "", 3, DateTime.UtcNow);
            assert(!Directory.Exists(folders[0]), "a released build lease allows later cleanup");
            string recent = Path.Combine(builds, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(recent);
            GeneratedHistory.Prune(root, "builds", "", 1, DateTime.UtcNow);
            assert(Directory.Exists(recent), "cleanup leaves new work alone");
        }
    }
}
