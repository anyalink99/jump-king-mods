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
        }
    }
}
