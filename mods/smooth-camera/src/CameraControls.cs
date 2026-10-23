using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        // keep the old XML item name for saved Focus profiles
        public string Device;
        public List<CameraChord> Chords = new List<CameraChord>();
    }

    internal static class CameraControls
    {
        internal const string FocusId = "smooth-camera.focus";
        internal const string UpId = "smooth-camera.up", DownId = "smooth-camera.down";
        internal static readonly CameraMode[] Actions = { CameraMode.Focus, CameraMode.Up, CameraMode.Down };
        internal static string Id(CameraMode action) { return action == CameraMode.Up ? UpId : action == CameraMode.Down ? DownId : FocusId; }
        internal static string Label(CameraMode action) { return action == CameraMode.Focus ? "Focus Camera" : "Look " + action; }
        internal const string KeyboardDevice = "pc_keyboard_jump_king";
        internal static readonly CameraGestures Gestures = new CameraGestures();
        private static readonly FieldInfo Overlay = typeof(PadInstance).GetField("_steam_overlay_active", BindingFlags.Static | BindingFlags.NonPublic);
        private static UiRegistrationScope registration;
        internal static UiBindingDefinition Binding { get; private set; }
        private static CameraSettings cachedSettings;
        private static readonly Dictionary<CameraMode, Dictionary<string, int[][]>> bindings = new Dictionary<CameraMode, Dictionary<string, int[][]>>();
        internal static UiBindingsPage Page(params string[] ids)
        { return new UiBindingsPage("Camera Binds", null, ids.Length == 0 ? Actions.Select(Id).ToArray() : ids) { ShowList = true }; }

        internal static void Register()
        {
            if (registration != null) return;
            var scope = new UiRegistrationScope("smooth-camera");
            try
            {
                foreach (CameraMode action in Actions)
                {
                    CameraMode captured = action;
                    var definition = new UiBindingDefinition(Id(action), "Smooth Camera", Label(action),
                        () => { Settings.Load(); return GetChords(Settings.Current, captured); },
                        chords => SetChords(captured, chords), () => SetChords(captured, null));
                    definition.Mode = new UiBindingModeOption(() => { Settings.Load(); return (UiBindingMode)Settings.Current.Trigger(captured); },
                        value => { Settings.Load(); var next = Settings.Current.Copy(); next.SetTrigger(captured, (CameraTrigger)value); Settings.Save(next); Gestures.Reset(); }, UiBindingMode.Hold, UiBindingMode.Hold, UiBindingMode.Press, UiBindingMode.Both);
                    if (action == CameraMode.Focus) Binding = definition;
                    scope.RegisterBinding(definition);
                }
                registration = scope;
            }
            catch { scope.Dispose(); throw; }
        }
        internal static void Unload()
        {
            if (registration != null) registration.Dispose();
            registration = null; Binding = null; cachedSettings = null; bindings.Clear(); Gestures.Reset();
        }
        internal static string MainDevice()
        {
            var pads = BindingSnapshot.Registered();
            if (pads.Count == 0) return null;
            return ControllerManager.instance.GetMain().GetPad().GetSaveIdentifier();
        }
        internal static UiChord[] GetChords(CameraSettings settings, CameraMode action)
        {
            string device = MainDevice();
            if (device == null) return new UiChord[0];
            foreach (var binding in settings.Bindings(action) ?? new List<FocusBinding>())
                if (binding != null && binding.Device == device)
                    return (binding.Chords ?? new List<CameraChord>()).Where(c => c != null && c.Buttons != null && c.Buttons.Length <= 2)
                        .Select(c => new UiChord(c.Buttons)).Where(c => !c.IsEmpty).ToArray();
            if (action == CameraMode.Focus) return device == KeyboardDevice ? new[] { new UiChord(70) } : new UiChord[0];
            // read Runtime's physical chords, not its virtual key codes
            var native = UIApi.GetBindings().FirstOrDefault(b => b.Id == "jump-king." + action.ToString().ToLowerInvariant());
            if (native != null) return native.GetChords();
            var pad = ControllerManager.instance.GetMain();
            return UiChord.FromAlternatives(pad.GetBind().GetButtonBind(action == CameraMode.Up ? JKpadButtons.Up : JKpadButtons.Down));
        }
        private static void SetChords(CameraMode action, UiChord[] chords)
        {
            Settings.Load(); string device = MainDevice();
            if (device == null) throw new InvalidOperationException("No input device selected");
            Settings.Save(Settings.WithBinding(Settings.Current, action, device, chords)); Gestures.Suspend();
        }
        private static void RefreshBindings()
        {
            if (ReferenceEquals(cachedSettings, Settings.Current)) return;
            bindings.Clear();
            foreach (CameraMode action in Actions)
            {
                var values = new Dictionary<string, int[][]>(StringComparer.Ordinal);
                // old settings have no profile yet, use this default
                // an explicitly cleared profile below wins, so clearing F survives reload
                if (action == CameraMode.Focus) values[KeyboardDevice] = new[] { new[] { 70 } };
                foreach (var binding in Settings.Current.Bindings(action) ?? new List<FocusBinding>())
                {
                    if (binding == null || string.IsNullOrEmpty(binding.Device)) continue;
                    var chords = new List<int[]>();
                    foreach (var chord in binding.Chords ?? new List<CameraChord>())
                    {
                        if (chord == null || chord.Buttons == null || chord.Buttons.Length > 2) continue;
                        var normalized = new UiChord(chord.Buttons);
                        if (!normalized.IsEmpty) chords.Add(normalized.Buttons);
                    }
                    values[binding.Device] = chords.ToArray();
                }
                bindings.Add(action, values);
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
            // native pad states already reflect keyboard/controller rebinding and
            // Runtime's chord layer. Don't consume presses or poll connectivity
            foreach (var pad in BindingSnapshot.Registered())
            {
                if (!pad.IsValid || pad.GetBind() == null || !pad.GetBind().Enabled) continue;
                var state = pad.GetState(); string device = pad.GetPad().GetSaveIdentifier();
                int[][] upChords, downChords, focusChords;
                bool customUp = bindings[CameraMode.Up].TryGetValue(device, out upChords);
                bool customDown = bindings[CameraMode.Down].TryGetValue(device, out downChords);
                bool customFocus = bindings[CameraMode.Focus].TryGetValue(device, out focusChords);
                var buttons = customUp || customDown || customFocus ? pad.GetPad().GetPressedButtons() : new int[0];
                up |= customUp ? Match(upChords, buttons) : state.up;
                down |= customDown ? Match(downChords, buttons) : state.down;
                focused |= customFocus && Match(focusChords, buttons);
            }
        }
        internal static void Update(bool available)
        {
            Gestures.SetTriggers(Settings.Current.UpTrigger, Settings.Current.DownTrigger, Settings.Current.FocusTrigger);
            available = available && ControllerManager.instance != null && Game1.instance != null && Game1.instance.IsActive && !UIApi.IsOpen
                && Overlay != null && !(bool)Overlay.GetValue(null);
            if (!available) { Gestures.Suspend(); return; }
            bool up, down, focused; ReadPads(out up, out down, out focused);
            // an explicit Focus chord may contain Up/Down, the named action wins
            Gestures.Update(up && !focused, down && !focused, focused,
                Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, true);
        }
    }
}
