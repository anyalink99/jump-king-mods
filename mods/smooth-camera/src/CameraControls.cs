using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using JKRuntime.Input;
using JKRuntime.UI;
using JumpKing;
using JumpKing.Controller;

namespace SmoothCamera
{
    [Serializable] public sealed class CameraChord { public int[] Buttons = new int[0]; }
    [Serializable] public sealed class FocusBinding
    {
        public string Device;
        public List<CameraChord> Chords = new List<CameraChord>();
    }

    internal static class CameraControls
    {
        internal const string FocusId = "smooth-camera.focus";
        internal const string KeyboardDevice = "pc_keyboard_jump_king";
        internal static readonly CameraGestures Gestures = new CameraGestures();
        private static readonly FieldInfo Overlay = typeof(PadInstance).GetField("_steam_overlay_active", BindingFlags.Static | BindingFlags.NonPublic);
        private static UiRegistrationScope registration;
        internal static UiBindingDefinition Binding { get; private set; }
        private static CameraSettings cachedSettings;
        private static readonly Dictionary<string, int[][]> focus = new Dictionary<string, int[][]>(StringComparer.Ordinal);

        internal static void Register()
        {
            if (registration != null) return;
            var scope = new UiRegistrationScope("smooth-camera");
            try
            {
                Binding = new UiBindingDefinition(FocusId, "Smooth Camera", "Focus Camera",
                    GetChords, SetChords, delegate { SetChords(MainDevice() == KeyboardDevice ? new[] { new UiChord(70) } : new UiChord[0]); });
                scope.RegisterBinding(Binding);
                registration = scope;
            }
            catch { scope.Dispose(); throw; }
        }
        internal static void Unload()
        {
            if (registration != null) registration.Dispose();
            registration = null; Binding = null; cachedSettings = null; focus.Clear(); Gestures.Reset();
        }
        private static string MainDevice()
        {
            var pads = BindingSnapshot.Registered();
            if (pads.Count == 0) return null;
            return ControllerManager.instance.GetMain().GetPad().GetSaveIdentifier();
        }
        private static UiChord[] GetChords()
        {
            Settings.Load(); string device = MainDevice(); RefreshBindings();
            int[][] values;
            if (device == null || !focus.TryGetValue(device, out values)) return new UiChord[0];
            var result = new UiChord[values.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new UiChord(values[i]);
            return result;
        }
        private static void SetChords(UiChord[] chords)
        {
            Settings.Load(); string device = MainDevice();
            if (device == null) throw new InvalidOperationException("No input device selected");
            Settings.SetFocus(device, chords); Gestures.Suspend();
        }
        private static void RefreshBindings()
        {
            if (ReferenceEquals(cachedSettings, Settings.Current)) return;
            focus.Clear();
            // Legacy settings have no profile yet. An explicitly cleared profile
            // below overrides this default, so clearing F survives a reload.
            focus[KeyboardDevice] = new[] { new[] { 70 } };
            foreach (var binding in Settings.Current.FocusBindings ?? new List<FocusBinding>())
            {
                if (binding == null || string.IsNullOrEmpty(binding.Device)) continue;
                var chords = new List<int[]>();
                foreach (var chord in binding.Chords ?? new List<CameraChord>())
                {
                    if (chord == null || chord.Buttons == null || chord.Buttons.Length > 2) continue;
                    var normalized = new UiChord(chord.Buttons);
                    if (!normalized.IsEmpty) chords.Add(normalized.Buttons);
                }
                focus[binding.Device] = chords.ToArray();
            }
            cachedSettings = Settings.Current;
        }
        internal static bool Match(int[][] chords, int[] buttons)
        {
            foreach (var chord in chords)
            {
                bool held = chord.Length > 0;
                foreach (int button in chord) if (Array.IndexOf(buttons, button) < 0) { held = false; break; }
                if (held) return true;
            }
            return false;
        }
        internal static void ReadPads(out bool up, out bool down, out bool focused)
        {
            up = down = focused = false; RefreshBindings();
            // Native pad states already reflect keyboard/controller rebinding and
            // Runtime's chord layer. Do not consume presses or poll connectivity.
            foreach (var pad in BindingSnapshot.Registered())
            {
                if (!pad.IsValid || pad.GetBind() == null || !pad.GetBind().Enabled) continue;
                var state = pad.GetState(); up |= state.up; down |= state.down;
                int[][] chords;
                if (focus.TryGetValue(pad.GetPad().GetSaveIdentifier(), out chords) && chords.Length > 0)
                    focused |= Match(chords, pad.GetPad().GetPressedButtons());
            }
        }
        internal static void Update(bool available)
        {
            available = available && ControllerManager.instance != null && Game1.instance != null && Game1.instance.IsActive && !UIApi.IsOpen
                && Overlay != null && !(bool)Overlay.GetValue(null);
            if (!available) { Gestures.Suspend(); return; }
            bool up, down, focused; ReadPads(out up, out down, out focused);
            // An explicit Focus chord may contain Up/Down; the named action wins.
            Gestures.Update(up && !focused, down && !focused, focused,
                Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, true);
        }
    }
}
