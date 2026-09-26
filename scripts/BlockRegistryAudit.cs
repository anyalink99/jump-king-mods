using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using JumpKing.API;
using JumpKing.Level.Factory;
using Microsoft.Xna.Framework;

// Offline only: never invoke mod entrypoints or GetBlock (which can mutate maps).
// Enumerate the actual accepted opaque RGB domain, including encoded ranges.
internal static class BlockRegistryAudit
{
    private static int Main(string[] args)
    {
        try
        {
            string game = args[0], workshop = args[1], output = args[2];
            string[] files = Directory.GetFiles(workshop, "*.dll", SearchOption.AllDirectories);
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs request) {
                string name = new AssemblyName(request.Name).Name + ".dll";
                string path = Path.Combine(game, name);
                if (!File.Exists(path)) path = files.FirstOrDefault(f => Path.GetFileName(f) == name);
                return path == null || !File.Exists(path) ? null : Assembly.LoadFrom(path);
            };
            var assemblies = new List<object>();
            var factories = new List<object>();
            Scan(typeof(BaseBlockFactory).Assembly, "vanilla", factories, true);
            foreach (string path in files.OrderBy(p => p))
            {
                string name = Path.GetFileName(path);
                if (name == "0Harmony.dll") continue;
                // First-party native discovery shells embed their implementation.
                Assembly assembly = Assembly.LoadFrom(path);
                byte[] hash;
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) hash = sha.ComputeHash(stream);
                string id = new DirectoryInfo(Path.GetDirectoryName(path)).Name;
                assemblies.Add(new { workshop = id, file = name, sha256 = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant() });
                Scan(assembly, id, factories, false);
                foreach (string resource in assembly.GetManifestResourceNames().Where(n => n == "JKRuntime.Module" || n.EndsWith(".Module.dll", StringComparison.OrdinalIgnoreCase)))
                    using (var stream = assembly.GetManifestResourceStream(resource))
                    using (var memory = new MemoryStream()) { stream.CopyTo(memory); Scan(Assembly.Load(memory.ToArray()), id, factories, false); }
            }
            var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            File.WriteAllText(output, json.Serialize(new { schema = 1, alpha = 255, assemblies = assemblies, factories = factories }));
            Console.WriteLine("[OK] Registry: " + factories.Count + " factories; " + assemblies.Count + " DLLs; " + output);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Scan(Assembly assembly, string owner, List<object> output, bool vanilla)
    {
        foreach (Type type in assembly.GetTypes().Where(t => !t.IsAbstract && typeof(IBlockFactory).IsAssignableFrom(t)))
        {
            if (vanilla && type != typeof(BaseBlockFactory)) continue;
            IBlockFactory factory = (IBlockFactory)Activator.CreateInstance(type, true);
            var ranges = new List<int[]>();
            int count = 0;
            for (int r = 0; r < 256; r++) for (int g = 0; g < 256; g++)
            {
                int start = -1;
                for (int b = 0; b <= 256; b++)
                {
                    bool accepted = b < 256 && factory.CanMakeBlock(new Color(r, g, b, 255), null);
                    if (accepted) { count++; if (start < 0) start = b; }
                    else if (start >= 0) { ranges.Add(new[] { r, g, start, b - 1 }); start = -1; }
                }
            }
            output.Add(new { owner = owner, assembly = assembly.GetName().Name, factory = type.FullName, count = count, rgb_ranges = ranges });
            Console.WriteLine("[OK] " + type.FullName + ": " + count + " RGB codes");
        }
    }
}
