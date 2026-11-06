using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace JKRuntime
{
    internal static class HarmonyLogTests
    {
        [MethodImpl(MethodImplOptions.NoInlining)] private static int Subject() { return 4; }
        private static void After(ref int __result) { __result += 3; }
        private static MethodInfo Method(string name) { return typeof(HarmonyLogTests).GetMethod(name, OwnedPatches.Members); }
        private static void Require(bool ok, string text) { if (!ok) throw new Exception(text); }
        private sealed class FailingWriter : StreamWriter
        {
            internal Exception Failure = new IOException("fixture: log flush failed", unchecked((int)0x80070299));
            internal FailingWriter() : base(Stream.Null) { }
            public override void WriteLine(string value) { throw Failure; }
        }

        internal static void Run(Assembly engine)
        {
            var harmony = engine.GetType("HarmonyLib.Harmony", true);
            var debug = harmony.GetField("DEBUG");
            var writer = engine.GetType("HarmonyLib.FileLog", true).GetProperty("LogWriter");
            // older engines without a writer override keep their existing behavior
            if (writer == null) return;
            var previous = writer.GetValue(null, null);
            object previousDebug = debug.GetValue(null);
            string environment = Environment.GetEnvironmentVariable("HARMONY_DEBUG");
            string oldPath = Environment.GetEnvironmentVariable("HARMONY_LOG_FILE");
            string noLog = Environment.GetEnvironmentVariable("HARMONY_NO_LOG");
            string folder = Path.GetFullPath(Path.Combine("build", "_work", "harmony-log-tests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(folder);
            string log = Path.Combine(folder, "harmony.log.txt");
            try
            {
                Environment.SetEnvironmentVariable("HARMONY_DEBUG", "1");
                Environment.SetEnvironmentVariable("HARMONY_LOG_FILE", log);
                Environment.SetEnvironmentVariable("HARMONY_NO_LOG", null);
                writer.SetValue(null, null, null);
                using (var locked = new FileStream(log, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    bool reproduced = false;
                    try { Activator.CreateInstance(harmony, new object[] { "test.raw-logging-failure" }); }
                    catch (TargetInvocationException error) { reproduced = error.InnerException is IOException; }
                    Require(reproduced, "Unprotected Harmony constructor reproduces a log I/O crash");
                    using (var patches = new OwnedPatches("test.locked-log"))
                    {
                        patches.Add(Method("Subject"), postfix: Method("After"));
                        Require(Subject() == 7, "Log I/O failure doesn't prevent the actual patch");
                    }
                    Require(Subject() == 4 && HarmonyLogScope.LastError != null, "Log I/O failure doesn't prevent cleanup and remains inspectable");
                }
                using (var healthy = new OwnedPatches("test.healthy-log")) { }
                Require(File.ReadAllText(log).Contains("test.healthy-log"), "Writable debug logs still receive messages");
                File.WriteAllBytes(log, new byte[HarmonyLogScope.MaximumBytes]);
                using (var capped = new OwnedPatches("test.capped-log")) { }
                Require(new FileInfo(log).Length == HarmonyLogScope.MaximumBytes, "An oversized log is preserved without further growth");
                using (var failing = new FailingWriter())
                {
                    writer.SetValue(null, failing, null);
                    using (var patches = new OwnedPatches("test.failed-flush"))
                    {
                        patches.Add(Method("Subject"), postfix: Method("After"));
                        Require(Subject() == 7, "A filesystem-limit write failure doesn't lose the patch");
                    }
                    Require(Subject() == 4 && ReferenceEquals(writer.GetValue(null, null), failing), "Failed writes preserve the caller's writer and patch cleanup");
                    failing.Failure = new InvalidOperationException("fixture: non-I/O failure");
                    bool rejected = false;
                    try { new OwnedPatches("test.non-io-error"); }
                    catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
                    Require(rejected && ReferenceEquals(writer.GetValue(null, null), failing), "Non-I/O failures still propagate and restore the writer");
                }
                Require((bool)debug.GetValue(null), "Logging guards preserve Harmony debug mode");
            }
            finally
            {
                writer.SetValue(null, previous, null);
                debug.SetValue(null, previousDebug);
                Environment.SetEnvironmentVariable("HARMONY_DEBUG", environment);
                Environment.SetEnvironmentVariable("HARMONY_LOG_FILE", oldPath);
                Environment.SetEnvironmentVariable("HARMONY_NO_LOG", noLog);
            }
            Console.WriteLine("[OK] Harmony log failures: locked file, filesystem-limit writer, capped output, cleanup and original errors");
        }
    }
}
