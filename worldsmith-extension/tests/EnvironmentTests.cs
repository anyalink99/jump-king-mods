using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class EnvironmentTests
    {
        internal static void Run(string dir, Action<bool, string> Assert, Action<Action, string> Fails)
        {
            var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
            InterfaceLanguage.Initialize();
            Assert(System.Threading.Thread.CurrentThread.CurrentUICulture.Name == "en-US", "English UI on a localized Windows installation");
            Assert(System.Threading.Thread.CurrentThread.CurrentCulture == originalCulture, "UI language preserves numeric and date culture");
            Assert(new System.Reflection.AmbiguousMatchException().Message == "Ambiguous match found.", "Framework errors use English resources");
            string workerLanguage = null;
            var languageThread = new System.Threading.Thread(() => workerLanguage = System.Threading.Thread.CurrentThread.CurrentUICulture.Name);
            languageThread.Start();
            languageThread.Join();
            Assert(workerLanguage == "en-US", "Background threads inherit English UI resources");
            var workerOutput = new System.Collections.Concurrent.ConcurrentBag<string>();
            string fixtureText = "Folder with spaces and caf\u00e9";
            string workerArguments = "--worker-fixture " + WorkerProcess.Quote(fixtureText);
            WorkerProcess.Run(typeof(Tests).Assembly.Location, workerArguments, dir, workerOutput.Add, "Fixture failed");
            Assert(workerOutput.Contains(fixtureText) && workerOutput.Contains("stderr captured") && workerOutput.Contains("en-US"), "worker arguments, UTF-8 streams and language environment");
            Fails(() => WorkerProcess.Run(typeof(Tests).Assembly.Location, workerArguments + " fail", dir, s =>
            {
            }, "Fixture failed"), "nonzero worker exit fails the build");
            try
            {
                WorkerProcess.Run(typeof(Tests).Assembly.Location, workerArguments + " fail", dir, s => {}, "Fixture failed");
                throw new Exception("Worker failure lost its diagnostic output");
            }
            catch (WorkerProcessFailure error)
            {
                Assert(error.Message.Contains("stderr captured") && error.Message.Contains("7"), "visible worker failure includes its cause and exit code");
                Assert(error.ToString().Contains(fixtureText) && error.ToString().Contains(dir), "copied worker report includes stdout and process context");
            }
            var oversized = new WorkerProcessFailure("Build failed", 9, "worker.exe", "--build", dir, new string('x', 8192), "wrapper ---> actual failure\nstack trace");
            Assert(oversized.Message.Contains("actual failure") && !oversized.Message.Contains("stack trace"), "worker summary unwraps reflection and keeps the full trace in details");
            var silent = new WorkerProcessFailure("Build failed", 9, "worker.exe", "--build", dir, "", "");
            Assert(silent.Message.Contains("No diagnostic output"), "silent crashes stay distinct from reported compiler errors");
            try
            {
                WorkerProcess.Run(typeof(Tests).Assembly.Location, workerArguments + " noisy", dir, s => {}, "Fixture failed");
                throw new Exception("Noisy worker unexpectedly succeeded");
            }
            catch (WorkerProcessFailure error)
            {
                Assert(error.Message.Contains("broken.png"), "a long compiler trace doesn't hide the initial failure");
                Assert(error.ToString().Contains("last output retained") && error.ToString().Length < 24000, "worker report retains the output tail within a bounded size");
            }
            string steam = Path.Combine(dir, "Steam"), library = Path.Combine(dir, "Library caf\u00e9"), bundle = Path.Combine(dir, "portable");
            Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
            Directory.CreateDirectory(bundle);
            string game = Path.Combine(library, "steamapps", "common", "Jump King");
            Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "JumpKing.exe"), "discovery fixture");
            string escaped = library.Replace("\\", "\\\\");
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            File.WriteAllText(vdf, "\"libraryfolders\" { \"0\" { \"path\" \"" + escaped + "\" \"apps\" { \"1061090\" \"1234\" } } }");
            Assert(SteamInstallations.Find(steam, "Jump King", "JumpKing.exe") == game, "game discovery reads secondary Steam libraries with Unicode paths");
            Assert(SteamInstallations.Game(bundle, Path.Combine(dir, "other library", "Jump King Workshop"), steam) == game, "worker game discovery does not assume Worldsmith and the game share a drive");
            File.WriteAllText(vdf, "\"libraryfolders\" { \"1\" \"" + escaped + "\" }");
            Assert(SteamInstallations.Find(steam, "Jump King", "JumpKing.exe") == game, "older Steam library lists still resolve");
            File.WriteAllText(Path.Combine(bundle, "game-path.txt"), game);
            Assert(SteamInstallations.Game(bundle, Path.Combine(dir, "host"), null) == game, "explicit game path remains authoritative");
        }
    }
}
