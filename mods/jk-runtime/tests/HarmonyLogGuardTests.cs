using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JumpKing.Mods;

namespace JKRuntime
{
    internal static class HarmonyLogGuardTests
    {
        private static Assembly engine;
        private static object owner;
        private static bool started;
        private static PropertyInfo Writer { get { return engine.GetType("HarmonyLib.FileLog", true).GetProperty("LogWriter"); } }
        private static MethodInfo Method(string name) { return typeof(HarmonyLogGuardTests).GetMethod(name, OwnedPatches.Members); }
        private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        [MethodImpl(MethodImplOptions.NoInlining)] private static int Subject() { return 4; }
        private static void After(ref int __result) { __result += 3; }
        private static void InvalidAfter(ref int __missing) { }

        // behaves like an old mod: raw Harmony, no Runtime patch wrapper
        public static void ForeignBeforeLevelLoad()
        {
            owner = Activator.CreateInstance(engine.GetType("HarmonyLib.Harmony", true), new object[] { "test.foreign-log" });
            Patch(Method("After"));
            started = true;
        }

        private static void Patch(MethodInfo callback)
        {
            var metadata = Activator.CreateInstance(engine.GetType("HarmonyLib.HarmonyMethod", true), new object[] { callback });
            owner.GetType().GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5)
                .Invoke(owner, new object[] { Method("Subject"), null, metadata, null, null });
        }

        private sealed class FailingWriter : StreamWriter
        {
            internal Exception Failure = new IOException("fixture: filesystem limitation", unchecked((int)0x80070299));
            internal int Writes;
            internal FailingWriter() : base(Stream.Null) { }
            public override void WriteLine(string value) { Writes++; throw Failure; }
        }

        private static void Main(string[] args)
        {
            string mode = args.Length > 1 ? args[1] : "early";
            string enginePath = Path.GetFullPath(args[0]);
            string folder = Path.GetFullPath(Path.Combine("build", "_work", "harmony-guard-tests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(folder);
            string log = Path.Combine(folder, "harmony.log.txt");
            Environment.CurrentDirectory = folder;
            Environment.SetEnvironmentVariable("HARMONY_DEBUG", "1");
            Environment.SetEnvironmentVariable("HARMONY_NO_LOG", null);
            Environment.SetEnvironmentVariable("HARMONY_LOG_FILE", log);
            if (mode != "late") engine = Assembly.LoadFrom(enginePath);
            var failing = mode == "custom" ? new FailingWriter() : null;
            if (failing != null) Writer.SetValue(null, failing, null);
            if (mode == "capped") File.WriteAllBytes(log, new byte[HarmonyLogScope.MaximumBytes]);
            using (var locked = mode == "early" || mode == "late" ? new FileStream(log, FileMode.Create, FileAccess.ReadWrite, FileShare.None) : null)
            {
                var loader = ModLoader.Instance;
                var foreign = new ModAssembly(typeof(HarmonyLogGuardTests).Assembly, new JumpKingModAttribute("Old foreign mod"));
                foreign.BeforeLevelLoadMethods.Clear();
                foreign.BeforeLevelLoadMethods.Add(Method("ForeignBeforeLevelLoad"));
                loader.LoadedMods.Add(foreign);

                // use the real loader's marker discovery, with the foreign mod already first
                string scan = Path.Combine(folder, "scan"); Directory.CreateDirectory(scan);
                File.Copy(Path.Combine(Path.GetDirectoryName(typeof(RuntimeApi).Assembly.Location), "JKRuntime.dll"), Path.Combine(scan, "JKRuntime.dll"));
                List<ModAssembly> discovered;
                loader.LoadMods(scan, out discovered);
                Require(discovered.Any(m => m.ModName == "JK Runtime"), "The native loader still recognizes Runtime's marker");
                var runtime = discovered.Single(m => m.ModName == "JK Runtime").Assembly;
                if (mode == "late") engine = Assembly.LoadFrom(enginePath);
                Require(runtime.GetType("JKRuntime.HarmonyLogGuard").GetField("InstallError", OwnedPatches.Members).GetValue(null) == null,
                    "The loaded engine accepts the early logging guard");
                loader.LoadedMods.RemoveAll(m => m != foreign);
                loader.CallBeforeLevelLoadMethods();
                Require(started && Subject() == 7, "The earlier foreign callback patches successfully with raw Harmony");
                var fileLog = engine.GetType("HarmonyLib.FileLog", true);
                fileLog.GetMethod("LogBuffered", new[] { typeof(string) }).Invoke(null, new object[] { "buffered foreign log" });
                fileLog.GetMethod("FlushBuffer").Invoke(null, null);
                bool rejected = false;
                try { Patch(Method("InvalidAfter")); }
                catch (TargetInvocationException) { rejected = true; }
                Require(rejected && Subject() == 7, "A real patch failure still propagates through the guard");
                owner.GetType().GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) })
                    .Invoke(owner, new object[] { Method("Subject"), Method("After") });
                Require(Subject() == 4, "Raw foreign cleanup survives logging failure too");
                if (mode != "healthy") Require(runtime.GetType("JKRuntime.HarmonyLogScope").GetField("LastError", OwnedPatches.Members).GetValue(null) != null,
                    "The blocked logger remains visible in the packaged Runtime's diagnostics");
            }
            if (mode == "healthy") Require(File.ReadAllText(log).Contains("buffered foreign log"), "Healthy logs keep both direct and buffered output");
            else
            {
                if (mode == "capped") Require(new FileInfo(log).Length == HarmonyLogScope.MaximumBytes, "The oversized log is kept unchanged");
                if (failing != null) Require(failing.Writes == 1, "A failing custom writer is retained but isn't hammered repeatedly");
            }
            if (mode == "healthy")
            {
                using (var writer = new StringWriterStream())
                {
                    Writer.SetValue(null, writer, null);
                    using (new OwnedPatches("test.replacement-writer")) { }
                    Require(ReferenceEquals(Writer.GetValue(null, null), writer) && writer.Writes > 0, "A later replacement writer stays owned by its caller");
                }
                using (var writer = new FailingWriter { Failure = new InvalidOperationException("fixture: not an I/O error") })
                {
                    Writer.SetValue(null, writer, null); HarmonyLogScope.Install(engine);
                    bool rejected = false;
                    try { Activator.CreateInstance(engine.GetType("HarmonyLib.Harmony", true), new object[] { "test.non-io" }); }
                    catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
                    Require(rejected, "The persistent guard doesn't swallow non-I/O writer errors");
                }
            }
            Console.WriteLine("[OK] Native discovery log guard: " + mode + " / Harmony " + engine.GetName().Version);
        }

        private sealed class StringWriterStream : StreamWriter
        {
            internal int Writes;
            internal StringWriterStream() : base(Stream.Null) { }
            public override void WriteLine(string value) { Writes++; }
        }
    }
}
