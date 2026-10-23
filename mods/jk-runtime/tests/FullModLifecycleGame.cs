using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BehaviorTree;
using HarmonyLib;
using JumpKing;
using JumpKing.Controller;
using JumpKing.GameManager;
using JumpKing.GameManager.TitleScreen;
using JumpKing.Mods;
using JumpKing.PauseMenu.BT;
using Microsoft.Xna.Framework;
using Steamworks;

// real menu/intro/game transitions in the existing isolated full-mod fixture
internal static class FullModLifecycleGame
{
    private static readonly Harmony hooks = new Harmony("niotid.runtime-lifecycle-check");
    private static readonly Stopwatch clock = Stopwatch.StartNew();
    private static readonly List<string> report = new List<string>();
    private static readonly List<double> gaps = new List<double>(), updates = new List<double>();
    private static string root, profile;
    private static bool failed, finished, capturing, menuReady;
    private static int started, completed, menuFrames, gameplayFrames, inventoryScans;
    private static long introStart, handoff, updateStart, previousDraw;
    private static long entryStart;
    private static double entryToFirstDraw;
    private static int gen0, gen1, gen2;
    private static object factory;
    private static MenuSelector menu;
    private static BTmanager selection;
    private static Type runtime, pause;
    private static int wanted = 4;
    private static Vector2 resumePosition;
    private static int initialResources;
    private static string mode = "InputsAndPhysics";
    private static bool JumpHeld { get { return wanted == 5 && capturing && gameplayFrames >= 30 && gameplayFrames < 45; } }
    private static string compilationFile;
    private static DateTime compilationTimestamp;
    private static BrokenLog brokenLog;
    private sealed class BrokenLog : StreamWriter
    {
        internal int Writes;
        internal BrokenLog() : base(Stream.Null) { }
        public override void WriteLine(string value)
        { Writes++; throw new IOException("fixture: Harmony log filesystem limitation", unchecked((int)0x80070299)); }
    }
    private static Type Find(string name)
    { return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null); }
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static void Record(string value)
    { report.Add(value); Console.WriteLine(value); File.WriteAllLines(Path.Combine(root, "lifecycle-game.txt"), report); }
    private static double Milliseconds(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
    private static string Number(double value) { return value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture); }
    private static void Patch(Type type, string method, string prefix, string postfix, int priority = 400)
    {
        hooks.Patch(AccessTools.Method(type, method),
            prefix: prefix == null ? null : new HarmonyMethod(typeof(FullModLifecycleGame), prefix) { priority = priority },
            postfix: postfix == null ? null : new HarmonyMethod(typeof(FullModLifecycleGame), postfix) { priority = 0 });
    }
    [STAThread] private static int Main()
    {
        root = AppDomain.CurrentDomain.BaseDirectory;
        profile = Environment.GetEnvironmentVariable("JK_LIFECYCLE_PROFILE") ?? "on";
        string count = Environment.GetEnvironmentVariable("JK_LIFECYCLE_ATTEMPTS");
        if (count != null) wanted = int.Parse(count);
        try {
            Check(root.IndexOf("build", StringComparison.OrdinalIgnoreCase) >= 0 && Directory.Exists(Path.Combine(root, "WorkshopMods")), "Use an isolated staged game");
            Environment.SetEnvironmentVariable("SteamAppId", "1061090");
            Environment.SetEnvironmentVariable("SteamGameId", "1061090");
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args) {
                string name = new AssemblyName(args.Name).Name;
                if (name.EndsWith(".resources") || name.EndsWith(".Module")) return null;
                var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
                if (loaded != null) return loaded;
                string[] files = Directory.GetFiles(Path.Combine(root, "WorkshopMods"), name + ".dll", SearchOption.AllDirectories);
                return files.Length == 1 ? Assembly.LoadFrom(files[0]) : null;
            };
            Check(SteamAPI.Init(), "Steam initialization failed");
            Patch(typeof(Microsoft.Xna.Framework.Audio.SoundEffect), "set_MasterVolume", "Mute", null);
            Patch(typeof(Microsoft.Xna.Framework.Media.MediaPlayer), "set_Volume", "Mute", null);
            Microsoft.Xna.Framework.Audio.SoundEffect.MasterVolume = 0;
            Microsoft.Xna.Framework.Media.MediaPlayer.Volume = 0;
            hooks.Patch(AccessTools.Method(typeof(System.Windows.Forms.MessageBox), "Show", new[] { typeof(string), typeof(string),
                typeof(System.Windows.Forms.MessageBoxButtons), typeof(System.Windows.Forms.MessageBoxIcon) }),
                prefix: new HarmonyMethod(typeof(FullModLifecycleGame), "CrashDialog"));
            Patch(typeof(JumpKing.Program), "InitSteam", "SteamReady", null);
            Patch(typeof(ModLoader), "LoadMods", "LoadMods", null);
            foreach (string method in new[] { "GetModAssemblies", "LoadReferencedAssemblies" })
                hooks.Patch(AccessTools.Method(typeof(ModLoader), method), transpiler: new HarmonyMethod(typeof(FullModLifecycleGame), "SharedAssemblies"));
            Patch(typeof(SteamUGC), "GetItemInstallInfo", null, "WorkshopPath");
            foreach (var method in typeof(SteamUserStats).GetMethods().Where(m => m.ReturnType == typeof(bool) &&
                (m.Name.StartsWith("Set") || m.Name.StartsWith("Clear") || m.Name == "StoreStats" || m.Name == "ResetAllStats" || m.Name == "IndicateAchievementProgress" || m.Name == "UpdateAvgRateStat")))
                hooks.Patch(method, prefix: new HarmonyMethod(typeof(FullModLifecycleGame), "NoSteamWrite"));
            Patch(typeof(Game), "get_IsActive", "Active", null);
            Patch(typeof(Game1), "Update", "BeforeUpdate", "Updated", int.MaxValue);
            Patch(typeof(Game1), "Draw", "BeforeDraw", "Drawn", int.MaxValue);
            Patch(typeof(GameTitleScreen), "CreateMenu", null, "MenuCreated");
            Patch(typeof(IntroState), "OnNewRun", "Intro", null, int.MaxValue);
            Patch(typeof(GameLoop), "OnNewRun", "Handoff", "PlayerCreated", int.MaxValue);
            Patch(typeof(PadInstance), "GetPadState", "Pad", null);
            hooks.Patch(AccessTools.Method(typeof(Microsoft.Xna.Framework.Input.Keyboard), "GetState", Type.EmptyTypes), prefix: new HarmonyMethod(typeof(FullModLifecycleGame), "EmptyKeyboard"));
            hooks.Patch(AccessTools.Method(typeof(Microsoft.Xna.Framework.Input.Mouse), "GetState", Type.EmptyTypes), prefix: new HarmonyMethod(typeof(FullModLifecycleGame), "EmptyMouse"));
            var prefs = typeof(Game1).Assembly.GetType("JumpKing.PlayerPreferences.Persocom.GraphicsPreferencesRuntime", true);
            Patch(prefs, "InitAllPrefs", "Windowed", null); Patch(prefs, "OnSetPrefs", "Windowed", null);
            pause = typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.PauseManager", true);
            JumpKing.Program.Run();
            Check(finished && !failed && completed == wanted, "Game exited before completing lifecycle checks");
            if (Environment.GetEnvironmentVariable("JK_LIFECYCLE_FAIL_HARMONY_LOG") == "1") {
                Check(brokenLog != null && brokenLog.Writes == 1, "The global guard didn't pause the broken writer");
                Check(AccessTools.Field(Find("JKRuntime.HarmonyLogScope"), "LastError").GetValue(null) != null, "The logging failure wasn't recorded");
                Record("PASS automatic Harmony log recovery across the full mod lifecycle");
            }
            return 0;
        } catch (Exception error) { Record("FAIL " + error); return 1; }
        finally { SteamAPI.Shutdown(); }
    }
    private static bool SteamReady(ref bool __result) { PadInstance.RegisterSteamCallback(); __result = true; return false; }
    private static bool NoSteamWrite(ref bool __result) { __result = false; return false; }
    private static void Mute(ref float __0) { __0 = 0; }
    private static bool CrashDialog(string __0, ref System.Windows.Forms.DialogResult __result)
    { failed = true; Record("FAIL native game: " + __0); __result = System.Windows.Forms.DialogResult.OK; return false; }
    private static bool EmptyKeyboard(ref Microsoft.Xna.Framework.Input.KeyboardState __result) { __result = default(Microsoft.Xna.Framework.Input.KeyboardState); return false; }
    private static bool EmptyMouse(ref Microsoft.Xna.Framework.Input.MouseState __result) { __result = default(Microsoft.Xna.Framework.Input.MouseState); return false; }
    private static bool Active(ref bool __result) { __result = true; return false; }
    private static void Windowed(ref JumpKing.PlayerPreferences.Persocom.GraphicPrefs p_settings)
    { p_settings.screen_mode = JumpKing.PlayerPreferences.Persocom.ScreenMode.Windowed; p_settings.size_mode = JumpKing.PlayerPreferences.Persocom.SizeMode.x1; }
    private static bool LoadMods(string modDirectory, ref List<ModAssembly> loadedModAssemblies)
    {
        if (Path.GetFullPath(modDirectory).StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
        loadedModAssemblies = new List<ModAssembly>(); return false;
    }
    private static void WorkshopPath(PublishedFileId_t nPublishedFileID, ref string pchFolder, bool __result)
    { string path = Path.Combine(root, "WorkshopMods", nPublishedFileID.m_PublishedFileId.ToString()); if (__result && Directory.Exists(path)) pchFolder = path; }
    private static Assembly Shared(string path)
    {
        string identity = AssemblyName.GetAssemblyName(path).FullName;
        var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == identity);
        if (loaded != null) return loaded;
        var assembly = Assembly.LoadFrom(path);
        if (assembly.GetName().Name == "JKRuntime") {
            runtime = assembly.GetType("JKRuntime.RuntimeApi", true);
            var compiler = assembly.GetType("JKRuntime.StartupCompilation");
            if (compiler == null) {
                compilationFile = Path.Combine(Path.GetDirectoryName(assembly.Location), "JKRuntime.StartupCache", "first-entry.profile");
                compilationTimestamp = File.Exists(compilationFile) ? File.GetLastWriteTimeUtc(compilationFile) : DateTime.MinValue;
            }
            if (profile != "on" && compiler != null) Patch(compiler, "Start", "Skip", null);
            var images = assembly.GetType("JKRuntime.PackageImages");
            if (profile == "bytes" && images != null) Patch(images, "Load", "LoadBytes", null);
            Patch(assembly.GetType("JKRuntime.World.WorldObservation", true), "Prepare", "Inventory", null);
            hooks.Patch(AccessTools.Constructor(assembly.GetType("JKRuntime.Input.KeyboardSource", true), new[] { typeof(Func<int, short>), typeof(Func<IntPtr>), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(FullModLifecycleGame), "Keyboard"));
            // fail after harness setup, but before the native loader constructs Runtime's marker
            if (Environment.GetEnvironmentVariable("JK_LIFECYCLE_FAIL_HARMONY_LOG") == "1") {
                brokenLog = new BrokenLog(); FileLog.LogWriter = brokenLog; Harmony.DEBUG = true;
                Environment.SetEnvironmentVariable("HARMONY_DEBUG", "1");
            }
        }
        return assembly;
    }
    private static void Keyboard(ref Func<int, short> read, ref Func<IntPtr> focus, bool start)
    { if (start) { read = key => (short)(key == 32 && JumpHeld ? -32768 : 0); focus = () => Game1.instance == null ? IntPtr.Zero : Game1.instance.Window.Handle; } }
    private static bool Skip() { return false; }
    private static bool LoadBytes(byte[] bytes, ref Assembly __result) { __result = Assembly.Load(bytes); return false; }
    private static void Inventory() { inventoryScans++; }
    private static IEnumerable<CodeInstruction> SharedAssemblies(IEnumerable<CodeInstruction> source)
    {
        var original = typeof(Assembly).GetMethod("LoadFrom", new[] { typeof(string) });
        foreach (var instruction in source) {
            if (instruction.Calls(original)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(FullModLifecycleGame), "Shared"); }
            yield return instruction;
        }
    }
    private static bool Pad(ref PadState __result)
    { __result = new PadState { jump = JumpHeld || (menu == null || !menu.IsRunning()) && !capturing && selection == null && (clock.ElapsedMilliseconds / 300) % 2 == 0 }; return false; }
    private static void MenuCreated(GameTitleScreen __instance)
    {
        factory = AccessTools.Field(typeof(GameTitleScreen), "m_menu_factory").GetValue(__instance);
        menu = (MenuSelector)AccessTools.Method(Find("JKRuntime.UI.VanillaMenuAdapter"), "FindRootMainMenu").Invoke(null, new[] { factory });
        Check(menu != null && menu.GetBounds().Top >= 100, "Main menu layout moved");
        if (completed == 4) {
            var save = (JumpKing.SaveThread.SaveState)AccessTools.Property(typeof(Game1).Assembly.GetType("JumpKing.SaveThread.SaveLube"), "PlayerPosition").GetValue(null, null);
            resumePosition = save.position;
            Check(!JumpKing.SaveThread.SaveManager.instance.IsNewGame && menu.Children.OfType<TextButton>().Any(b => b.Text == LanguageJK.language.GAMETITLESCREEN_CONTINUE),
                "Continue/stats visibility lost a saved attempt");
        }
        Record("MENU " + Game1.instance.contentManager.root + " bounds=" + menu.GetBounds());
        menuReady = true; menuFrames = 0;
    }
    private static void Intro() { introStart = Stopwatch.GetTimestamp(); gen0 = GC.CollectionCount(0); gen1 = GC.CollectionCount(1); gen2 = GC.CollectionCount(2); }
    private static void Handoff() { handoff = Stopwatch.GetTimestamp(); capturing = true; gaps.Clear(); updates.Clear(); gameplayFrames = 0; started++; }
    private static void PlayerCreated()
    {
        var expected = started == 5 ? resumePosition : JumpKing.SaveThread.SaveState.GetDefault().position;
        Check(Vector2.Distance(GameLoop.m_player.m_body.Position, expected) < 1, "New game inherited another map's coordinates");
    }
    private static void BeforeUpdate() { updateStart = Stopwatch.GetTimestamp(); }
    private static void Updated(Game1 __instance)
    {
        if (capturing) updates.Add(Milliseconds(Stopwatch.GetTimestamp() - updateStart));
        if (finished) return;
        try {
            Check(clock.Elapsed.TotalSeconds < 180, "Lifecycle game timed out");
            if (failed) { finished = true; __instance.Exit(); return; }
            if (selection != null) { if (selection.Run(1f / 60f) == BTresult.Success) selection = null; return; }
            if (!menuReady || !menu.IsRunning() || ++menuFrames < 90 || clock.Elapsed.TotalSeconds < 9 || started > completed) return;
            if (completed == 4) {
                var resume = menu.Children.OfType<TextButton>().Single(b => b.Text == LanguageJK.language.GAMETITLESCREEN_CONTINUE);
                entryStart = Stopwatch.GetTimestamp();
                selection = new BTmanager(resume.GetRelatedNodes()[0]); menuReady = false; return;
            }
            if (wanted == 5) {
                mode = completed == 1 ? "AllInputs" : completed == 2 ? "Charge" : "InputsAndPhysics";
                object setting = AccessTools.Field(Find("SubframeCharge.Options"), "Mode").GetValue(null);
                var type = Find("SubframeCharge.SubframeMode");
                setting.GetType().GetMethod("Set").Invoke(setting, new[] { Enum.Parse(type, mode) });
            }
            // use the native new-game confirmation and the live menu's completion node
            entryStart = Stopwatch.GetTimestamp();
            var content = new BTsequencor();
            if (completed == 2) content.AddChild(new JumpKing.Workshop.Nodes.SetContentNode(new JumpKing.Workshop.Level(
                Path.Combine(root, "WorkshopMods", "3820038901"), new SteamUGCDetails_t { m_nPublishedFileId = new PublishedFileId_t(3820038901) })));
            else content.AddChild(new JumpKing.Workshop.Nodes.SetContentNode("New Game"));
            var after = new BTsequencor();
            after.AddChild(new SetTitleScreenResultNode(TitleScreenResult.StartNormalGame));
            after.AddChild(new SetMenuResult(menu, BTresult.Success));
            var format = AccessTools.Field(pause, "GUI_FORMAT").GetValue(null);
            var button = (IBTnode)AccessTools.Method(factory.GetType(), "CreateNewGameButton").Invoke(factory,
                new object[] { "Lifecycle", content, after, Color.White, format });
            var node = button.GetRelatedNodes()[0];
            var popup = node as MenuSelector;
            if (popup != null) node = popup.Children.OfType<TextButton>().Last().GetRelatedNodes()[0];
            selection = new BTmanager(node); menuReady = false;
        } catch (Exception error) { failed = finished = true; Record("FAIL " + error); __instance.Exit(); }
    }
    private static void BeforeDraw()
    {
        long now = Stopwatch.GetTimestamp();
        if (capturing && gaps.Count == 0) entryToFirstDraw = Milliseconds(now - entryStart);
        if (capturing && previousDraw != 0) gaps.Add(Milliseconds(now - previousDraw));
        previousDraw = now;
    }
    private static void Drawn(Game1 __instance)
    {
        if (!capturing || ++gameplayFrames < 120) return;
        capturing = false; completed++;
        Check((string)AccessTools.Property(runtime, "State").GetValue(null, null) == "active", "Runtime didn't activate all SDK modules");
        string prefix = "attempt-" + completed;
        File.WriteAllLines(Path.Combine(root, prefix + "-frames.csv"), new[] { "gap_ms,update_ms" }.Concat(
            Enumerable.Range(0, Math.Max(gaps.Count, updates.Count)).Select(i =>
                (i < gaps.Count ? Number(gaps[i]) : "") + "," + (i < updates.Count ? Number(updates[i]) : ""))));
        int resources = ((Array)AccessTools.Method(Find("JKRuntime.RuntimeResources"), "Inspect").Invoke(null, null)).Length;
        if (completed == 1) initialResources = resources;
        if (completed == 2 && wanted != 5 || completed == 4 || completed == 5) Check(resources == initialResources, "Same-world activation retained extra resources");
        if (wanted == 5) {
            bool physical = (bool)AccessTools.Property(Find("SubframeCharge.SubframePhysics"), "Active").GetValue(null, null);
            Check(physical == (mode == "InputsAndPhysics"), "SFC mode didn't activate its selected physics cadence");
            if (mode == "Charge") Check(!(bool)AccessTools.Property(Find("SubframeCharge.PerformanceFeatures"), "RefreshRequested").GetValue(null, null),
                "Charge retained Smooth Rendering");
        }
        var compiler = Find("JKRuntime.StartupCompilation");
        string compilation = compiler == null ? "removed" : (string)AccessTools.Property(compiler, "Status").GetValue(null, null);
        if (compiler == null) {
            Check(inventoryScans == 0, "Gameplay built an unrequested IL inventory");
            Check((File.Exists(compilationFile) ? File.GetLastWriteTimeUtc(compilationFile) : DateTime.MinValue) == compilationTimestamp,
                "Gameplay trained or rewrote a compilation profile");
        }
        Record("MEASURE attempt=" + completed + " profile=" + profile + " mode=" + mode + " mods=" + ModLoader.Instance.LoadedMods.Count
            + " intro_to_handoff_ms=" + Number(Milliseconds(handoff - introStart)) + " first_gap_ms=" + Number(gaps[0])
            + " entry_to_first_draw_ms=" + Number(entryToFirstDraw)
            + " max_gap_ms=" + Number(gaps.Max()) + " max_update_ms=" + Number(updates.Max())
            + " gc0=" + (GC.CollectionCount(0) - gen0) + " gc1=" + (GC.CollectionCount(1) - gen1) + " gc2=" + (GC.CollectionCount(2) - gen2)
            + " scans=" + inventoryScans + " resources=" + resources
            + " patched=" + Harmony.GetAllPatchedMethods().Count() + " compilation=" + compilation);
        if (completed == wanted) { finished = true; Record("PASS native menus, intro, first 120 frames and new-game transitions"); __instance.Exit(); }
        else {
            if (completed == 4) GameLoop.m_player.m_body.Position = new Vector2(220, 250);
            object current = AccessTools.Field(pause, "instance").GetValue(null);
            AccessTools.Field(pause, "_exit_to_menu").SetValue(current, true);
        }
    }
}
