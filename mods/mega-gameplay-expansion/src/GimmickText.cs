using System;
using System.Linq;
using JKRuntime.UI;
using JumpKing;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal sealed class GimmickText : IDisposable
    {
        private readonly TextEntryBuffer editor = new TextEntryBuffer(256, true);
        private bool dragging;
        private string message;
        internal string Text { get { return editor.Text; } }
        internal int Caret { get { return editor.Caret; } }
        internal int Anchor { get { return editor.Anchor; } }
        internal bool Confirm { get { return editor.Confirm; } }
        internal bool Cancel { get { return editor.Cancel; } }
        internal string Error { get { return message ?? editor.Error; } set { message = value; } }
        internal void Open(string value) { message = null; editor.Open(value); }
        internal void Insert(string value) { editor.Insert(value); }
        internal void Move(int position, bool extend) { editor.Move(position, extend); }
        internal void Delete(bool back) { editor.Delete(back); }
        internal bool Update(float delta) { string before = Text; editor.Update(delta); return before != Text; }
        internal void Draw(Rectangle bounds)
        {
            UiTheme.Panel(bounds, UiTheme.PanelFill, UiTheme.Gold);
            var font = Game1.instance.contentManager.font.MenuFontSmall;
            string display = new string(Text.Select(c => font.Characters.Contains(c) ? c : '?').ToArray());
            int start = 0; while (start < Caret && font.MeasureString(display.Substring(start, Caret - start)).X > bounds.Width - 20) start++;
            string visible = UiTheme.FitText(display.Substring(start), bounds.Width - 12, true);
            float x = bounds.X + 6, y = bounds.Y + 6;
            int lo = Math.Max(start, Math.Min(Caret, Anchor)), hi = Math.Min(start + visible.Length, Math.Max(Caret, Anchor));
            if (hi > lo) UiTheme.Panel(new Rectangle((int)(x + font.MeasureString(display.Substring(start, lo - start)).X), (int)y, (int)font.MeasureString(display.Substring(lo, hi - lo)).X, 13), UiTheme.Border, UiTheme.Border);
            UiTheme.TextLine(visible, new Vector2(x, y), UiTheme.Text, true);
            int caretX = (int)(x + font.MeasureString(display.Substring(start, Caret - start)).X);
            UiTheme.Panel(new Rectangle(caretX, (int)y, 1, 13), UiTheme.Gold, UiTheme.Gold);
            int offset = start;
            UiPointer.DragRegion(bounds, point => {
                int at = offset; float local = point.X - x;
                while (at < display.Length && font.MeasureString(display.Substring(offset, at - offset + 1)).X < local) at++;
                Move(at, dragging); dragging = true;
            }, cancelled => dragging = false);
        }
        public void Dispose() { editor.Dispose(); dragging = false; }
    }
}
