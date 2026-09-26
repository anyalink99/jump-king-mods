using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing.Controller;

namespace JKRuntime.UI
{
    internal sealed class InputContextLease : IDisposable
    {
        private sealed class Snapshot
        {
            internal ModalPad Gate;
            internal int[] Pause;
            internal int[] Cancel;
            internal int[] Boots;
            internal HashSet<int> Held;
        }

        private sealed class ModalPad : IPad, IInputPadLayer
        {
            private IPad inner;
            private readonly HashSet<int> blocked;

            internal ModalPad(IPad value, IEnumerable<int> blockedButtons)
            {
                inner = value;
                blocked = new HashSet<int>(blockedButtons);
            }

            internal IPad Inner { get { return inner; } }
            IPad IInputPadLayer.Inner { get { return inner; } }
            internal int[] GetRawPressedButtons() { return inner.GetPressedButtons(); }

            public int[] GetPressedButtons()
            {
                return inner.GetPressedButtons()
                    .Where(button => !blocked.Contains(button))
                    .ToArray();
            }

            public string ButtonToString(int button) { return inner.ButtonToString(button); }
            public PadBinding GetDefaultBind() { return inner.GetDefaultBind(); }
            public string GetSaveIdentifier() { return inner.GetSaveIdentifier(); }
            public string GetPrintName() { return inner.GetPrintName(); }
            public bool IsConnected() { return inner.IsConnected(); }

            bool IInputPadLayer.ReplaceInner(IPad expected, IPad replacement)
            {
                if (!ReferenceEquals(inner, expected)) return false;
                inner = replacement;
                return true;
            }
        }

        private static readonly FieldInfo PadField = typeof(PadInstance).GetField(
            "m_pad",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Dictionary<PadInstance, Snapshot> snapshots =
            new Dictionary<PadInstance, Snapshot>();
        private bool disposed;

        private InputContextLease()
        {
            if (PadField == null)
                throw new InvalidOperationException(
                    "Jump King input-device contract is unavailable");
            AttachConnectedPads();
        }

        internal static InputContextLease AcquireModal()
        {
            return new InputContextLease();
        }

        internal UiInput ReadModal(PadState state)
        {
            AttachConnectedPads();
            foreach (KeyValuePair<PadInstance, Snapshot> pair in snapshots)
            {
                if (!pair.Key.IsValid || !pair.Key.IsConnected) continue;
                HashSet<int> held = new HashSet<int>(
                    pair.Value.Gate.GetRawPressedButtons());
                if (PressedNow(held, pair.Value.Held, pair.Value.Pause)
                    || PressedNow(held, pair.Value.Held, pair.Value.Cancel))
                    state.cancel = true;
                if (PressedNow(held, pair.Value.Held, pair.Value.Boots))
                    state.boots = true;
                pair.Value.Held = held;
            }
            return UiInputRouter.Read(state, false);
        }

        internal bool IsCancelReleased()
        {
            AttachConnectedPads();
            foreach (KeyValuePair<PadInstance, Snapshot> pair in snapshots)
            {
                if (!pair.Key.IsValid || !pair.Key.IsConnected) continue;
                int[] held = pair.Value.Gate.GetRawPressedButtons();
                if (held.Any(button => pair.Value.Pause.Contains(button)
                    || pair.Value.Cancel.Contains(button))) return false;
            }
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (KeyValuePair<PadInstance, Snapshot> pair in snapshots)
            {
                if (!pair.Key.IsValid) continue;
                IPad current = PadField.GetValue(pair.Key) as IPad;
                if (ReferenceEquals(current, pair.Value.Gate))
                    PadField.SetValue(pair.Key, pair.Value.Gate.Inner);
                else if (!RemoveFromChain(current, pair.Value.Gate))
                {
                    Console.WriteLine(
                        "[JK Runtime UI] Could not detach the modal input layer; "
                        + "an unknown pad wrapper replaced the active chain");
                }
            }
            snapshots.Clear();
        }

        private static bool RemoveFromChain(IPad current, ModalPad gate)
        {
            IInputPadLayer layer = current as IInputPadLayer;
            while (layer != null)
            {
                if (layer.ReplaceInner(gate, gate.Inner)) return true;
                layer = layer.Inner as IInputPadLayer;
            }
            return false;
        }

        private void AttachConnectedPads()
        {
            if (disposed) return;
            foreach (PadInstance pad in ControllerManager.instance.GetConnectedPads())
            {
                if (!pad.IsValid || snapshots.ContainsKey(pad)) continue;
                // Keep the permanent text/release boundary beneath this
                // removable layer, where it can observe the unfiltered device.
                KeyboardMousePad.Ensure(pad);
                PadBinding binding = pad.GetBind();
                int[] pause = Copy(binding.GetButtonBind(JKpadButtons.Pause));
                int[] cancel = Copy(binding.GetButtonBind(JKpadButtons.Cancel));
                int[] boots = Copy(binding.GetButtonBind(JKpadButtons.Boots));
                List<int> blocked = new List<int>();
                AddRange(blocked, pause);
                AddRange(blocked, cancel);
                AddRange(blocked, boots);
                AddRange(blocked, binding.GetButtonBind(JKpadButtons.Snake));
                AddRange(blocked, binding.GetButtonBind(JKpadButtons.Restart));
                IPad current = PadField.GetValue(pad) as IPad;
                if (current == null) continue;
                ModalPad gate = new ModalPad(current, blocked);
                snapshots.Add(pad, new Snapshot
                {
                    Gate = gate,
                    Pause = pause,
                    Cancel = cancel,
                    Boots = boots,
                    Held = new HashSet<int>(current.GetPressedButtons())
                });
                PadField.SetValue(pad, gate);
            }
        }

        private static bool PressedNow(
            HashSet<int> held,
            HashSet<int> previous,
            int[] buttons)
        {
            return held.Any(button => !previous.Contains(button)
                && buttons.Contains(button));
        }

        private static void AddRange(List<int> target, int[] values)
        {
            foreach (int value in values ?? new int[0])
                if (!target.Contains(value)) target.Add(value);
        }

        private static int[] Copy(int[] values)
        {
            return values == null ? new int[0] : (int[])values.Clone();
        }
    }
}
