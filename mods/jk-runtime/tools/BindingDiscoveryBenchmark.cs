using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JumpKing.Mods;

// Read-only traversal benchmark. Suppress every foreign container getter so
// loading metadata cannot initialize mods or write their settings/save files.
internal static class BindingDiscoveryBenchmark
{
    private static readonly Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static bool MetadataOnly(Type containerType, ref bool __result)
    {
        containerType.GetProperty("KeyBindings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        __result = false;
        return false;
    }
    private static Type[] Types(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException error) { return error.Types.Where(t => t != null).ToArray(); }
    }
    private static void Main(string[] args)
    {
        var files = Directory.GetFiles(args[0], "*.dll", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(Path.Combine(args[0], "Content/JKMods"), "*.dll", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(args[1], "*.dll", SearchOption.AllDirectories)).ToArray();
        foreach (string file in files)
        {
            try { paths[AssemblyName.GetAssemblyName(file).Name] = file; } catch (BadImageFormatException) { }
        }
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs eventArgs)
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == eventArgs.Name);
            if (loaded != null) return loaded;
            string file;
            return paths.TryGetValue(new AssemblyName(eventArgs.Name).Name, out file) ? Assembly.LoadFrom(file) : null;
        };
        var runtime = Assembly.LoadFrom(args[2]);
        foreach (string file in files)
        {
            if (Path.GetFileName(file) == "JKRuntime.dll") continue;
            Assembly assembly;
            try
            {
                string identity = AssemblyName.GetAssemblyName(file).FullName;
                assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == identity) ?? Assembly.LoadFrom(file);
            }
            catch (BadImageFormatException) { continue; }
            var attribute = Types(assembly).SelectMany(t => CustomAttributeData.GetCustomAttributes(t))
                .FirstOrDefault(a => a.Constructor.DeclaringType == typeof(JumpKingModAttribute));
            if (attribute == null) continue;
            try { ModLoader.Instance.LoadedMods.Add(new ModAssembly(assembly, new JumpKingModAttribute((string)attribute.ConstructorArguments[0].Value))); }
            catch (ReflectionTypeLoadException error)
            {
                Console.WriteLine("Excluded unloadable metadata: " + assembly.GetName().Name + ": " + error.LoaderExceptions[0].Message);
            }
        }
        var automatic = runtime.GetType("JKRuntime.UI.AutomaticBindings", true);
        new Harmony("binding.discovery.benchmark").Patch(AccessTools.Method(automatic, "TryRegisterContainer"),
            prefix: new HarmonyMethod(typeof(BindingDiscoveryBenchmark), "MetadataOnly"));
        var refresh = AccessTools.Method(automatic, "Refresh");
        for (int i = 0; i < 3; i++) refresh.Invoke(null, null);
        int gc = GC.CollectionCount(0);
        var samples = new List<double>();
        for (int i = 0; i < 20; i++)
        {
            var timer = Stopwatch.StartNew(); refresh.Invoke(null, null); timer.Stop();
            samples.Add(timer.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        Console.WriteLine("Runtime: " + FileVersionInfo.GetVersionInfo(args[2]).FileVersion + "; mods: " + ModLoader.Instance.LoadedMods.Count);
        Console.WriteLine("20 warm metadata-only binding scans; median_ms=" + samples[10].ToString("F3")
            + "; min_ms=" + samples[0].ToString("F3") + "; max_ms=" + samples[19].ToString("F3")
            + "; gen0_collections=" + (GC.CollectionCount(0) - gc));
        Console.WriteLine("Foreign settings getters were not executed. This is not an end-to-end game measurement.");
        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "MonoGame.Framework" || a.GetName().Name == "JumpKing" || a.GetName().Name == "JKRuntime"))
            Console.WriteLine("Loaded: " + loaded.FullName + " at " + loaded.Location);
        var catalog = runtime.GetType("JKRuntime.UI.ModSettingsCatalog", true);
        var catalogRefresh = AccessTools.Method(catalog, "Refresh");
        object[] catalogArgs = { null, new JumpKing.PauseMenu.GuiFormat(), false };
        for (int i = 0; i < 3; i++) catalogRefresh.Invoke(null, catalogArgs);
        samples.Clear(); gc = GC.CollectionCount(0);
        for (int i = 0; i < 20; i++)
        {
            var timer = Stopwatch.StartNew(); catalogRefresh.Invoke(null, catalogArgs); timer.Stop();
            samples.Add(timer.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        Console.WriteLine("20 warm settings-catalog refreshes; median_ms=" + samples[10].ToString("F3")
            + "; min_ms=" + samples[0].ToString("F3") + "; max_ms=" + samples[19].ToString("F3")
            + "; gen0_collections=" + (GC.CollectionCount(0) - gc));
    }
}
