using System;
using System.Diagnostics;
using System.IO;
using JKRuntime.Settings;

namespace JKRuntime
{
    public static class SettingsFileTests
    {
        public sealed class Preferences { public int Choice { get; set; } }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Reject(Action action)
        { try { action(); } catch (IOException) { return; } catch (InvalidOperationException) { return; } throw new Exception("Expected save rejection"); }
        private static SettingsFile<Preferences> Open(string path)
        { return new SettingsFile<Preferences>(path, delegate { return new Preferences { Choice = 7 }; }); }
        private static void Main()
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "preferences.xml");
            var missing = Open(path);
            Check(missing.Status == SettingsReadStatus.Missing && missing.Value.Choice == 7 && !File.Exists(path), "Missing defaults perform no write");
            missing.Save(new Preferences { Choice = 9 });
            string first = File.ReadAllText(path);
            missing.Save(new Preferences { Choice = 11 });
            Check(File.ReadAllText(path + ".bak") == first && missing.Value.Choice == 11, "Successful replacement retains the previous exact bytes");

            const string broken = "<broken user settings";
            File.WriteAllText(path, broken);
            var recovered = Open(path);
            Check(recovered.Status == SettingsReadStatus.Recovered && recovered.Value.Choice == 9 && !recovered.CanSave, "Valid backup is used read-only");
            Reject(delegate { recovered.Save(new Preferences()); });
            Check(File.ReadAllText(path) == broken && File.ReadAllText(path + ".bak") == first, "Recovery does not replace either original or backup");

            string corrupt = Path.Combine(root, "corrupt.xml");
            File.WriteAllText(corrupt, broken);
            var unreadable = Open(corrupt);
            Check(unreadable.Status == SettingsReadStatus.Unreadable && unreadable.Value.Choice == 7, "Corruption differs from missing data");
            Reject(delegate { unreadable.Save(new Preferences()); });
            Check(File.ReadAllText(corrupt) == broken, "Corrupt input is preserved on later edits too");

            string future = Path.Combine(root, "future.xml");
            const string newer = "<Preferences><Choice>5</Choice><Version>999</Version><NewFeature>keep</NewFeature></Preferences>";
            File.WriteAllText(future, newer);
            var unsupported = Open(future);
            Check(unsupported.Status == SettingsReadStatus.Unsupported && !unsupported.CanSave, "Unknown future data cannot be downgraded silently");
            Reject(delegate { unsupported.Save(new Preferences()); });
            Check(File.ReadAllText(future) == newer, "Unknown fields remain byte-identical");

            string external = Path.Combine(root, "external.xml");
            var original = Open(external);
            original.Save(new Preferences { Choice = 4 });
            var second = Open(external);
            original.Save(new Preferences { Choice = 6 });
            Reject(delegate { second.Save(new Preferences { Choice = 8 }); });
            Check(second.Value.Choice == 4 && Open(external).Value.Choice == 6, "Stale writers neither overwrite external changes nor publish their candidate");
            using (var locked = new FileStream(external, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Reject(delegate { original.Save(new Preferences { Choice = 10 }); });
            Check(original.Value.Choice == 6 && Open(external).Value.Choice == 6, "Failed write leaves current settings intact");

            // Compare equivalent warm reads; this is preparation/menu work, never
            // a frame getter. Report the cost instead of a machine-specific limit.
            const int count = 100;
            AtomicXmlFile.Load<Preferences>(external); Open(external);
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < count; i++) AtomicXmlFile.Load<Preferences>(external);
            double oldMs = clock.Elapsed.TotalMilliseconds / count;
            clock.Restart();
            for (int i = 0; i < count; i++) Open(external);
            double safeMs = clock.Elapsed.TotalMilliseconds / count;
            Console.WriteLine("[COST] Settings read: legacy={0:F3}ms protected={1:F3}ms; one load per store, no getter IO", oldMs, safeMs);
            Console.WriteLine("[OK] Settings recovery, retained backups, unknown schemas, stale writers and failed writes");
        }
    }
}
