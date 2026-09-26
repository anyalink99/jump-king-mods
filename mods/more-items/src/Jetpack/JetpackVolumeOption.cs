using JumpKing;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using MoreItems;

namespace JumpKingJetpack
{
    public sealed class JetpackVolumeOption : ISlider
    {
        private const string Label = "Jetpack volume";
        private const int Padding = 8;

        public JetpackVolumeOption()
            : base(GetInitialVolume())
        {
        }

        protected override void OnSliderChange(float value)
        {
            SettingsStore.SetJetpackVolume(value);
            JetpackInstaller.RefreshSettings();
        }

        protected override void IconDraw(
            float value,
            int x,
            int y,
            out int newX)
        {
            JKRuntime.UI.UiTheme.DrawText(
                Game1.instance.contentManager.font.MenuFont,
                Label,
                new Vector2(x, y - 4),
                JKRuntime.UI.UiTheme.Text);
            newX = x + LabelSize().X + Padding;
        }

        public override Point GetSize()
        {
            Point size = base.GetSize();
            Point label = LabelSize();
            size.X += label.X + Padding;
            size.Y = System.Math.Max(size.Y, label.Y);
            return size;
        }

        private static float GetInitialVolume()
        {
            SettingsStore.EnsureLoaded();
            return SettingsStore.Current.JetpackVolume;
        }

        private static Point LabelSize()
        {
            Vector2 size = Game1.instance.contentManager.font.MenuFont
                .MeasureString(Label);
            return new Point((int)size.X, (int)size.Y);
        }

    }
}
