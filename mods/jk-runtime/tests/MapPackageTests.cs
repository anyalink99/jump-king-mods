using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing;
using JumpKing.Mods;
using JKRuntime;

internal static class MapPackageTests
{
    private static bool SkipAssets() { return false; }
    public static int Main(string[] args)
    {
        try {
            var runtime = Assembly.LoadFrom(args[0]);
            var host = runtime.GetType("JKRuntime.PackageHost", true);
            var discover = host.GetMethod("DiscoverMap", BindingFlags.Static | BindingFlags.NonPublic);
            var release = host.GetMethod("ReleaseMapPackages", BindingFlags.Static | BindingFlags.NonPublic);
            var modules = runtime.GetType("JKRuntime.RuntimeApi", true).GetMethod("GetModules");
            if (Directory.GetFiles(args[1], "*.dll").Length != 0) throw new Exception("Map has root DLLs");
            host.GetMethod("Discover").Invoke(null, null);
            if (((Array)modules.Invoke(null, null)).Length != 0) throw new Exception("Map loaded globally");
            for (int visit = 0; visit < 3; visit++) {
                discover.Invoke(null, new object[] { args[1] });
                discover.Invoke(null, new object[] { args[1] });
                var found = ((Array)modules.Invoke(null, null)).Cast<object>().ToArray();
                if (found.Length != 1 || (string)found[0].GetType().GetProperty("Id").GetValue(found[0], null) != "stereo-madness")
                    throw new Exception("Missing/duplicate map registration");
                if (ModLoader.Instance.LoadedMods.Count != 0) throw new Exception("Map changed native global mod registry");
                release.Invoke(null, null);
                release.Invoke(null, null);
                host.GetMethod("Discover").Invoke(null, null);
                if (((Array)modules.Invoke(null, null)).Length != 0) throw new Exception("Map registration leaked after exit");
            }
            var errors = (string[])host.GetProperty("Errors").GetValue(null, null);
            if (errors.Length != 0) throw new Exception(string.Join("\n", errors));
            Console.WriteLine("[OK] Embedded map discovery, idempotency, exit and re-entry without native mod registry changes");
            VerifyNativeMapSwitch(runtime, args[1], args[2], args[3]);
            return 0;
        } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void VerifyNativeMapSwitch(Assembly runtime, string map, string mappingPath, string harmonyPath)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        Assembly.LoadFrom(harmonyPath);
        var mapping = Assembly.LoadFrom(mappingPath);
        var attribute = mapping.GetTypes().Select(t => (JumpKingModAttribute)Attribute.GetCustomAttribute(t, typeof(JumpKingModAttribute))).Single(a => a != null);
        ModLoader.Instance.LoadedMods.Add(new ModAssembly(mapping, attribute));
        var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1)); GC.SuppressFinalize(game);
        typeof(Game1).GetField("_instance", flags).SetValue(null, game);
        typeof(Microsoft.Xna.Framework.Game).GetField("_services", flags).SetValue(game, new Microsoft.Xna.Framework.GameServiceContainer());
        game.contentManager = new JKContentManager();
        string empty = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "empty-world"); Directory.CreateDirectory(empty);
        game.contentManager.root = empty;
        var runtimeHost = runtime.GetType("JKRuntime.RuntimeHost", true);
        runtimeHost.GetMethod("BeforeLevelLoad", flags, null, new[] { typeof(string) }, null).Invoke(null, new object[] { empty });
        var getModules = runtime.GetType("JKRuntime.RuntimeApi", true).GetMethod("GetModules");
        Func<bool> hasController = () => ((Array)getModules.Invoke(null, null)).Cast<object>().Any(m => (string)m.GetType().GetProperty("Id").GetValue(m, null) == "stereo-madness");
        // Only disk/GPU asset loading is stubbed; execute the real patched load
        // boundary after the native setter, exactly as SetContentNode does.
        var patchesType=runtime.GetType("JKRuntime.OwnedPatches",true);
        var patches=(IDisposable)Activator.CreateInstance(patchesType,new object[]{"map-switch.fixture"});
        patchesType.GetMethod("Add").Invoke(patches,new object[]{typeof(JKContentManager).GetMethod("LoadAssets"),typeof(MapPackageTests).GetMethod("SkipAssets", flags),null,null,null,400});
        for (int visit = 0; visit < 2; visit++) {
            // Actual native method called by Workshop's SetContentNode. Do not
            // call Runtime discovery manually after this boundary.
            game.contentManager.SetLevel(map);
            game.contentManager.LoadAssets(game);
            if (!hasController()) throw new Exception("Native menu SetLevel did not discover the map controller");
            var implementation = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MegaMappingExpansion.Module");
            var state = implementation.GetType("MegaMappingExpansion.MappingState", true);
            if (state.GetProperty("Pending", flags).GetValue(null, null) == null) throw new Exception("MME preflight was not rerun for the selected map");
            var api = runtime.GetType("JKRuntime.RuntimeApi", true);
            var kernel = api.GetField("Kernel", flags).GetValue(null);
            var plan = (string[])kernel.GetType().GetMethod("Resolve", flags).Invoke(kernel, null);
            if (!plan.Contains("stereo-madness")) throw new Exception("Controller rejected with only Runtime and MME installed");
            Console.WriteLine("[OK] Map resolves with only its documented dependencies");
            VerifyOptionalIntegrations(kernel);
            game.contentManager.SetLevel(empty);
            game.contentManager.LoadAssets(game);
            if (hasController()) throw new Exception("Native menu switch retained the previous controller");
            if (state.GetProperty("Pending", flags).GetValue(null, null) != null) throw new Exception("MME retained previous map data");
        }
        runtimeHost.GetMethod("ExitWorld", flags).Invoke(null, null);
        patches.Dispose();
        Console.WriteLine("[OK] Native menu SetLevel reloads controller and MME preflight; exit/re-entry clears the old world");
    }
    private static void VerifyOptionalIntegrations(object kernel)
    {
        var resolve = kernel.GetType().GetMethod("Resolve", BindingFlags.Instance | BindingFlags.NonPublic);
        string[] optional = { "casual-jumping", "smooth-camera", "subframe-charge", "morph-ball" };
        for (int mask = 0; mask < 16; mask++) {
            var leases = new System.Collections.Generic.List<IDisposable>();
            try {
                for (int i = 0; i < optional.Length; i++) if ((mask & (1 << i)) != 0)
                    leases.Add(RuntimeApi.Register(new ModuleDefinition(optional[i], new Version(1, 0), delegate { })));
                var order = (string[])resolve.Invoke(kernel, null);
                int controller = Array.IndexOf(order, "stereo-madness");
                if (controller < 0 || Array.IndexOf(order, "mega-mapping-expansion") >= controller)
                    throw new Exception("Required MME dependency must precede the map controller");
                for (int i = 0; i < optional.Length; i++) if ((mask & (1 << i)) != 0) {
                    int index = Array.IndexOf(order, optional[i]);
                    if (index < 0 || (i == 3 ? index <= controller : index >= controller))
                        throw new Exception("Installed integration ordering changed: " + optional[i]);
                }
            } finally { foreach (var lease in leases) lease.Dispose(); }
        }
        Console.WriteLine("[OK] All 16 optional integration combinations preserve installed target order");
    }
}
