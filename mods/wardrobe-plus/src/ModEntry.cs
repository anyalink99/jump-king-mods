using System.Linq;
using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.UI;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;

[assembly: System.Reflection.AssemblyTitle("Wardrobe+")]
[assembly: System.Reflection.AssemblyVersion("1.5.1.0")]

namespace WardrobePlus
{
    [RuntimeModule("wardrobe-plus", "Wardrobe+")]
    public static class ModEntry
    {
        [BeforeLevelLoad]
        public static void Before() { Controller.Ensure(); Controller.RefreshCatalog(); Menus.Sync(); }
        [OnWorldReady]
        public static void PrepareWorld(RuntimeScope scope)
        {
            scope.Defer(CrystalRenderer.Release);
            scope.Defer(CosmicRenderer.Release);
            if (Controller.Active != null && Controller.Active.HasCosmic)
                CosmicRenderer.Prepare(JumpKing.Game1.spriteBatch.GraphicsDevice);
            if (Controller.Active != null && Controller.Active.HasRefraction)
                CrystalRenderer.Prepare(JumpKing.Game1.spriteBatch.GraphicsDevice);
        }
        [OnLevelStart]
        public static void Start() { Controller.Ensure(); Menus.Sync(); }
        [OnLevelUnload]
        public static void Unload() { Controller.Release(); }
        [MainMenuItemSetting]
        public static TextButton MainMenu(object factory, GuiFormat format) { return Button(factory); }
        [PauseMenuItemSetting]
        public static TextButton Pause(object factory, GuiFormat format) { return Button(factory); }
        private static TextButton Button(object factory)
        { Controller.Ensure(); return new TextButton("Wardrobe+", UIApi.CreateMenuPage(factory, new WardrobePage())); }
    }
    internal static class Menus
    {
        private static readonly UiMainMenuItemDefinition Workshop = UiMainMenuItemDefinition.Page(
            "wardrobe-plus.workshop", "Wardrobe+", UiMainMenuPlacement.Workshop, 110, context => new WardrobePage());
        internal static void Sync()
        {
            // Registration changes rebuild native menu nodes. Only alter them when
            // necessary; live outfit edits must keep their currently running page.
            var main = UIApi.GetMainMenuItems();
            if (main.Any(item => item.Id == "wardrobe-plus.main")) UIApi.UnregisterMainMenuItem("wardrobe-plus.main");
            if (UIApi.GetPauseMenuItems().Any(item => item.Id == "wardrobe-plus.pause")) UIApi.UnregisterPauseMenuItem("wardrobe-plus.pause");
            if (!main.Any(item => ReferenceEquals(item, Workshop))) UIApi.RegisterMainMenuItem(Workshop);
            UIApi.UnregisterBinding("wardrobe-plus.open");
            UIApi.UnregisterInputAction("wardrobe-plus.open");
        }
    }
}
