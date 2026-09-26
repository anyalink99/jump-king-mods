using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using JumpKing;
using Microsoft.Xna.Framework.Input;
using Keys = Microsoft.Xna.Framework.Input.Keys;

namespace JKRuntime.UI
{
    /// <summary>Embeddable single-line editor with bounded native text capture and asynchronous clipboard access. Open/dispose on the game thread.</summary>
    public sealed class TextEntryBuffer : IDisposable
    {
        private sealed class InputWindow : NativeWindow
        {
            internal readonly Queue<char> Characters = new Queue<char>();
            protected override void WndProc(ref Message message)
            {
                if (message.Msg == 0x102 && Characters.Count < 1024)
                { char c = (char)message.WParam.ToInt32(); if (!char.IsControl(c) && !char.IsSurrogate(c)) Characters.Enqueue(c); }
                base.WndProc(ref message);
            }
        }
        private InputWindow window;
        private KeyboardState previous;
        private Keys repeated;
        private float repeat;
        private IDisposable capture;
        private ClipboardWork clipboard;
        private long clipboardStarted;
        private int revision, pasteRevision;
        private bool copying;
        private readonly bool rejectOverflow;
        private static readonly Keys[] EditingKeys = { Keys.Left, Keys.Right, Keys.Home, Keys.End, Keys.Back, Keys.Delete };
        public string Text { get; internal set; }
        public string Error { get; internal set; }
        public int MaxLength { get; internal set; }
        public int Caret { get; internal set; }
        public int Anchor { get; internal set; }
        public bool Confirm { get; private set; }
        public bool Cancel { get; private set; }
        public bool ClipboardPending { get { return clipboard != null; } }
        public TextEntryBuffer(int maximumLength = 64, bool rejectOverflow = false)
        {
            if (maximumLength < 1 || maximumLength > 1024) throw new ArgumentOutOfRangeException("maximumLength");
            MaxLength = maximumLength; Text = Error = ""; this.rejectOverflow = rejectOverflow;
        }
        public void DiscardPendingText()
        { if (window != null) window.Characters.Clear(); previous = Keyboard.GetState(); }
        public void Open(string value, bool captureInput = true)
        {
            Dispose(); Error = ""; repeat = 0; repeated = Keys.None; Text = ""; Caret = Anchor = 0; Insert(value); previous = Keyboard.GetState();
            if (captureInput) capture = UIApi.AcquireTextInput();
            try { if (Game1.instance != null && Game1.instance.Window != null) AttachWindow(Game1.instance.Window.Handle); }
            catch { window = null; Error = "Keyboard input unavailable. Use the character buttons."; }
        }
        public void Insert(string value)
        {
            value = new string((value ?? "").Where(c => !char.IsControl(c) && !char.IsSurrogate(c)).ToArray());
            int first = Math.Min(Caret,Anchor), last = Math.Max(Caret,Anchor);
            int room = MaxLength - (Text.Length - (last-first));
            if (value.Length > room && rejectOverflow) { Error = "Text is limited to " + MaxLength + " characters."; return; }
            if (value.Length > room) value = value.Substring(0,Math.Max(0,room));
            Text = Text.Substring(0,first) + value + Text.Substring(last); Caret = Anchor = first+value.Length;
            revision++;
        }
        public void Move(int position, bool extend)
        { Caret = Math.Max(0,Math.Min(Text.Length,position)); if (!extend) Anchor = Caret; revision++; }
        public void Delete(bool back)
        { if (Caret == Anchor) Anchor = back ? Math.Max(0,Caret-1) : Math.Min(Text.Length,Caret+1); Insert(""); }
        public void Paste() { BeginClipboard(false); }
        public void Copy() { if (Caret != Anchor) BeginClipboard(true); }
        private void BeginClipboard(bool copy)
        {
            if (clipboard != null) return;
            clipboard = ClipboardWork.Start(copy, copy ? Text.Substring(Math.Min(Caret,Anchor), Math.Abs(Caret-Anchor)) : null);
            if (clipboard == null) { Error = "Clipboard is busy. Try again later."; return; }
            copying = copy; pasteRevision = revision; clipboardStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            Error = copy ? "Copying..." : "Pasting...";
        }
        private void PollClipboard()
        {
            if (clipboard == null) return;
            if (!clipboard.Done)
            {
                if (System.Diagnostics.Stopwatch.GetTimestamp()-clipboardStarted > 3L*System.Diagnostics.Stopwatch.Frequency)
                { clipboard = null; Error = "Clipboard is busy. Try again later."; }
                return;
            }
            var complete = clipboard; clipboard = null;
            if (complete.Error != null) { Error = "Clipboard unavailable: " + complete.Error; return; }
            if (!copying && pasteRevision != revision) { Error = "Text changed while waiting. Paste again."; return; }
            Error = "";
            if (!copying) Insert(complete.Text);
        }
        public void Update(float delta)
        {
            UpdateKeyboard(Keyboard.GetState(),delta,Game1.instance == null || Game1.instance.IsActive);
        }
        internal void AttachWindow(IntPtr handle)
        { if (window != null) window.ReleaseHandle(); window = new InputWindow(); window.AssignHandle(handle); }
        public void UpdateKeyboard(KeyboardState keys, float delta, bool focused)
        {
            Confirm = Cancel = false;
            if (!focused)
            { if (window != null) window.Characters.Clear(); if (clipboard != null) Error = ""; clipboard = null; revision++; previous = keys; return; }
            PollClipboard();
            bool ctrl = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
            bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
            Confirm = keys.IsKeyDown(Keys.Enter) && !previous.IsKeyDown(Keys.Enter);
            Cancel = keys.IsKeyDown(Keys.Escape) && !previous.IsKeyDown(Keys.Escape);
            if (ctrl && keys.IsKeyDown(Keys.A) && !previous.IsKeyDown(Keys.A)) { Anchor = 0; Caret = Text.Length; revision++; }
            if (ctrl && keys.IsKeyDown(Keys.C) && !previous.IsKeyDown(Keys.C)) Copy();
            if (ctrl && keys.IsKeyDown(Keys.V) && !previous.IsKeyDown(Keys.V)) Paste();
            foreach (var key in EditingKeys)
            {
                if (!keys.IsKeyDown(key)) continue;
                if (!previous.IsKeyDown(key)) { repeated = key; repeat = .4f; }
                else if (repeated == key) { repeat -= delta; if (repeat > 0) continue; repeat = .035f; } else continue;
                if (key == Keys.Left) Move(!shift && Caret != Anchor ? Math.Min(Caret,Anchor) : Caret-1,shift);
                else if (key == Keys.Right) Move(!shift && Caret != Anchor ? Math.Max(Caret,Anchor) : Caret+1,shift);
                else if (key == Keys.Home) Move(0,shift); else if (key == Keys.End) Move(Text.Length,shift); else Delete(key == Keys.Back);
            }
            if (window != null) while (window.Characters.Count > 0)
            { char c = window.Characters.Dequeue(); if (!ctrl) Insert(c.ToString()); }
            previous = keys;
        }
        public void Dispose()
        {
            clipboard = null; revision++;
            try { if (window != null) { window.ReleaseHandle(); window = null; } }
            finally { if (capture != null) { capture.Dispose(); capture = null; } }
        }
    }
}
