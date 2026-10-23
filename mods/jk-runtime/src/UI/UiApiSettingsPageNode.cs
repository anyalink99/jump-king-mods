using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;

namespace JKRuntime.UI
{
    internal static class UiApiSettingsPageNode
    {
        internal static MenuSelector Create(object factory, GuiFormat format, bool isPause)
        {
            ModMenuIntegration.RegisterFactory(factory, format, isPause);
            MenuSelector settings = new MenuSelector(format);
            settings.AddChild(ModEntry.MainOptimizations(factory, format));
            settings.AddChild(ModEntry.MainDiagnosticMode(factory, format));
            settings.AddChild(new SettingToggle(Compatibility.ModCompatibility.Setting));
            settings.AddChild(new CompactWorkshopGridsOption());
            settings.AddChild(new CompactInventoryOption());
            settings.Initialize();
            return settings;
        }
    }

    internal static class PinnedSettingsPageNode
    {
        internal static PinnedSettingsBrowser Create(
            object factory,
            GuiFormat format,
            bool isPause)
        {
            ModMenuIntegration.RegisterFactory(factory, format, isPause);
            return new PinnedSettingsBrowser(ModSettingsCatalog.GetDescriptors());
        }
    }
}
