using System;
using System.IO;
using JKRuntime;

namespace MegaGameplayExpansion
{
    internal static class WarpDiagnostics
    {
        internal static string OutputDirectory { get; set; }
        private static bool reportedError;
        private static readonly BackgroundWorkQueue writer = new BackgroundWorkQueue("mega-gameplay.diagnostics", 32);
        private static string defaultDirectory;
        private static int dropped;
        internal static int Dropped { get { return System.Threading.Volatile.Read(ref dropped); } }
        internal static bool Drain(int milliseconds) { return writer.Drain(milliseconds); }
        internal static void Write(string message)
        {
            if (message == null) return;
            if (message.Length > 16384) message = message.Substring(0, 16384) + " [truncated]";
            if (defaultDirectory == null) defaultDirectory = PackageHost.GetDataDirectory(typeof(ModEntry).Assembly);
            string directory = OutputDirectory ?? defaultDirectory;
            string entry = DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine;
            BackgroundWork work;
            if (!writer.TryEnqueue(delegate { Append(directory, entry); }, out work))
                System.Threading.Interlocked.Increment(ref dropped);
        }
        private static void Append(string directory, string entry)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "MegaGameplayExpansion.log");
                // At most one current and one previous log, no per-frame writes.
                if (File.Exists(path) && new FileInfo(path).Length >= 524288)
                { File.Copy(path, path + ".previous", true); File.WriteAllText(path, ""); }
                int skipped = System.Threading.Interlocked.Exchange(ref dropped, 0);
                if (skipped != 0) entry = "[Diagnostics] Queue capacity reached; skipped " + skipped + " messages" + Environment.NewLine + entry;
                File.AppendAllText(path, entry);
            }
            catch (Exception error)
            {
                if (!reportedError) Console.WriteLine("[Mega Gameplay Expansion] Diagnostic file unavailable: " + error.Message);
                reportedError = true;
            }
        }
    }
}
