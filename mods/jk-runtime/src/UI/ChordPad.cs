using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.Controller;

namespace JKRuntime.UI
{
    internal interface IInputPadLayer
    {
        IPad Inner { get; }
        bool ReplaceInner(IPad expected, IPad replacement);
    }

    internal sealed class ChordPad : IPad, IInputPadLayer
    {
        private IPad inner;
        private readonly Dictionary<int, UiChord> chords = new Dictionary<int, UiChord>();
        private readonly Dictionary<string, List<int>> owners =
            new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private int nextButton = -1000000;

        internal ChordPad(IPad pad)
        {
            inner = KeyboardMousePad.Wrap(pad);
        }

        internal IPad Inner { get { return inner; } }
        internal bool IsEmpty { get { return owners.Count == 0; } }
        IPad IInputPadLayer.Inner { get { return inner; } }

        bool IInputPadLayer.ReplaceInner(IPad expected, IPad replacement)
        {
            if (!ReferenceEquals(inner, expected)) return false;
            inner = replacement;
            return true;
        }

        internal int[] SetChords(string owner, UiChord[] values)
        {
            JKRuntime.Input.PhysicalBindings.Invalidate();
            Remove(owner);
            List<int> result = new List<int>();
            List<int> synthetic = new List<int>();
            foreach (UiChord chord in values ?? new UiChord[0])
            {
                if (chord == null || chord.IsEmpty) continue;
                int[] buttons = chord.Buttons;
                if (buttons.Length == 1)
                {
                    if (!result.Contains(buttons[0])) result.Add(buttons[0]);
                    continue;
                }
                int button = nextButton--;
                chords[button] = new UiChord(buttons);
                synthetic.Add(button);
                result.Add(button);
            }
            if (synthetic.Count > 0) owners[owner] = synthetic;
            return result.ToArray();
        }

        internal void Remove(string owner)
        {
            JKRuntime.Input.PhysicalBindings.Invalidate();
            List<int> values;
            if (!owners.TryGetValue(owner, out values)) return;
            foreach (int value in values) chords.Remove(value);
            owners.Remove(owner);
        }

        internal int[][] ResolvePhysicalBinding(int[] runtimeButtons)
        {
            if (runtimeButtons == null || runtimeButtons.Length == 0)
                return new int[0][];
            int[][] result = new int[runtimeButtons.Length][];
            for (int index = 0; index < runtimeButtons.Length; index++)
            {
                UiChord chord;
                result[index] = chords.TryGetValue(
                    runtimeButtons[index], out chord)
                    ? chord.Buttons
                    : new[] { runtimeButtons[index] };
            }
            return result;
        }

        public int[] GetPressedButtons()
        {
            int[] physical = inner.GetPressedButtons();
            List<int> result = new List<int>(physical);
            foreach (KeyValuePair<int, UiChord> pair in chords)
                if (pair.Value.IsHeld(physical)) result.Add(pair.Key);
            return result.ToArray();
        }

        public string ButtonToString(int button)
        {
            UiChord chord;
            if (!chords.TryGetValue(button, out chord)) return inner.ButtonToString(button);
            string result = string.Empty;
            foreach (int value in chord.Buttons)
            {
                string label = inner.ButtonToString(value);
                result = result.Length == 0 ? label : result + "+" + label;
            }
            return result;
        }

        public PadBinding GetDefaultBind() { return inner.GetDefaultBind(); }
        public string GetSaveIdentifier() { return inner.GetSaveIdentifier(); }
        public string GetPrintName() { return inner.GetPrintName(); }
        public bool IsConnected() { return inner.IsConnected(); }
    }

    internal static class ChordVirtualizer
    {
        private static readonly FieldInfo PadField = typeof(PadInstance).GetField(
            "m_pad",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<PadInstance, ChordPad> Pads =
            new Dictionary<PadInstance, ChordPad>();

        internal static int[] Apply(PadInstance instance, string owner, UiChord[] chords)
        {
            return GetPad(instance).SetChords(owner, chords);
        }

        // Native profiles cannot represent mouse IDs or synthetic chords.
        // Keep full bindings in Runtime settings; never turn mouse+key into a
        // bare key when the Runtime is absent. Controller IDs are a separate domain.
        internal static int[] NativeAlternatives(IPad pad, UiChord[] chords)
        {
            if (pad.GetSaveIdentifier() != "pc_keyboard_jump_king") return UiChord.ToAlternatives(chords);
            List<UiChord> native = new List<UiChord>();
            foreach (UiChord chord in chords ?? new UiChord[0])
                if (chord != null && !Array.Exists(chord.Buttons, JKRuntime.Input.MouseButtons.IsMouseButton)) native.Add(chord);
            return UiChord.ToAlternatives(native.ToArray());
        }

        internal static void Remove(PadInstance instance, string owner)
        {
            ChordPad pad;
            if (instance == null || !Pads.TryGetValue(instance, out pad)) return;
            pad.Remove(owner);
            if (!pad.IsEmpty) return;
            if (PadField != null && ReferenceEquals(PadField.GetValue(instance), pad))
                PadField.SetValue(instance, pad.Inner);
            Pads.Remove(instance);
        }

        internal static int[][] ResolvePhysicalBinding(
            PadInstance instance,
            int[] runtimeButtons)
        {
            ChordPad pad;
            if (instance != null && Pads.TryGetValue(instance, out pad))
                return pad.ResolvePhysicalBinding(runtimeButtons);
            if (runtimeButtons == null || runtimeButtons.Length == 0)
                return new int[0][];
            int[][] result = new int[runtimeButtons.Length][];
            for (int index = 0; index < runtimeButtons.Length; index++)
                result[index] = new[] { runtimeButtons[index] };
            return result;
        }

        private static ChordPad GetPad(PadInstance instance)
        {
            if (instance == null) throw new ArgumentNullException("instance");
            ChordPad pad;
            if (Pads.TryGetValue(instance, out pad)) return pad;
            if (PadField == null) throw new InvalidOperationException("Jump King pad field is unavailable");
            IPad current = PadField.GetValue(instance) as IPad;
            if (current == null) throw new InvalidOperationException("Jump King input device is unavailable");
            pad = current as ChordPad;
            if (pad == null) pad = new ChordPad(current);
            PadField.SetValue(instance, pad);
            Pads[instance] = pad;
            return pad;
        }
    }
}
