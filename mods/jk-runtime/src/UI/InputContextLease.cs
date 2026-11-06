using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using JumpKing;
using JumpKing.Controller;

namespace JKRuntime.UI
{
    // the page reads its own device edges; gameplay never borrows that frame
    internal sealed class InputContextLease : IDisposable
    {
        private sealed class Snapshot
        {
            internal ModalPad Gate;
            internal HashSet<int> Held;
        }

        private sealed class ModalPad : IPad, IInputPadLayer
        {
            private IPad inner;
            internal readonly PadInstance Owner;
            internal bool Retiring;
            internal ModalPad(PadInstance owner, IPad value) { Owner = owner; inner = value; }
            internal IPad Inner { get { return inner; } }
            IPad IInputPadLayer.Inner { get { return inner; } }
            internal int[] Raw() { return inner.GetPressedButtons(); }
            internal bool Blocks()
            {
                if (Retiring && Raw().Length == 0) Detach(this);
                // the release sample belongs to the closing page too
                return true;
            }
            public int[] GetPressedButtons() { Blocks(); return new int[0]; }
            public string ButtonToString(int button) { return inner.ButtonToString(button); }
            public PadBinding GetDefaultBind() { return inner.GetDefaultBind(); }
            public string GetSaveIdentifier() { return inner.GetSaveIdentifier(); }
            public string GetPrintName() { return inner.GetPrintName(); }
            public bool IsConnected() { return inner.IsConnected(); }
            bool IInputPadLayer.ReplaceInner(IPad expected, IPad replacement)
            {
                if (!ReferenceEquals(inner, expected)) return false;
                inner = replacement; return true;
            }
            internal void Wrap(IPad value) { inner = value; }
        }

