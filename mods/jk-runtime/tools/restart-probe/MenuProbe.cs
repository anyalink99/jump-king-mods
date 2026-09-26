using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JumpKing;
using JumpKing.Mods;
using JumpKing.PauseMenu;

// One-shot menu construction benchmark in the fully initialized native game.
// Never starts/restarts an attempt or synthesizes gameplay input.
namespace JKMenuProbe
{
    [JumpKingMod("JK Menu Probe")]
    public static class Entry
    {
        private sealed class Owner : EntityComponent.Entity { internal Owner() : base(false) { } }
        private static readonly List<string> lines = new List<string>();
        private static bool measuring;
        private static string output;
        private static readonly Type Factory = typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.MenuFactory", true);
        [BeforeLevelLoad]
        public static void Install()
        {
            output = Path.Combine(Path.GetDirectoryName(typeof(Entry).Assembly.Location), "JKMenuProbe.txt");
            File.WriteAllText(output, "Menu probe armed; waiting for initialized native menus.\r\n");
            var harmony = new Harmony("jk-runtime.menu-probe");
            harmony.Patch(AccessTools.Method(Factory, "TryCreateModSetting"),
                new HarmonyMethod(typeof(Entry), "Begin"), new HarmonyMethod(typeof(Entry), "End"));
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                foreach (string spec in new[] { "JKRuntime.UI.AutomaticBindings|Discover", "JKRuntime.UI.ModSettingsCatalog|Refresh", "JKRuntime.PackageHost|Discover", "MegaGameplayExpansion.DashBindings|Register" })
                {
                    string[] parts = spec.Split('|'); var type = assembly.GetType(parts[0]);
                    if (type == null) continue;
                    harmony.Patch(AccessTools.Method(type, parts[1]), new HarmonyMethod(typeof(Entry), "Begin"), new HarmonyMethod(typeof(Entry), "DetailEnd"));
                }
            Game1.callbackManager.CreateRoutine(120, Run);
        }
        private static void Begin(out long __state) { __state = measuring ? Stopwatch.GetTimestamp() : 0; }
        private static void End(MethodInfo method, long __state)
        {
            if (__state == 0) return;
            double ms = (Stopwatch.GetTimestamp() - __state) * 1000.0 / Stopwatch.Frequency;
            lines.Add(ms.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms\t" + method.DeclaringType.FullName + "." + method.Name);
        }
        private static void DetailEnd(MethodBase __originalMethod, object[] __args, long __state)
        {
            if (__state == 0) return;
            double ms = (Stopwatch.GetTimestamp() - __state) * 1000.0 / Stopwatch.Frequency;
            if (ms < 0.1) return;
            string subject = __args.Length != 0 && __args[0] is ModAssembly ? " / " + ((ModAssembly)__args[0]).ModName : "";
            lines.Add("  " + ms.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms\t" + __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name + subject);
        }
        private static void Run()
        {
            try
            {
                // Use disposable, unregistered owners. Keep the real pause manager,
                // player, native behavior tree and save-manager state untouched.
                for (int i = 0; i < 4; i++)
                {
                    var owner = new Owner();
                    try
                    {
                        var factory = Activator.CreateInstance(Factory, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { owner }, null);
                        var format = new GuiFormat();
                        format.anchor_bounds = new Microsoft.Xna.Framework.Rectangle(0, 0, 480, 360);
                        format.element_margin = 8; format.all_padding = 16;
                        lines.Add("Pass " + (i + 1));
                        measuring = true;
                        var timer = Stopwatch.StartNew();
                        AccessTools.Method(Factory, "CreateOptionsMenu").Invoke(factory, new object[] { format, format, format, true });
                        timer.Stop();
                        lines.Add("Options total: " + timer.Elapsed.TotalMilliseconds.ToString("F3") + " ms");
                        timer.Restart();
                        AccessTools.Method(Factory, "CreateInventory").Invoke(factory, new object[] { format, format, format, null });
                        timer.Stop();
                        lines.Add("Inventory total: " + timer.Elapsed.TotalMilliseconds.ToString("F3") + " ms");
                    }
                    finally { measuring = false; owner.Destroy(); }
                }
            }
            catch (Exception error) { lines.Add("FAILED: " + error); }
            finally
            {
                File.WriteAllLines(output, lines.ToArray());
                Game1.instance.Exit();
            }
        }
    }
}
