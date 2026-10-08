using BehaviorTree;
using JumpKing;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.Util;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    public sealed class CompactWorkshopGridsOption : IToggle, IMenuItem
    {
        private const string Label = "Compact Workshop grids";
        private Point labelSize;

        public CompactWorkshopGridsOption()
            : base(GetValue())
        {
        }

        protected override void OnToggle()
        {
            SettingsStore.EnsureLoaded();
            SettingsStore.Current.UseCompactWorkshopGrids = toggle;
            SettingsStore.Save();
            ModMenuIntegration.Refresh();
        }

        public override void Draw(int x, int y, bool selected)
        {
            UiTheme.DrawText(
                Game1.instance.contentManager.font.MenuFont,
                Label,
                new Vector2(x, y),
                UiTheme.Text);
            DrawCheckBox(new Vector2(x + labelSize.X + 2, y + labelSize.Y / 2), toggle);
        }

        public override Point GetSize()
        {
            labelSize = Game1.instance.contentManager.font.MenuFont.MeasureString(Label).ToPoint();
            Point size = labelSize;
            size.X += GetCheckBoxSize().X + 2;
            return size;
        }

        private static bool GetValue()
        {
            SettingsStore.EnsureLoaded();
            return SettingsStore.Current.UseCompactWorkshopGrids;
        }
    }
}
