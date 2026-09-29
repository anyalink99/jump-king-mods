using JKRuntime.Modules;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JKRuntime.UI;

namespace SmoothCamera
{
    [RuntimeModule("smooth-camera", "Smooth Camera")]
    public static class ModEntry
    {
        private static bool worldPrepared;
        internal static bool ExternalClock { get { return JKRuntime.PresentationScheduling.IsActive("subframe-charge"); } }
        internal static bool ExternalInterpolation { get { return JKRuntime.PresentationScheduling.IsHighRefresh("subframe-charge"); } }
        public static void ConnectExternalClock() { }
        [BeforeLevelLoad] public static void Prepare() {
            Settings.Load(); CameraControls.Register();
            JKRuntime.Gameplay.MapMechanics.Register("smooth-camera", MapPolicy.MechanicId,
                delegate { Settings.DisableOnEntry(MapPolicy.Current.Key); });
        }
        [OnWorldReady] public static void PrepareWorld(JKRuntime.RuntimeScope scope)
        {
            worldPrepared = true;
            scope.Defer(delegate { worldPrepared = false; MapPolicy.Reset(); Unload(); });
        }
        [BeforeAttempt] public static void PrepareAttempt(JKRuntime.RuntimeScope scope)
        {
            scope.Defer(Unload);
            Settings.Load(); CameraControls.Register();
            using (JKRuntime.RuntimeApi.MeasureStartup("smooth-camera.map-rules")) MapPolicy.LoadLevel();
            if (MapPolicy.NeedsHooks) Hooks.Install(); else Hooks.Uninstall();
        }
        [OnLevelStart] public static void Start() { Settings.Load(); if (MapPolicy.NeedsHooks) Hooks.Install(); Renderer.Start(); }
        [OnLevelEnd] public static void End() { Renderer.Stop(); }
        [OnLevelUnload] public static void Unload() { Renderer.Stop(); if (!worldPrepared) Hooks.Uninstall(); CameraControls.Unload(); }
        [MainMenuItemSetting] public static CameraOption MainMenu(object factory, GuiFormat format) { CameraControls.Register(); return new CameraOption(); }
        [PauseMenuItemSetting] public static CameraOption PauseMenu(object factory, GuiFormat format) { return MainMenu(factory, format); }
        [MainMenuItemSetting] public static TextButton MainSettings(object factory, GuiFormat format)
        {
            CameraControls.Register();
            return new TextButton("Settings", UIApi.CreateMenuPage(factory, new UiPageStack(pages => new CameraSettingsPage(pages))));
        }
        [PauseMenuItemSetting] public static TextButton PauseSettings(object factory, GuiFormat format) { return MainSettings(factory, format); }
        // legacy factories remain callable, only Enable and Settings are menu entries
        public static SettingToggle MainHorizontal(object factory, GuiFormat format) { return new SettingToggle(Settings.HorizontalToggle); }
        public static SettingToggle PauseHorizontal(object factory, GuiFormat format) { return MainHorizontal(factory, format); }
        public static SettingToggle MainRefresh(object factory, GuiFormat format) { return new SettingToggle(Settings.RefreshToggle); }
        public static SettingToggle PauseRefresh(object factory, GuiFormat format) { return MainRefresh(factory, format); }
        public static TextButton MainFocus(object factory, GuiFormat format)
        { CameraControls.Register(); return new TextButton("Bind Focus", UIApi.CreateMenuPage(factory, new UiBindingsPage("Bind Focus", "Tap to toggle. Hold for a temporary view.", CameraControls.FocusId))); }
        public static TextButton PauseFocus(object factory, GuiFormat format) { return MainFocus(factory, format); }
    }
    public sealed class CameraOption : SettingToggle
    {
        public CameraOption() : base(Settings.Toggle, "Enable", delegate { return !MapPolicy.Current.DisableOnEnter && JKRuntime.Gameplay.MapMechanics.CanConfigure(MapPolicy.MechanicId); }) { }
    }
}