        private static readonly FieldInfo PadField = typeof(PadInstance).GetField("m_pad", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Overlay = typeof(PadInstance).GetField("_steam_overlay_active", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo MainPad = typeof(ControllerManager).GetField("_current_main", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly ConditionalWeakTable<PadInstance, ModalPad> Gates = new ConditionalWeakTable<PadInstance, ModalPad>();
        private static InputContextLease current;
        private static int owners, epoch;
        internal static bool Active { get { return Volatile.Read(ref owners) != 0; } }
        internal static int Epoch { get { return Volatile.Read(ref epoch); } }
        private readonly Dictionary<PadInstance, Snapshot> snapshots = new Dictionary<PadInstance, Snapshot>();
        private bool disposed;

        private InputContextLease()
        {
            if (PadField == null || Overlay == null || MainPad == null) throw new InvalidOperationException("Jump King input-device contract is unavailable");
            if (current != null) throw new InvalidOperationException("Modal input is already owned");
            current = this; Interlocked.Exchange(ref owners, 1); Interlocked.Increment(ref epoch);
            try { AttachConnectedPads(); ClearPublished(); }
            catch { Dispose(); throw; }
        }
        internal static InputContextLease AcquireModal() { return new InputContextLease(); }

        // also called before native device polling, so hotplug can't leak a first tick
        internal static void PreparePoll() { if (current != null) current.AttachConnectedPads(); }
        internal static void AfterPoll()
        {
            if (current == null) return;
            // native discovery can add a pad during the controller update itself
            current.AttachConnectedPads(); ClearPublished();
        }
        internal static bool BlocksPublication(PadInstance pad)
        {
            if (Active) return true;
            ModalPad gate;
            return Gates.TryGetValue(pad, out gate) && gate.Blocks();
        }

        internal UiInput ReadModal(PadState ignored)
        {
            AttachConnectedPads();
            var state = new PadState();
            bool focused = (Game1.instance == null || Game1.instance.IsActive) && !(bool)Overlay.GetValue(null);
            foreach (var pair in snapshots)
            {
                if (!pair.Key.IsValid || !pair.Key.IsConnected) continue;
                var held = new HashSet<int>(pair.Value.Gate.Raw());
                var binding = pair.Key.GetBind();
                if (focused && binding.Enabled)
                {
                    if (held.Any(button => !pair.Value.Held.Contains(button))) MainPad.SetValue(ControllerManager.instance, pair.Key);
                    Func<JKpadButtons, bool> pressed = action => PressedNow(held, pair.Value.Held, binding.GetButtonBind(action));
                    state.cancel |= pressed(JKpadButtons.Pause) || pressed(JKpadButtons.Cancel);
                    state.boots |= pressed(JKpadButtons.Boots);
                    state.confirm |= pressed(JKpadButtons.Confirm) || pressed(JKpadButtons.Jump);
                    state.up |= pressed(JKpadButtons.Up); state.down |= pressed(JKpadButtons.Down);
                    state.left |= pressed(JKpadButtons.Left); state.right |= pressed(JKpadButtons.Right);
                }
                pair.Value.Held = held;
            }
            return UiInputRouter.Read(state, false);
        }

        internal bool IsCancelReleased()
        {
            AttachConnectedPads();
            return snapshots.All(pair => !pair.Key.IsValid || !pair.Key.IsConnected || pair.Value.Gate.Raw().Length == 0);
        }

        public void Dispose()
        {
            if (disposed) return;
            ClearPublished();
            foreach (var pair in snapshots)
            {
                // a forced close keeps a small release guard in the device chain
                pair.Value.Gate.Retiring = true;
                if (!pair.Key.IsValid || pair.Value.Gate.Raw().Length == 0) Detach(pair.Value.Gate);
            }
            snapshots.Clear(); current = null; disposed = true;
            Interlocked.Exchange(ref owners, 0); Interlocked.Increment(ref epoch);
        }

        private static void ClearPublished()
        {
            var manager = ControllerManager.instance;
            if (manager == null) return;
            foreach (var pad in manager.GetConnectedPads())
                if (pad.IsValid) Input.NativeInputFrames.Publish(pad, new PadState(), new PadState());
            if (manager.MenuController != null) manager.MenuController.ConsumePadPresses();
        }

        private static bool RemoveFromChain(IPad pad, ModalPad gate)
        {
            if (ReferenceEquals(pad, gate)) { PadField.SetValue(gate.Owner, gate.Inner); return true; }
            var layer = pad as IInputPadLayer;
            while (layer != null)
            {
                if (layer.ReplaceInner(gate, gate.Inner)) return true;
                layer = layer.Inner as IInputPadLayer;
            }
            return false;
        }
        private static void Detach(ModalPad gate)
        {
            if (RemoveFromChain(gate.Owner.GetPad(), gate)) Gates.Remove(gate.Owner);
        }

        private void AttachConnectedPads()
        {
            if (disposed || ControllerManager.instance == null) return;
            foreach (var pad in ControllerManager.instance.GetConnectedPads())
            {
                if (!pad.IsValid) continue;
                Snapshot snapshot;
                if (!snapshots.TryGetValue(pad, out snapshot))
                {
                    ModalPad old; if (Gates.TryGetValue(pad, out old)) Detach(old);
                    KeyboardMousePad.Ensure(pad);
                    var gate = new ModalPad(pad, pad.GetPad());
                    snapshot = new Snapshot { Gate = gate, Held = new HashSet<int>(gate.Raw()) };
                    snapshots.Add(pad, snapshot); Gates.Add(pad, gate); PadField.SetValue(pad, gate);
                }
                else if (!ReferenceEquals(pad.GetPad(), snapshot.Gate))
                {
                    // chord rebinding may add a layer above us; keep the gate outermost
                    RemoveFromChain(pad.GetPad(), snapshot.Gate);
                    snapshot.Gate.Wrap(pad.GetPad()); PadField.SetValue(pad, snapshot.Gate);
                }
            }
        }
        private static bool PressedNow(HashSet<int> held, HashSet<int> previous, int[] buttons)
        { return (buttons ?? new int[0]).Any(button => held.Contains(button) && !previous.Contains(button)); }
    }
}
