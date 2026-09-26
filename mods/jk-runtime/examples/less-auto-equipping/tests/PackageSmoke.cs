using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JumpKing.Mods;

internal static class PackageSmoke
{
    public static int Main(string[] args)
    {
        try
        {
            var shell = Assembly.LoadFrom(args[1]);
            var entry = shell.GetTypes().Single(t => t.IsDefined(typeof(JumpKingModAttribute), false));
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "JKRuntime" || a.GetName().Name == "0Harmony"))
                throw new Exception("Native discovery loaded implementation dependencies");
            if (!entry.GetMethod("MainMenu").IsDefined(typeof(MainMenuItemSettingAttribute), false)
                || !entry.GetMethod("PauseMenu").IsDefined(typeof(PauseMenuItemSettingAttribute), false))
                throw new Exception("Both native menu locations must be exported");
            ModLoader.Instance.LoadedMods.Add(new ModAssembly(shell, (JumpKingModAttribute)Attribute.GetCustomAttribute(entry, typeof(JumpKingModAttribute))));
            var runtime = Assembly.LoadFrom(args[0]);
            var host = runtime.GetType("JKRuntime.PackageHost", true);
            host.GetMethod("Discover").Invoke(null, null);
            host.GetMethod("Discover").Invoke(null, null);
            var errors = (string[])host.GetProperty("Errors").GetValue(null, null);
            if (errors.Length != 0) throw new Exception(string.Join("\n", errors));
            var implementation = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "LessAutoEquipping.Module");
            if (implementation.Location != "") throw new Exception("Expected a byte-loaded implementation");
            var directory = (string)host.GetMethod("GetDataDirectory").Invoke(null, new object[] { implementation });
            if (Path.GetFullPath(directory) != Path.GetDirectoryName(Path.GetFullPath(args[1]))) throw new Exception("Data path must be beside the shell");
            // Only this freshly built fixture package is touched, never an installed mod.
            var path = Path.Combine(directory, "Zebra.LessAutoEquipping.Settings.xml");
            File.WriteAllText(path, "<Preferences><ShouldPreventAutoEquip>True</ShouldPreventAutoEquip></Preferences>");
            var migrated = implementation.GetType("LessAutoEquipping.ModEntry", true);
            var preferences = migrated.GetProperty("Preferences").GetValue(null, null);
            if (!(bool)preferences.GetType().GetProperty("ShouldPreventAutoEquip").GetValue(preferences, null)) throw new Exception("Packaged legacy setting was not found");
            foreach (string name in new[] { "MainMenu", "PauseMenu" })
                entry.GetMethod(name).Invoke(null, new object[] { null, default(JumpKing.PauseMenu.GuiFormat) });
            File.Delete(path); // Test-created fixture only; do not ship user preferences.
            Console.WriteLine("[OK] Dependency-free discovery, both exported menus, byte-loaded implementation and legacy data path");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
