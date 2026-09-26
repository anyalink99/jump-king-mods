using JKRuntime.Settings;
using JumpKing;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.Util;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    // Native checkbox projection of the same setting used by controls/commands.
    public class SettingToggle : IToggle, IMenuItem
    {
        private readonly Setting<bool> setting;
        private readonly string label;
        private readonly System.Func<bool> available;
        private Point labelSize;
        public SettingToggle(Setting<bool> value, string text = null) : base(value.Value)
        { setting = value; label = text ?? value.Label; }
        /// <summary>Native dependent checkbox. The condition is checked on activation and draw; unavailable options are grey.</summary>
        public SettingToggle(Setting<bool> value, string text, System.Func<bool> canChange) : this(value, text)
        { available = canChange; }
        protected override bool CanChange() { return available == null || available(); }
        protected override void OnToggle()
        {
            try { setting.Set(toggle); }
            finally { OverrideToggle(setting.Value); }
        }
        public override void Draw(int x, int y, bool selected)
        {
            OverrideToggle(setting.Value);
            UiTheme.DrawText(Game1.instance.contentManager.font.MenuFont,
                label, new Vector2(x, y), CanChange() ? UiTheme.Text : Color.Gray);
            DrawCheckBox(new Vector2(x + labelSize.X + 2, y + labelSize.Y / 2), toggle);
        }
        public override Point GetSize()
        {
            labelSize = Game1.instance.contentManager.font.MenuFont.MeasureString(label).ToPoint();
            Point checkbox = GetCheckBoxSize();
            return new Point(labelSize.X + checkbox.X + 2, System.Math.Max(labelSize.Y, checkbox.Y));
        }
    }
}
