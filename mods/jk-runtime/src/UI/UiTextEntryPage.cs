using System;
using System.Linq;
using JumpKing;
using JumpKing.Controller;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace JKRuntime.UI
{
    /// <summary>Reusable single-line text entry for modal, embedded and composed pages.
    /// Captures keyboard text separately from menu bindings; gamepad and pointer use
    /// the on-screen keyboard. Call OnClose even when the parent is torn down.</summary>
    public sealed class UiTextEntryPage : IUiPage, IUiPageInputPolicy
    {
        private readonly string title, initial;
        private readonly Action<string> accepted;
        private readonly bool allowEmpty;
        private readonly TextEntryBuffer editor = new TextEntryBuffer();
        private readonly UiTextRenderer text = new UiTextRenderer();
        private readonly UiFrame frame = new UiFrame(new Rectangle(12,12,456,336));
        private TextInputCapture capture;
        private int index, alphabet;
        private bool opening, finishing, acceptResult, dragging;
        private string error = "";
        private readonly string[] alphabets = {
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -_!.,",
            "abcdefghijklmnopqrstuvwxyz0123456789 -_!.,",
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ0123456789 -_",
            "абвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789 -_" };
        public bool WantsClose { get; private set; }
        public bool HandlesCancel { get { return true; } }
        /// <summary>Current text; accepted callback runs once, after release, only on confirmation.</summary>
        public string Text { get { return editor.Text; } }
        /// <summary>Maximum 1..1024 UTF-16 BMP characters; whitespace is trimmed on acceptance.
        /// Empty input may be enabled for search. Cancel never calls accepted.</summary>
        public UiTextEntryPage(string title, string initial, Action<string> accepted, int maxLength = 64, bool allowEmpty = false)
        {
            if (accepted == null) throw new ArgumentNullException("accepted");
            if (maxLength < 1 || maxLength > 1024) throw new ArgumentOutOfRangeException("maxLength");
            this.title = title ?? "TEXT"; this.initial = initial ?? "";
            this.accepted = accepted; this.allowEmpty = allowEmpty; editor.MaxLength = maxLength;
        }
        public void OnOpen()
        {
            OnClose(); WantsClose = false; finishing = false; opening = true;
            index = alphabet = 0; error = ""; capture = new TextInputCapture();
            try { editor.Open(initial, false); }
            catch { OnClose(); throw; }
        }
        public void OnClose()
        {
            UiPointer.CancelCapture(this); editor.Dispose(); text.Dispose();
            if (capture != null) capture.Dispose(); capture = null;
        }
        private static bool Released()
        {
            if (Keyboard.GetState().GetPressedKeys().Length != 0) return false;
            return ControllerManager.instance == null || !ControllerManager.instance.GetConnectedPads()
                .Any(p => p.IsValid && p.IsConnected && p.GetPad().GetPressedButtons().Length != 0);
        }
        private void Finish(bool accept)
        {
            if (accept && editor.ClipboardPending) { error = "Wait for the clipboard operation."; UiSounds.Play(UiSound.Error); return; }
            if (accept && !allowEmpty && string.IsNullOrWhiteSpace(editor.Text)) { error = "Enter a name."; UiSounds.Play(UiSound.Error); return; }
            finishing = true; acceptResult = accept;
        }
        public void Update(UiInput input, float delta)
        {
            if (capture == null || WantsClose) return;
            if (opening)
            {
                editor.DiscardPendingText();
                if (Released()) opening = false;
                return;
            }
            if (finishing)
            {
                editor.DiscardPendingText();
                if (!Released()) return;
                if (acceptResult)
                {
                    try { accepted(editor.Text.Trim()); }
                    catch (Exception failure) { UiSounds.Play(UiSound.Error); error = failure.GetBaseException().Message; finishing = false; return; }
                }
                UiSounds.Play(acceptResult ? UiSound.Confirm : UiSound.Back); WantsClose = true; return;
            }
            editor.Update(delta);
            if (Game1.instance != null && !Game1.instance.IsActive) return;
            if (editor.Cancel) { Finish(false); return; }
            if (editor.Confirm) { Finish(true); return; }
            input = UiPointer.Read(this,input);
            int count = alphabets[alphabet].Length + 6;
            if (input.Cancel) { Finish(false); return; }
            if (input.Secondary) editor.Delete(true);
            if (input.Left) UiSounds.Select(ref index, (index + count - 1) % count);
            if (input.Right) UiSounds.Select(ref index, (index + 1) % count);
            if (input.Up) UiSounds.Select(ref index, (index + count - 8) % count);
            if (input.Down) UiSounds.Select(ref index, (index + 8) % count);
            if (!input.Confirm) return;
            int length = alphabets[alphabet].Length;
            if (index < length + 4) UiSounds.Play(UiSound.Change);
            if (index < length) editor.Insert(alphabets[alphabet][index].ToString());
            else switch (index-length)
            {
                case 0: alphabet = (alphabet+1)%alphabets.Length; index = 0; break;
                case 1: editor.Delete(true); break;
                case 2: editor.Move(editor.Caret-1,false); break;
                case 3: editor.Move(editor.Caret+1,false); break;
                case 4: Finish(true); break;
                case 5: Finish(false); break;
            }
        }
        private void Line(string value, int x, int y, int width, Color color)
        { text.DrawFitted(value,width,new Vector2(x,y),color,true); }
        public void Draw()
        {
            UiPointer.BeginSurface(this); frame.Draw();
            Line(title,25,26,425,UiTheme.Gold); DrawField(new Rectangle(25,52,421,30));
            string characters = alphabets[alphabet];
            string[] commands = { "Aa/RU", "Erase", "<", ">", "OK", "Back" };
            for (int i = 0; i < characters.Length+commands.Length; i++)
            {
                int current = i, x = 36+i%8*51, y = 98+i/8*20;
                var bounds = new Rectangle(x-4,y-3,48,20);
                UiPointer.ActionRegion(bounds,UiAction.Confirm,() => UiSounds.Select(ref index, current));
                if (i == index) UiTheme.Panel(bounds,new Color(31,48,48),UiTheme.Cyan);
                string label = i < characters.Length ? (characters[i] == ' ' ? "Space" : characters[i].ToString()) : commands[i-characters.Length];
                Line(label,x,y,43,i == index ? UiTheme.Cyan : UiTheme.Text);
            }
            Line(editor.Text.Length + " / " + editor.MaxLength,25,236,425,UiTheme.Muted);
            Line(error.Length > 0 ? error : editor.Error.Length > 0 ? editor.Error : "Enter: OK   Esc: Back   Ctrl+A/V   Shift+arrows",25,251,425,UiTheme.Muted);
            var main = ControllerManager.instance == null ? null : ControllerManager.instance.GetMain();
            if (main == null || main.GetPad().GetSaveIdentifier() == "pc_keyboard_jump_king")
            {
                Line("Type directly. Arrows move the text caret.",28,270,424,UiTheme.Muted);
                UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds,1),new UiCommand("Ctrl+A","Select all"),new UiCommand("Ctrl+V","Paste"));
                DrawFinishButton(new Rectangle(28,312,202,20),"Enter: OK",true);
                DrawFinishButton(new Rectangle(238,312,214,20),"Esc: Back",false);
                return;
            }
            UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds,2),
                UiInputHints.Command(UiAction.Up,"Up"),UiInputHints.Command(UiAction.Down,"Down"));
            UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds,1),
                UiInputHints.Command(UiAction.Left,"Left"),UiInputHints.Command(UiAction.Right,"Right"));
            UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds),UiInputHints.Command(UiAction.Confirm,"Select"),
                UiInputHints.Command(UiAction.Secondary,"Erase"),UiInputHints.Command(UiAction.Cancel,"Back"));
        }
        private void DrawFinishButton(Rectangle bounds, string label, bool accept)
        {
            bool hover = UiPointer.Visible && bounds.Contains(UiPointer.Position);
            UiTheme.Panel(bounds,UiTheme.PanelFill,hover ? UiTheme.Cyan : UiTheme.Border);
            Line(label,bounds.X+8,bounds.Y+5,bounds.Width-16,hover ? UiTheme.Cyan : UiTheme.Text);
            UiPointer.Region(bounds,null,() => { if (!opening && !finishing) Finish(accept); });
        }
        private void DrawField(Rectangle bounds)
        {
            UiTheme.Panel(bounds,UiTheme.PanelFill,UiTheme.Cyan);
            var font = text.Font(editor.Text,true); int start = 0;
            while (start < editor.Caret && font.MeasureString(editor.Text.Substring(start,editor.Caret-start)).X > bounds.Width-20) start++;
            int end = editor.Text.Length;
            while (end > start && font.MeasureString(editor.Text.Substring(start,end-start)).X > bounds.Width-12) end--;
            int left = Math.Max(start,Math.Min(editor.Caret,editor.Anchor)), right = Math.Min(end,Math.Max(editor.Caret,editor.Anchor));
            Vector2 at = new Vector2(bounds.X+6,bounds.Y+6);
            if (right > left) UiTheme.Panel(new Rectangle((int)(at.X+font.MeasureString(editor.Text.Substring(start,left-start)).X),(int)at.Y,
                (int)font.MeasureString(editor.Text.Substring(left,right-left)).X,18),UiTheme.Border,UiTheme.Border);
            Game1.spriteBatch.DrawString(font,editor.Text.Substring(start,end-start),at,UiTheme.Text);
            int caret = (int)(at.X+font.MeasureString(editor.Text.Substring(start,editor.Caret-start)).X);
            UiTheme.Panel(new Rectangle(caret,(int)at.Y,1,18),UiTheme.Gold,UiTheme.Gold);
            int offset = start;
            UiPointer.DragRegion(bounds,point => {
                int position = offset;
                while (position < editor.Text.Length && font.MeasureString(editor.Text.Substring(offset,position-offset+1)).X < point.X-at.X) position++;
                editor.Move(position,dragging); dragging = true;
            },cancelled => dragging = false);
        }
    }
}
