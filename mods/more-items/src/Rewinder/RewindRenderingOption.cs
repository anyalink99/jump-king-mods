using JumpKing;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.Util;
using Microsoft.Xna.Framework;

namespace MoreItems
{
    public sealed class RewindRenderingOption : IToggle, IMenuItem
    {
        private const string Label = "Render rewind";
        private const int Padding = 2;
        private Point labelSize;

        public RewindRenderingOption()
            : base(GetInitialValue())
        {
        }

        protected override void OnToggle()
        {
            SettingsStore.SetRenderRewind(toggle);
        }

        public override void Draw(int x, int y, bool selected)
        {
            JKRuntime.UI.UiTheme.DrawText(
                Game1.instance.contentManager.font.MenuFont,
                Label,
                new Vector2(x, y),
                JKRuntime.UI.UiTheme.Text);
            DrawCheckBox(
                new Vector2(
                    x + labelSize.X + Padding,
                    y + labelSize.Y / 2),
                toggle);
        }

        public override Point GetSize()
        {
            Vector2 measured = Game1.instance.contentManager.font.MenuFont
                .MeasureString(Label);
            labelSize = new Point((int)measured.X, (int)measured.Y);
            Point checkbox = GetCheckBoxSize();
            return new Point(
                labelSize.X + Padding + checkbox.X,
                System.Math.Max(labelSize.Y, checkbox.Y));
        }

        private static bool GetInitialValue()
        {
            SettingsStore.EnsureLoaded();
            return SettingsStore.Current.RenderRewind;
        }
    }
}
