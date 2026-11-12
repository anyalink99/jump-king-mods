using System;
using System.IO;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.Modules;
using JKRuntime.UI;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;

namespace Prism
{
    [RuntimeModule("prism", "Prism")]
    public static class ModEntry
    {
        private static RuntimeScope worldScope;
        private static World world;
        private static bool active, hooksInstalled;
        internal static ThemePreferences Prefs { get { return Settings.Current.Current; } }
        internal static bool Rendering { get { return active && world != null && Settings.Current.Enabled; } }
        internal static bool HideProps { get { return Rendering && Prefs.DisableProps; } }
        internal static World Current { get { return Rendering ? world : null; } }
        [BeforeLevelLoad] public static void BeforeLoad() { Settings.Load(); }
        [OnWorldReady] public static void Prepare(RuntimeScope scope)
        {
            worldScope = scope;
            scope.Defer(delegate { active = hooksInstalled = false; world = null; worldScope = null; });
            if (Settings.Current.Enabled) EnsureTheme(Settings.Current.SelectedTheme);
        }
        [BeforeAttempt] public static void Attempt(RuntimeScope scope)
        {
            if (world == null) return;
            using (RuntimeApi.MeasureStartup("prism.restart-voice")) world.Music.Restart();
            world.Wind.Reset();
        }
        internal static void InstallHooks(RuntimeScope scope) { NativeHooks.Install(scope); }
        private static World CreateWorld(string id) { return new World(Path.Combine(Settings.DirectoryPath, "themes", id), id); }
        private static void EnsureHooks()
        {
            if (hooksInstalled) return;
            var scope = new RuntimeScope();
            try { InstallHooks(scope); worldScope.Own(scope); hooksInstalled = true; }
            catch { scope.Dispose(); throw; }
        }
        private static void EnsureTheme(string id)
        {
            if (worldScope == null || (world != null && world.ThemeId == id)) return;
            var value = CreateWorld(id);
            try { EnsureHooks(); worldScope.Own(value); }
            catch { value.Dispose(); throw; }
            if (world != null) world.Dispose(); world = value;
        }
        internal static void SavePreferences(Preferences next)
        {
            // Decode a new selection before publishing it. Failed saves keep the active theme.
            World staged = null;
            if (next.Enabled && worldScope != null && (world == null || world.ThemeId != next.SelectedTheme)) staged = CreateWorld(next.SelectedTheme);
            try { if (staged != null) EnsureHooks(); Settings.File.Save(next); }
            catch { if (staged != null) staged.Dispose(); throw; }
            if (staged != null) { if (world != null) world.Dispose(); world = worldScope.Own(staged); }
            if (world == null) return;
            world.Music.Volume = next.Current.MusicVolume / 100f;
            if (active && next.Enabled) world.Music.Activate(NativePause.IsPaused);
            else world.Music.Deactivate();
        }
        [OnLevelStart] public static void Start(ModuleContext context)
        {
            var lifetime = context.Track(new RuntimeScope());
            lifetime.Defer(delegate { active = false; if (world != null) world.Music.Deactivate(); });
            active = true;
            context.Track(NativePause.Subscribe("prism", (paused, stamp) => { if (Rendering) world.Music.Pause(paused); }));
            if (Rendering) { world.Music.Volume = Prefs.MusicVolume / 100f; world.Music.Activate(NativePause.IsPaused); }
        }
        [MainMenuItemSetting] public static TextButton MainThemes(object factory, GuiFormat format)
        { return new TextButton("Themes", UIApi.CreateMenuPage(factory, new UiPageStack(pages => new ThemesPage(pages)))); }
        [PauseMenuItemSetting] public static TextButton PauseThemes(object factory, GuiFormat format) { return MainThemes(factory, format); }
    }
}
