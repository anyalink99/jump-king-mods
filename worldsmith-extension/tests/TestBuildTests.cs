using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace WorldsmithExtension
{
    internal static class TestBuildTests
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CommandLineToArgvW(string command, out int count);
        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr memory);

        internal static void Run(string temporary, Action<bool, string> assert, Action<Action, string> fails)
        {
            string root = Path.Combine(temporary, "test map with spaces"), data = Path.Combine(temporary, "test-data");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "map.xml"), "first");
            int builds = 0;
            Action<string> build = destination => { builds++; ProjectFormat.Snapshot(root, destination, message => {}); };
            Func<string> prepare = () => TestBuild.Prepare(data, root, "Level", build, () => false, message => {});
            string content = prepare();
            assert(prepare() == content && builds == 1, "unchanged test build reuses the verified output and launch path");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(content), "test-engine.sha256"), "previous extension build");
            prepare();
            assert(builds == 2, "updating the extension invalidates its earlier test builds");
            assert(TestBuild.ProjectDirectory(data, root.ToUpperInvariant() + "\\", "Level") == TestBuild.ProjectDirectory(data, root, "Level"), "test identity ignores Windows path casing and trailing separators");
            assert(TestBuild.ProjectDirectory(data, root, "Mod") != TestBuild.ProjectDirectory(data, root, "Level"), "test identities keep project categories separate");
            File.WriteAllText(Path.Combine(content, "stale.xml"), "old output");
            File.WriteAllText(Path.Combine(root, "map.xml"), "second");
            assert(prepare() == content && File.ReadAllText(Path.Combine(content, "map.xml")) == "second" && !File.Exists(Path.Combine(content, "stale.xml")), "changed map replaces the test copy at the same path without stale assets");
            string project = TestBuild.ProjectDirectory(data, root, "Level");
            assert(Directory.GetDirectories(Path.GetDirectoryName(project), "previous-*").Any(folder => File.Exists(Path.Combine(folder, "content", "stale.xml"))), "previous test content is retained separately");
            File.WriteAllText(Path.Combine(root, "map.xml"), "third");
            fails(() => TestBuild.Prepare(data, root, "Level", destination => { Directory.CreateDirectory(destination); throw new IOException("Compile failed"); }, () => false, message => {}), "failed test build is rejected");
            assert(File.ReadAllText(Path.Combine(content, "map.xml")) == "second", "failed compilation leaves the last test copy intact");
            bool running = false;
            fails(() => TestBuild.Prepare(data, root, "Level", destination => { build(destination); running = true; }, () => running, message => {}), "game starting during build prevents replacement");
            assert(File.ReadAllText(Path.Combine(content, "map.xml")) == "second", "running game keeps its original files");
            using (var lease = new FileStream(Path.Combine(project, "prepare.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                fails(() => prepare(), "two editors cannot replace the same test folder concurrently");
            assert(prepare() == content, "retry retains the stable test location");
            Directory.CreateDirectory(Path.Combine(root, "empty-resource"));
            prepare();
            assert(Directory.Exists(Path.Combine(content, "empty-resource")), "adding an empty resource directory invalidates the test build");
            int beforeMod = builds;
            for (int i = 0; i < 2; i++) TestBuild.Prepare(data, root, "Mod", build, () => false, message => {});
            assert(builds == beforeMod + 2, "mod builds are not reused because their dependencies may live outside the project");

            string steam = Path.Combine(temporary, "Steam client", "steam.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(steam));
            File.WriteAllText(steam, "launch descriptor fixture, never executed");
            var launch = TestBuild.Launch(steam, content, "Level");
            var args = Arguments(WorkerProcess.Quote(launch.FileName) + " " + launch.Arguments);
            assert(args.SequenceEqual(new[] { steam, "-applaunch", "1061090", "-debug", content }), "Steam receives exactly one debug folder argument, including spaces");
            assert(!launch.UseShellExecute && !launch.EnvironmentVariables.ContainsKey("SteamAppId") && !launch.EnvironmentVariables.ContainsKey("SteamGameId"), "game launch does not inherit the editor's Steam identity");
            assert(Arguments(WorkerProcess.Quote(steam) + " " + TestBuild.Launch(steam, content, "Mod").Arguments).Length == 3, "testing a mod launches the game without map debug arguments");
        }

        static string[] Arguments(string command)
        {
            int count;
            IntPtr memory = CommandLineToArgvW(command, out count);
            if (memory == IntPtr.Zero) throw new InvalidOperationException("Cannot parse launch arguments");
            try { return Enumerable.Range(0, count).Select(index => Marshal.PtrToStringUni(Marshal.ReadIntPtr(memory, index * IntPtr.Size))).ToArray(); }
            finally { LocalFree(memory); }
        }
    }
}
