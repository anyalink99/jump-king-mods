using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using JumpKing.Mods;

namespace MultiplayerExpansion.Bootstrap
{
    internal static class Entry
    {
        [STAThread]
        private static int Main()
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                Assembly implementation = LoadImplementation();
                if (Environment.GetEnvironmentVariable("MPEX_BOOTSTRAP_CHECK") == "1")
                {
                    // repeat the native handoff that happens later in Game1.Initialize
                    Assembly shell = LoadShared(Find("MultiplayerExpansion.dll"));
                    var attribute = shell.GetTypes().SelectMany(t => t.GetCustomAttributes(typeof(JumpKingModAttribute), false)).Cast<JumpKingModAttribute>().Single();
                    ModLoader.Instance.LoadedMods.Add(new ModAssembly(shell, attribute));
                    RuntimeHost().GetMethod("Discover").Invoke(null, null);
                    if (AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "MultiplayerExpansion.Module") != 1 ||
                        ((string[])RuntimeHost().GetProperty("Errors").GetValue(null, null)).Length != 0)
                        throw new InvalidOperationException("The native handoff duplicated the SDK implementation.");
                    string directory = (string)RuntimeHost().GetMethod("GetDataDirectory").Invoke(null, new object[] { implementation });
                    File.WriteAllText(Environment.GetEnvironmentVariable("MPEX_BOOTSTRAP_RESULT"), implementation.FullName + "\n" + directory);
                    return 0;
                }
                return (int)implementation.GetType("MultiplayerExpansion.Entry", true)
                    .GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            }
            catch (Exception error)
            {
                string session = Environment.GetEnvironmentVariable("MPEX_SESSION");
                string role = Environment.GetEnvironmentVariable("MPEX_ROLE");
                if (!string.IsNullOrEmpty(session)) File.WriteAllText(Path.Combine(session, "client" + role + ".error.txt"), error.ToString());
                else MessageBox.Show(error.ToString(), "Multiplayer Expansion");
                return 1;
            }
        }

        private static string[] Roots()
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            // the old standalone lab host lives one directory above its clients
            return Directory.Exists(Path.Combine(root, "client1")) ? new[] { root, Path.Combine(root, "client1") } : new[] { root };
        }

        private static string Find(string name)
        {
            foreach (string root in Roots())
            {
                string direct = Path.Combine(root, name);
                if (File.Exists(direct)) return direct;
                string workshopId = name == "JKRuntime.dll" ? "3793086563" :
                    name == "Newtonsoft.Json.dll" || name == "JumpKingMultiplayer.dll" ? "3190590114" : null;
                if (workshopId != null)
                {
                    string installed = Path.Combine(root, "WorkshopMods", workshopId, name);
                    if (File.Exists(installed)) return installed;
                }
                foreach (string folder in new[] { "WorkshopMods", "Content/JKMods" })
                {
                    string path = Path.Combine(root, folder);
                    if (!Directory.Exists(path)) continue;
                    string[] files = Directory.GetFiles(path, name, SearchOption.AllDirectories);
                    if (files.Length > 1) throw new InvalidOperationException("Duplicate staged dependency: " + name);
                    if (files.Length == 1) return files[0];
                }
            }
            throw new FileNotFoundException("Missing staged dependency: " + name);
        }

        private static Assembly Resolve(object sender, ResolveEventArgs request)
        {
            string name = new AssemblyName(request.Name).Name;
            // resources use normal CLR fallback; never pick another mod's implementation
            if (name.EndsWith(".resources", StringComparison.Ordinal) || name.EndsWith(".Module", StringComparison.Ordinal)) return null;
            foreach (string extension in new[] { ".dll", ".exe" })
            {
                try { return LoadShared(Find(name + extension)); }
                catch (FileNotFoundException) { }
            }
            return null;
        }

        private static Assembly LoadShared(string path)
        {
            string identity = AssemblyName.GetAssemblyName(path).FullName;
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == identity) ?? Assembly.LoadFrom(path);
        }

        private static Type RuntimeHost()
        { return LoadShared(Find("JKRuntime.dll")).GetType("JKRuntime.PackageHost", true); }

        private static Assembly LoadImplementation()
        {
            Assembly shell = LoadShared(Find("MultiplayerExpansion.dll"));
            var attribute = shell.GetTypes().SelectMany(t => t.GetCustomAttributes(typeof(JumpKingModAttribute), false))
                .Cast<JumpKingModAttribute>().Single();
            LoadShared(Find("Newtonsoft.Json.dll"));
            LoadShared(Find("JumpKingMultiplayer.dll"));
            Type host = RuntimeHost();
            var mod = new ModAssembly(shell, attribute);
            // only lend the shell to discovery; the real game will enumerate native mods
            ModLoader.Instance.LoadedMods.Add(mod);
            try { host.GetMethod("Discover").Invoke(null, null); }
            finally { ModLoader.Instance.LoadedMods.Remove(mod); }
            string[] errors = (string[])host.GetProperty("Errors").GetValue(null, null);
            if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors));
            Assembly implementation = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MultiplayerExpansion.Module");
            if (ModLoader.Instance.LoadedMods.Count != 0) throw new InvalidOperationException("Bootstrap left native mod registrations behind.");
            return implementation;
        }
    }
}
