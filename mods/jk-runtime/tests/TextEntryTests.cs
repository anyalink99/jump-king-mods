using System;
using JKRuntime.UI;
using JumpKing.Controller;
using Microsoft.Xna.Framework.Input;

namespace JKRuntime
{
    internal static class TextEntryTests
    {
        private static int checks;
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr value, IntPtr extra);
        private static void Check(bool result, string label)
        { if (!result) throw new Exception(label); checks++; }
        private sealed class Pad : IPad
        {
            internal int[] Held = new int[0];
            public int[] GetPressedButtons() { return Held; }
            public string ButtonToString(int button) { return button.ToString(); }
            public string GetSaveIdentifier() { return "pc_keyboard_jump_king"; }
            public string GetPrintName() { return "Keyboard"; }
            public bool IsConnected() { return true; }
            public PadBinding GetDefaultBind() { return new PadBinding(); }
        }
        [STAThread]
        private static void Main()
        {
            ClipboardChecks();
            using (var window = new System.Windows.Forms.Form { ShowInTaskbar = false })
            using (var buffer = new TextEntryBuffer())
            {
                buffer.Open(""); buffer.AttachWindow(window.Handle);
                foreach (char character in "Gold Золото") SendMessage(window.Handle,0x102,(IntPtr)character,IntPtr.Zero);
                buffer.UpdateKeyboard(new KeyboardState(),.016f,true);
                Check(buffer.Text == "Gold Золото", "Native window text messages preserve active-layout characters");
                buffer.Dispose(); SendMessage(window.Handle,0x102,(IntPtr)'!',IntPtr.Zero);
                buffer.UpdateKeyboard(new KeyboardState(),.016f,true);
                Check(buffer.Text == "Gold Золото", "Closed editor detaches its native window hook");
            }
            using (var buffer = new TextEntryBuffer { MaxLength = 12 })
            {
                buffer.Open("Космос"); buffer.Move(0,false); buffer.Insert("My ");
                Check(buffer.Text == "My Космос", "Unicode inserts at the caret");
                buffer.Move(3,false); buffer.Move(9,true); buffer.Insert("Gold");
                Check(buffer.Text == "My Gold", "Selection replacement preserves unselected text");
                buffer.Move(0,false); buffer.Delete(false); buffer.Move(buffer.Text.Length,false); buffer.Delete(true);
                Check(buffer.Text == "y Gol", "Forward and backward deletion");
                buffer.UpdateKeyboard(new KeyboardState(Keys.LeftControl,Keys.A),.016f,true);
                buffer.Insert("Абв\n\t\0гд"); Check(buffer.Text == "Абвгд", "Select all and paste remove control characters");
                buffer.Insert("12345678901234567890"); Check(buffer.Text.Length == 12, "Length cap applies to pasted content");
                buffer.UpdateKeyboard(new KeyboardState(),.016f,true);
                buffer.UpdateKeyboard(new KeyboardState(Keys.Home,Keys.LeftShift),.016f,true);
                Check(buffer.Caret == 0 && buffer.Anchor == 12, "Shift Home extends selection");
                buffer.UpdateKeyboard(new KeyboardState(Keys.Escape),.016f,false);
                Check(!buffer.Cancel, "Focus loss does not cancel the page");
                buffer.UpdateKeyboard(new KeyboardState(Keys.Enter),.016f,true);
                Check(buffer.Confirm, "Enter is an editor command");
                buffer.UpdateKeyboard(new KeyboardState(Keys.Enter),.5f,true);
                Check(!buffer.Confirm, "Held Enter does not submit twice");
                buffer.Open("abcd");
                buffer.UpdateKeyboard(new KeyboardState(Keys.Back),.016f,true);
                Check(buffer.Text == "abc" && !buffer.Cancel && !buffer.Confirm, "Backspace edits text without a navigation command");
                buffer.UpdateKeyboard(new KeyboardState(),.016f,true);
                buffer.UpdateKeyboard(new KeyboardState(Keys.Left),.016f,true);
                Check(buffer.Caret == 2 && !buffer.Cancel, "Arrow edits the caret without a menu command");
                buffer.UpdateKeyboard(new KeyboardState(Keys.Delete),.016f,true);
                Check(buffer.Text == "ab" && !buffer.Cancel, "Delete edits text without activating a bound action");
            }
            var native = new Pad { Held = new[] { 65,32,27 } };
            bool mouseHeld = true;
            var pad = new KeyboardMousePad(native,key => mouseHeld ? unchecked((short)0x8000) : (short)0,() => true);
            using (var outer = new TextInputCapture())
            {
                Check(pad.GetPressedButtons().Length == 0, "Text capture suppresses letters, rebound menu keys and mouse bindings");
                using (var inner = new TextInputCapture()) Check(TextInputCapture.Active, "Nested capture remains active");
                Check(pad.GetPressedButtons().Length == 0, "Closing a nested capture preserves the outer owner");
            }
            Check(pad.GetPressedButtons().Length == 0, "Held keys cannot escape after forced close");
            native.Held = new int[0];
            Check(pad.GetPressedButtons().Length == 0, "Mouse bindings stay blocked after keyboard release");
            mouseHeld = false; pad.GetPressedButtons(); native.Held = new[] {65};
            Check(Array.IndexOf(pad.GetPressedButtons(),65) >= 0, "Release re-arms native keyboard input");
            var slow = new Input.KeyboardInputGate();
            var fast = new Input.KeyboardInputGate();
            using (UIApi.AcquireTextInput()) Check(fast.Suppress(false), "Custom editor lease suppresses an idle fast producer");
            Check(fast.Suppress(false) && !fast.Suppress(false), "Fast producer independently observes release");
            Check(slow.Suppress(true), "Open and close between polls invalidates the slow producer too");
            Check(slow.Suppress(true), "Another source cannot rearm a held slow snapshot");
            Check(slow.Suppress(false) && !slow.Suppress(true), "Slow producer rearms only after its own release");
            string result = null;
            var page = new UiTextEntryPage("Name","",value => result = value);
            page.OnOpen(); page.Update(new UiInput(),.016f);
            page.Update(new UiInput { Confirm = true },.016f);
            Check(page.Text == "A", "Gamepad enters a character without keyboard input");
            page.Update(new UiInput { Cancel = true },.016f); page.Update(new UiInput(),.016f);
            Check(page.WantsClose && result == null, "Cancel closes after release without committing");
            page.OnClose(); page.OnClose(); Check(!TextInputCapture.Active, "Repeated close releases capture exactly once");
            Check(UIApi.Supports("text-entry-v1"), "Text entry availability is discoverable");
            Check(UIApi.Supports("text-input-capture-v1"), "Custom editor capture is discoverable");
            Console.WriteLine("[OK] Text entry: " + checks + " buffer, focus, capture, release and gamepad checks");
        }
        private static void ClipboardChecks()
        {
            var original = ClipboardWork.Read;
            using (var entered = new System.Threading.ManualResetEvent(false))
            using (var release = new System.Threading.ManualResetEvent(false))
            using (var returned = new System.Threading.ManualResetEvent(false))
            using (var buffer = new TextEntryBuffer())
            {
                try
                {
                    ClipboardWork.Read = delegate { entered.Set(); release.WaitOne(); returned.Set(); return "clipboard"; };
                    buffer.Open("before");
                    buffer.Paste();
                    Check(entered.WaitOne(2000) && buffer.ClipboardPending && buffer.Text == "before", "Paste returns before blocked STA clipboard completes");
                    Check(ClipboardWork.Start(false, null) == null, "Blocked clipboard has one shared worker, not unlimited retries");
                    buffer.Insert(" edit"); release.Set(); Check(returned.WaitOne(2000), "Clipboard fixture was released");
                    Check(System.Threading.SpinWait.SpinUntil(delegate { buffer.UpdateKeyboard(new KeyboardState(), .016f, true); return !buffer.ClipboardPending; }, 2000), "Clipboard completion is observed without joining the worker");
                    Check(buffer.Text == "before edit" && buffer.Error.Contains("Text changed"), "A late paste cannot replace newer edits");
                    ClipboardWork.Read = delegate { return "ok"; };
                    buffer.Paste();
                    Check(System.Threading.SpinWait.SpinUntil(delegate { buffer.UpdateKeyboard(new KeyboardState(), .016f, true); return !buffer.ClipboardPending; }, 2000), "Successful paste completes");
                    Check(buffer.Text == "before editok", "Successful paste inserts at the captured selection");
                    buffer.Paste(); buffer.Dispose(); buffer.Open("next page");
                    buffer.UpdateKeyboard(new KeyboardState(), .016f, true);
                    Check(buffer.Text == "next page", "Closed editor drops late clipboard results");
                }
                finally { release.Set(); returned.WaitOne(2000); ClipboardWork.Read = original; }
            }
        }
    }
}
