using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;

namespace JKRuntime.UI
{
    [JumpKingMod("JK Runtime")]
    public static class ModEntry
    {
        // Main-menu input must exist before a run starts. Level services are
        // deliberately separate: they require a player and are disposed on exit.
        [BeforeLevelLoad]
        public static void InitializeMenuInput()
        {
            PointerDiagnostics.Start();
            PointerHooks.Install();
        }

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            JKRuntime.RuntimeHost.BeforeLevelLoad();
            SettingsStore.EnsureLoaded();
            DiagnosticModeOption.Apply();
            NativePerformance.Prepare();
            ModSettingsCatalog.Refresh(null, new GuiFormat(), false);
            BaseBindings.Register();
            AutomaticBindings.Refresh();
        }

        [OnLevelStart]
        public static void OnLevelStart()
        {
            JKRuntime.RuntimeHost.StartLevel();
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            JKRuntime.Gameplay.RunModifiers.Unload();
            JKRuntime.RuntimeHost.Stop();
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            JKRuntime.Gameplay.RunModifiers.Finish();
            try { JKRuntime.PackageHost.EndLevel(); }
            finally { JKRuntime.RuntimeHost.Stop(); }
        }

        public static SettingToggle MainOptimizations(object factory, GuiFormat format)
        { return new SettingToggle(NativePerformance.Setting); }
        public static SettingToggle PauseOptimizations(object factory, GuiFormat format)
        { return MainOptimizations(factory, format); }

        public static SettingToggle MainDiagnosticMode(object factory, GuiFormat format)
        { return new SettingToggle(DiagnosticModeOption.Setting); }
        public static SettingToggle PauseDiagnosticMode(object factory, GuiFormat format)
        { return MainDiagnosticMode(factory, format); }

        [MainMenuItemSetting]
        public static TextButton MainMenuRuntime(object factory, GuiFormat format)
        { return new TextButton("Runtime diagnostics", UIApi.CreateMenuPage(factory, new JKRuntime.RuntimePage())); }

        [PauseMenuItemSetting]
        public static TextButton PauseMenuRuntime(object factory, GuiFormat format)
        { return new TextButton("Runtime diagnostics", UIApi.CreateMenuPage(factory, new JKRuntime.RuntimePage())); }

        [MainMenuItemSetting]
        public static TextButton MainMenuSettings(object factory, GuiFormat format)
        {
            return new TextButton(
                "Settings",
                UiApiSettingsPageNode.Create(factory, format, false));
        }

        [MainMenuItemSetting]
        public static TextButton MainMenuPinnedSettings(object factory, GuiFormat format)
        {
            return new TextButton(
                "Pinned settings",
                PinnedSettingsPageNode.Create(factory, format, false));
        }

        [MainMenuItemSetting]
        public static TextButton MainMenuControls(object factory, GuiFormat format)
        {
            AutomaticBindings.Refresh();
            return new TextButton("Controls+", ControlsPageNode.Create(factory, format));
        }

        [MainMenuItemSetting]
        public static TextButton MainMenuDebugActions(object factory, GuiFormat format)
        {
            return new TextButton("ModsDebugActions", DebugActionsPageNode.Create(factory, format));
        }

        [PauseMenuItemSetting]
        public static TextButton PauseMenuSettings(object factory, GuiFormat format)
        {
            return new TextButton(
                "Settings",
                UiApiSettingsPageNode.Create(factory, format, true));
        }

        [PauseMenuItemSetting]
        public static TextButton PauseMenuPinnedSettings(object factory, GuiFormat format)
        {
            return new TextButton(
                "Pinned settings",
                PinnedSettingsPageNode.Create(factory, format, true));
        }

        [PauseMenuItemSetting]
        public static TextButton PauseMenuControls(object factory, GuiFormat format)
        {
            AutomaticBindings.Refresh();
            return new TextButton("Controls+", ControlsPageNode.Create(factory, format));
        }

        [PauseMenuItemSetting]
        public static TextButton PauseMenuDebugActions(object factory, GuiFormat format)
        {
            return new TextButton("ModsDebugActions", DebugActionsPageNode.Create(factory, format));
        }

    }

    internal static class UIApiInstaller
    {
        private static ModalHost modal;
        private static InteractionService interactions;

        internal static void Install()
        {
            Uninstall();
            PointerHooks.Install();
            modal = new ModalHost();
            interactions = new InteractionService();
            InventoryMenuIntegration.Install();
        }

        internal static void Uninstall()
        {
            UiPointer.Reset();
            InventoryMenuIntegration.Uninstall();
            if (interactions != null && interactions.IsAlive) interactions.Destroy();
            if (modal != null && modal.IsAlive) modal.Destroy();
            interactions = null;
            modal = null;
        }
    }
}
