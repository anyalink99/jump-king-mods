using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using JumpKing.Mods;

internal static class ColdDiscoveryTests
{
    private static int Main(string[] args)
    {
        try
        {
            // this process starts without Runtime or Multiplayer beside its executable
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs request) {
                string name = new AssemblyName(request.Name).Name;
                foreach (string suffix in new[] { ".dll", ".exe" }) {
                    string path = Path.Combine(args[1], name + suffix);
                    if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            var logs = new List<string>();
            var scan = typeof(ModLoader).GetMethod("GetModAssemblies", BindingFlags.NonPublic | BindingFlags.Instance);
            var mods = (List<ModAssembly>)scan.Invoke(new ModLoader(), new object[] { args[0], logs });
            if (logs.Count != 0) throw new Exception(string.Join("\n", logs));
            if (mods.Count != 1 || mods[0].ModName != "Multiplayer Expansion") throw new Exception("Native loader didn't discover exactly one expansion.");
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "JKRuntime" || a.GetName().Name == "JumpKingMultiplayer"))
                throw new Exception("Native discovery pulled in an unloaded mod dependency.");
            var entry = mods[0].Assembly.GetTypes().Single(t => t.IsDefined(typeof(JumpKingModAttribute), false));
            if (entry.GetMethods().Count(m => m.IsDefined(typeof(MainMenuItemSettingAttribute), false)) != 1 ||
                entry.GetMethods().Count(m => m.IsDefined(typeof(PauseMenuItemSettingAttribute), false)) != 1)
                throw new Exception("Native shell lost a settings menu.");

            // now reproduce the game's handoff after native mod enumeration
            foreach (string file in new[] { "Newtonsoft.Json.dll", "JumpKingMultiplayer.dll" }) Assembly.LoadFrom(Path.Combine(args[2], file));
            Assembly runtime = Assembly.LoadFrom(args[3]);
            ModLoader.Instance.LoadedMods.Add(mods[0]);
            Type host = runtime.GetType("JKRuntime.PackageHost", true);
            host.GetMethod("Discover").Invoke(null, null);
            string[] errors = (string[])host.GetProperty("Errors").GetValue(null, null);
            if (errors.Length != 0) throw new Exception(string.Join("\n", errors));
            Assembly implementation = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MultiplayerExpansion.Module");
            string directory = (string)host.GetMethod("GetDataDirectory").Invoke(null, new object[] { implementation });
            if (Path.GetFullPath(directory) != Path.GetFullPath(args[0])) throw new Exception("Settings/helper directory escaped the installed package.");
            var native = implementation.GetType("MultiplayerExpansion.NativeMod", true);
            native.GetMethod("Initialize").Invoke(null, null);
            string child = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "staged-client");
            string package = Path.Combine(child, "WorkshopMods", "3816170337");
            Directory.CreateDirectory(package);
            foreach (string file in Directory.GetFiles(args[0])) File.Copy(file, Path.Combine(package, Path.GetFileName(file)));
            Directory.CreateDirectory(Path.Combine(child, "WorkshopMods", "3793086563"));
            File.Copy(args[3], Path.Combine(child, "WorkshopMods", "3793086563", "JKRuntime.dll"));
            Directory.CreateDirectory(Path.Combine(child, "WorkshopMods", "3190590114"));
            foreach (string file in new[] { "Newtonsoft.Json.dll", "JumpKingMultiplayer.dll" })
                File.Copy(Path.Combine(args[2], file), Path.Combine(child, "WorkshopMods", "3190590114", file));
            Directory.CreateDirectory(Path.Combine(child, "WorkshopMods", "unrelated"));
            File.WriteAllText(Path.Combine(child, "WorkshopMods", "unrelated", "Newtonsoft.Json.dll"), "another mod's private JSON dependency");
            foreach (string file in Directory.GetFiles(args[1]).Where(f => new[] { ".exe", ".dll", ".config" }.Contains(Path.GetExtension(f))))
                File.Copy(file, Path.Combine(child, Path.GetFileName(file)));
            foreach (string file in new[] { "MultiplayerExpansion.exe", "MultiplayerExpansion.exe.config", "0Harmony.dll" })
                File.Copy(Path.Combine(args[0], file), Path.Combine(child, file));
            string result = Path.Combine(child, "bootstrap-result.txt");
            var start = new ProcessStartInfo(Path.Combine(child, "MultiplayerExpansion.exe")) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = child };
            start.EnvironmentVariables["MPEX_BOOTSTRAP_CHECK"] = "1";
            start.EnvironmentVariables["MPEX_BOOTSTRAP_RESULT"] = result;
            start.EnvironmentVariables["MPEX_SESSION"] = child;
            start.EnvironmentVariables["MPEX_ROLE"] = "2";
            using (var process = Process.Start(start)) {
                if (!process.WaitForExit(20000)) { process.Kill(); throw new Exception("SDK client bootstrap timed out."); }
                if (process.ExitCode != 0) throw new Exception("SDK client bootstrap failed: " + File.ReadAllText(Path.Combine(child, "client2.error.txt")));
            }
            string[] report = File.ReadAllLines(result);
            if (report[0] != implementation.FullName || Path.GetFullPath(report[1]) != Path.GetFullPath(package))
                throw new Exception("The client runs a different implementation or settings directory.");
            Console.WriteLine("[OK] Thin client bootstrap uses one SDK implementation across repeated native discovery");
            Console.WriteLine("[OK] Cold native scan before dependencies, both menus, one SDK implementation, installed data path and network initialization");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
