using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using JumpKing.Controller;
using JKRuntime.Input;
using JKRuntime.UI;

namespace MegaGameplayExpansion
{
    public sealed class DashDeviceBinding
    {
        public string Device { get; set; }
        public int[][] Chords { get; set; }
    }

    internal static class DashBindings
    {
        internal static int Revision { get; private set; }
        internal static void Register()
        {
            UIApi.RegisterBinding(new UiBindingDefinition("mega-gameplay-expansion.air-dash", "Mega Gameplay Expansion", "Air Dash",
                delegate { var pad = GetMain(); return Get(pad).Select(c => new UiChord(c)).ToArray(); },
                delegate(UiChord[] chords) { Set(GetMain().GetPad().GetSaveIdentifier(), (chords ?? new UiChord[0]).Select(c => c == null ? new int[0] : c.Buttons).ToArray()); },
                delegate { Set(GetMain().GetPad().GetSaveIdentifier(), null); }));
        }
        private static PadInstance GetMain()
        {
            var pad = ControllerManager.instance.GetMain();
            if (pad == null || !pad.IsValid || !pad.IsConnected) throw new InvalidOperationException("No active input device");
            return pad;
        }
        internal static DashDeviceBinding Custom(string device)
        { return (Settings.Current.DashBindings ?? new DashDeviceBinding[0]).FirstOrDefault(b => b != null && b.Device == device); }
        internal static int[][] Get(PadInstance pad)
        {
            var custom = Custom(pad.GetPad().GetSaveIdentifier());
            return custom == null ? PhysicalBindings.ResolvePhysicalBinding(pad, pad.GetBind().jump) : Normalize(custom.Chords);
        }
        internal static int[][] Normalize(int[][] chords)
        { return (chords ?? new int[0][]).Take(2).Select(c => (c ?? new int[0]).Where(b => b >= 0).Distinct().Take(2).ToArray()).Where(c => c.Length > 0).ToArray(); }
        internal static void Set(string device, int[][] chords)
        {
            var list = (Settings.Current.DashBindings ?? new DashDeviceBinding[0]).Where(b => b != null && b.Device != device).ToList();
            if (chords != null) list.Add(new DashDeviceBinding { Device = device, Chords = Normalize(chords) });
            Settings.Edit(value => value.DashBindings = list.ToArray()); Revision++;
        }
    }

    internal struct DashPress
    {
        internal bool Pressed, SharedJump;
        internal DashPress(bool pressed, bool shared) { Pressed = pressed; SharedJump = shared; }
    }

    // Observe even when grounded, dashing or unavailable: a held takeoff/menu
    // button must never turn into a new press when dash eligibility changes.
    internal sealed class DashInput
    {
        private PadInstance previous;
        private bool known, down;
        private int revision = -1, bindingsEpoch = -1;
        private int[][] chords, jump;
        private PadBinding previousBind;
        private readonly Dictionary<PadInstance, DashInput> devices = new Dictionary<PadInstance, DashInput>();
        private bool readFailed;
        private static readonly System.Reflection.FieldInfo Overlay = typeof(PadInstance).GetField("_steam_overlay_active", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        internal void Reset() { known = false; previous = null; devices.Clear(); }
        internal DashPress Read(bool nativePress)
        {
            if (Settings.Current.DashBindings == null || Settings.Current.DashBindings.Length == 0)
            { Reset(); return new DashPress(nativePress, true); }
            if (Game1.instance == null || !Game1.instance.IsActive || (bool)Overlay.GetValue(null))
            { Reset(); return default(DashPress); }
            return ReadDevices(BindingSnapshot.Registered(), nativePress);
        }
        internal DashPress ReadDevices(IList<PadInstance> pads, bool nativePress)
        {
            bool ordinary = false, custom = false, shared = false;
            foreach (var stale in devices.Keys.Where(p => !pads.Contains(p)).ToArray()) devices.Remove(stale);
            foreach (var pad in pads)
            {
                DashInput reader;
                if (!devices.TryGetValue(pad, out reader)) devices.Add(pad, reader = new DashInput());
                try
                {
                    if (!pad.IsValid || !pad.IsConnected || pad.GetBind().disabled) { reader.Reset(); continue; }
                    if (DashBindings.Custom(pad.GetPad().GetSaveIdentifier()) == null)
                    { reader.Reset(); ordinary |= nativePress && pad.GetPressed().jump; continue; }
                    var result = reader.ReadDevice(pad);
                    reader.readFailed = false;
                    custom |= result.Pressed; shared |= result.SharedJump;
                }
                catch (Exception error)
                {
                    reader.Reset();
                    if (!reader.readFailed) WarpDiagnostics.Write("Dash input unavailable: " + error.Message);
                    reader.readFailed = true;
                }
            }
            // A distinct custom activation takes precedence over a simultaneous
            // Jump on another device; that Jump remains a native buffer.
            return custom ? new DashPress(true, shared) : new DashPress(ordinary, ordinary);
        }
        private DashPress ReadDevice(PadInstance pad)
        {
            if (!ReferenceEquals(previous, pad) || !ReferenceEquals(previousBind, pad.GetBind()) || revision != DashBindings.Revision || bindingsEpoch != PhysicalBindings.Epoch)
            {
                previous = pad; previousBind = pad.GetBind(); revision = DashBindings.Revision; bindingsEpoch = PhysicalBindings.Epoch; known = false;
                chords = DashBindings.Get(pad);
                jump = PhysicalBindings.ResolvePhysicalBinding(pad, pad.GetBind().jump);
            }
            return Observe(pad.GetPad().GetPressedButtons(), chords, jump);
        }
        internal DashPress Observe(int[] buttons, int[][] dash, int[][] jumpButtons)
        {
            bool held = dash.Any(c => Held(c, buttons));
            bool press = known && held && !down;
            known = true; down = held;
            bool shared = press && dash.Any(c => Held(c, buttons)
                && jumpButtons.Any(j => Held(j, buttons) && c.Intersect(j).Any()));
            return new DashPress(press, shared);
        }
        private static bool Held(int[] chord, int[] buttons)
        { return chord != null && chord.Length > 0 && chord.All(b => Array.IndexOf(buttons, b) >= 0); }
    }
}
