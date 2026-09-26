using System;
using System.Collections.Generic;
using System.Reflection;
using JKRuntime.Input;
using JumpKing;
using JumpKing.Controller;

namespace JKRuntime.UI
{
    // Permanent desktop input extension beneath the removable chord layer.
    // Keep the native keyboard profile identity, defaults and connection state.
    internal sealed class KeyboardMousePad : IPad, IInputPadLayer
    {
        private IPad inner;
        private readonly Func<int, short> read;
        private readonly Func<bool> focused;
        private readonly bool[] armed = new bool[5];
        private readonly KeyboardInputGate textInput = new KeyboardInputGate();
        private static readonly FieldInfo PadField = typeof(PadInstance).GetField("m_pad", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Ensure(PadInstance instance)
        {
            IPad current = instance.GetPad(), wrapped = Wrap(current);
            if (!ReferenceEquals(current, wrapped)) PadField.SetValue(instance, wrapped);
        }

        internal KeyboardMousePad(IPad pad, Func<int, short> reader, Func<bool> hasFocus)
        {
            inner = pad;
            read = reader;
            focused = hasFocus;
        }

        internal static IPad Wrap(IPad pad)
        {
            if (pad.GetSaveIdentifier() != "pc_keyboard_jump_king") return pad;
            IPad current = pad;
            while (current != null)
            {
                if (current is KeyboardMousePad) return pad;
                IInputPadLayer layer = current as IInputPadLayer;
                current = layer == null ? null : layer.Inner;
            }
            return new KeyboardMousePad(pad, MouseButtons.GetAsyncKeyState, delegate {
                return Game1.instance != null
                    && MouseButtons.GetForegroundWindow() == Game1.instance.Window.Handle;
            });
        }

        IPad IInputPadLayer.Inner { get { return inner; } }
        bool IInputPadLayer.ReplaceInner(IPad expected, IPad replacement)
        {
            if (!ReferenceEquals(inner, expected)) return false;
            inner = replacement;
            return true;
        }

        public int[] GetPressedButtons()
        {
            List<int> result = new List<int>(inner.GetPressedButtons());
            int mouseHeld = 0;
            for (int index = 0; index < armed.Length; index++)
                if ((read(MouseButtons.ToVirtualKey(MouseButtons.Left + index)) & 0x8000) != 0) mouseHeld |= 1 << index;
            if (textInput.Suppress(result.Count != 0 || mouseHeld != 0)) { Array.Clear(armed,0,armed.Length); return new int[0]; }
            if (UiPointer.SuppressMouseBindings) { Array.Clear(armed, 0, armed.Length); return result.ToArray(); }
            if (!focused())
            {
                Array.Clear(armed, 0, armed.Length);
                return result.ToArray();
            }
            for (int index = 0; index < armed.Length; index++)
            {
                int button = MouseButtons.Left + index;
                bool down = (mouseHeld & (1 << index)) != 0;
                if (!down) armed[index] = true;
                if (down && armed[index]) result.Add(button);
            }
            return result.ToArray();
        }

        public string ButtonToString(int button)
        { return MouseButtons.IsMouseButton(button) ? MouseButtons.GetLabel(button) : inner.ButtonToString(button); }
        public PadBinding GetDefaultBind() { return inner.GetDefaultBind(); }
        public string GetSaveIdentifier() { return inner.GetSaveIdentifier(); }
        public string GetPrintName() { return inner.GetPrintName(); }
        public bool IsConnected() { return inner.IsConnected(); }
    }
}
