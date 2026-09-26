using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Xml.Serialization;
using JKRuntime.Modules;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JKRuntime.Settings;
using JKRuntime.UI;

namespace SmoothCamera
{
    [RuntimeModule("smooth-camera", "Smooth Camera")]
    public static class ModEntry
    {
        private static bool worldPrepared;
        internal static bool ExternalClock { get { return JKRuntime.PresentationScheduling.IsActive("subframe-charge"); } }
        internal static bool ExternalInterpolation { get { return JKRuntime.PresentationScheduling.IsHighRefresh("subframe-charge"); } }
        // Preserved discovery entry point. Runtime now negotiates the shared clock.
        public static void ConnectExternalClock() { }
        [BeforeLevelLoad] public static void Prepare()
        {
            Settings.Load(); CameraControls.Register(); ConnectExternalClock();
        }
        [OnWorldReady] public static void PrepareWorld(JKRuntime.RuntimeScope scope)
        {
            worldPrepared = true;
            scope.Defer(delegate { worldPrepared = false; Unload(); });
        }
        [BeforeAttempt] public static void PrepareAttempt(JKRuntime.RuntimeScope scope)
        {
            scope.Defer(Unload);
            Settings.Load(); CameraControls.Register();
            if (Settings.Current.Smooth) Hooks.Install();
        }
        [OnLevelStart] public static void Start() { Settings.Load(); if (Settings.Current.Smooth) Hooks.Install(); Renderer.Start(); }
        [OnLevelEnd] public static void End() { Renderer.Stop(); }
        [OnLevelUnload] public static void Unload() { Renderer.Stop(); if (!worldPrepared) Hooks.Uninstall(); CameraControls.Unload(); }
        [MainMenuItemSetting] public static CameraOption MainMenu(object factory, GuiFormat format) { CameraControls.Register(); return new CameraOption(); }
        [PauseMenuItemSetting] public static CameraOption PauseMenu(object factory, GuiFormat format) { return MainMenu(factory, format); }
        [MainMenuItemSetting] public static SettingToggle MainHorizontal(object factory, GuiFormat format) { return new SettingToggle(Settings.HorizontalToggle); }
        [PauseMenuItemSetting] public static SettingToggle PauseHorizontal(object factory, GuiFormat format) { return MainHorizontal(factory, format); }
        [MainMenuItemSetting] public static SettingToggle MainRefresh(object factory, GuiFormat format) { return new SettingToggle(Settings.RefreshToggle); }
        [PauseMenuItemSetting] public static SettingToggle PauseRefresh(object factory, GuiFormat format) { return MainRefresh(factory, format); }
        [MainMenuItemSetting] public static TextButton MainFocus(object factory, GuiFormat format)
        {
            CameraControls.Register();
            return new TextButton("Bind Focus", UIApi.CreateMenuPage(factory,
                new UiBindingsPage("Bind Focus", "Tap to toggle. Hold for a temporary view.", CameraControls.FocusId)));
        }
        [PauseMenuItemSetting] public static TextButton PauseFocus(object factory, GuiFormat format) { return MainFocus(factory, format); }
    }

    [Serializable]
    public sealed class CameraSettings
    {
        public bool Smooth = true;
        public bool Horizontal = false;
        public bool HighRefresh = true;
        public List<FocusBinding> FocusBindings = new List<FocusBinding>();
    }

    internal static class Settings
    {
        private static bool loaded;
        private static JKRuntime.Settings.SettingsFile<CameraSettings> file;
        internal static CameraSettings Current = new CameraSettings();
        internal static readonly Setting<bool> Toggle = new Setting<bool>("smooth-camera.enabled", "Smooth Camera",
            delegate { Load(); return Current.Smooth; }, Set, ApplyPresentationSetting);
        internal static readonly Setting<bool> HorizontalToggle = new Setting<bool>("smooth-camera.horizontal", "Horizontal",
            delegate { Load(); return Current.Horizontal; }, SetHorizontal, Renderer.HorizontalChanged);
        internal static readonly Setting<bool> RefreshToggle = new Setting<bool>("smooth-camera.high-refresh", "240 Hz",
            delegate { Load(); return Current.HighRefresh; }, SetHighRefresh);
        private static string PathName { get { return Path.Combine(JKRuntime.PackageHost.GetDataDirectory(Assembly.GetExecutingAssembly()), "SmoothCamera.Settings.xml"); } }
        internal static void Load()
        {
            if (loaded) return;
            loaded = true;
            file = new JKRuntime.Settings.SettingsFile<CameraSettings>(PathName, delegate { return new CameraSettings(); });
            Current = file.Value;
        }
        internal static void Set(bool enabled)
        {
            Load();
            var next = new CameraSettings { Smooth = enabled, Horizontal = Current.Horizontal, HighRefresh = Current.HighRefresh, FocusBindings = Current.FocusBindings };
            file.Save(next);
            Current = next;
        }
        private static void SetHorizontal(bool enabled)
        {
            Load();
            var next = new CameraSettings { Smooth = Current.Smooth, Horizontal = enabled, HighRefresh = Current.HighRefresh, FocusBindings = Current.FocusBindings };
            file.Save(next); Current = next;
        }
        private static void SetHighRefresh(bool enabled)
        {
            Load();
            var next = new CameraSettings { Smooth = Current.Smooth, Horizontal = Current.Horizontal, HighRefresh = enabled, FocusBindings = Current.FocusBindings };
            file.Save(next); Current = next;
        }
        internal static CameraSettings WithFocus(CameraSettings current, string device, UiChord[] chords)
        {
            var next = new CameraSettings { Smooth = current.Smooth, Horizontal = current.Horizontal, HighRefresh = current.HighRefresh };
            foreach (var binding in current.FocusBindings ?? new List<FocusBinding>())
                if (binding != null && binding.Device != device) next.FocusBindings.Add(binding);
            var replacement = new FocusBinding { Device = device };
            foreach (var chord in chords ?? new UiChord[0])
                if (chord != null && !chord.IsEmpty) replacement.Chords.Add(new CameraChord { Buttons = chord.Buttons });
            next.FocusBindings.Add(replacement);
            return next;
        }
        internal static void SetFocus(string device, UiChord[] chords)
        {
            Load();
            var next = WithFocus(Current, device, chords);
            file.Save(next); Current = next;
        }
        internal static void ApplyPresentationSetting()
        {
            Renderer.SettingsChanged();
            if (Renderer.Running || Hooks.Installed)
            {
                if (Current.Smooth) Hooks.Install();
                else Hooks.Uninstall();
            }
        }
    }

    public sealed class CameraOption : SettingToggle
    {
        public CameraOption() : base(Settings.Toggle) { }
    }
}
