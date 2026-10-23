using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using JKRuntime.Settings;
using JKRuntime.UI;

namespace SmoothCamera
{
    public enum FollowMode { JumpKing, Direct, Window, Screen }
    public enum CameraTrigger { Hold, Press, Both }

    [Serializable]
    public sealed class AxisSettings
    {
        [XmlAttribute("mode")] public FollowMode Mode = FollowMode.JumpKing;
        [XmlAttribute("focus")] public float Focus = .5f;
        [XmlAttribute("window")] public float Window = 80;
        [XmlAttribute("negativeBand")] public float NegativeBand = 12;
        [XmlAttribute("positiveBand")] public float PositiveBand = 64;
        [XmlAttribute("negativeResponse")] public float NegativeResponse = 12;
        [XmlAttribute("positiveResponse")] public float PositiveResponse = 12;
        [XmlAttribute("fastAssist")] public bool FastAssist = true;
        [XmlAttribute("lookAhead")] public float LookAhead;
        [XmlAttribute("lookDelay")] public float LookDelay = .15f;
        [XmlAttribute("recenter")] public bool Recenter;
        [XmlAttribute("recenterDelay")] public float RecenterDelay = .8f;
        internal AxisSettings Copy() { return (AxisSettings)MemberwiseClone(); }
        internal static AxisSettings HorizontalDefault()
        { return new AxisSettings { NegativeBand = 24, PositiveBand = 24, Window = 48, FastAssist = false }; }
        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(FollowMode), Mode)) throw new InvalidDataException("Unknown camera follow mode");
            Range(Focus, .15f, .85f, "focus"); Range(Window, 0, 240, "window");
            Range(NegativeBand, 0, 120, "negativeBand"); Range(PositiveBand, 0, 120, "positiveBand");
            Range(NegativeResponse, 2, 60, "negativeResponse"); Range(PositiveResponse, 2, 60, "positiveResponse");
            Range(LookAhead, 0, 96, "lookAhead"); Range(LookDelay, 0, 1, "lookDelay");
            Range(RecenterDelay, 0, 5, "recenterDelay");
        }
        internal static void Range(float value, float min, float max, string name)
        { if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max) throw new InvalidDataException("Invalid camera " + name); }
    }

    [Serializable]
    public class CameraProfile
    {
        public bool Horizontal;
        public AxisSettings Vertical = new AxisSettings();
        public AxisSettings HorizontalAxis = AxisSettings.HorizontalDefault();
        public float LookMargin = 48;
        public float HoldMilliseconds = 250;
        public string Preset = "Classic";
        internal CameraProfile CopyProfile()
        {
            return new CameraProfile { Horizontal = Horizontal, Vertical = Vertical.Copy(), HorizontalAxis = HorizontalAxis.Copy(),
                LookMargin = LookMargin, HoldMilliseconds = HoldMilliseconds, Preset = Preset };
        }
        internal void TakeProfile(CameraProfile value)
        {
            Horizontal = value.Horizontal; Vertical = value.Vertical.Copy(); HorizontalAxis = value.HorizontalAxis.Copy();
            LookMargin = value.LookMargin; HoldMilliseconds = value.HoldMilliseconds; Preset = value.Preset;
        }
        internal void ValidateProfile()
        {
            if (Vertical == null || HorizontalAxis == null) throw new InvalidDataException("Camera axes are required");
            Vertical.Validate(); HorizontalAxis.Validate();
            AxisSettings.Range(LookMargin, 24, 144, "look margin"); AxisSettings.Range(HoldMilliseconds, 100, 750, "hold time");
        }
        internal static CameraProfile FromPreset(string name)
        {
            var result = new CameraProfile(); result.Preset = name;
            if (name == "Centered") { result.Vertical.Mode = result.HorizontalAxis.Mode = FollowMode.Direct; }
            else if (name == "Platformer Window")
            { result.Vertical.Mode = result.HorizontalAxis.Mode = FollowMode.Window; result.Vertical.Focus = .6f; result.Vertical.Window = 100; result.HorizontalAxis.Window = 120; }
            else if (name != "Classic") throw new ArgumentException("Unknown camera preset");
            return result;
        }
    }

    [Serializable] public sealed class SavedMapProfile
    {
        public string Key;
        public CameraProfile Profile = new CameraProfile();
    }

    [Serializable]
    public sealed class CameraSettings : CameraProfile
    {
        public int Version = 2;
        public bool Smooth = true;
        public bool HighRefresh = true;
        public bool AutomaticallyDisabled;
        public string DisabledByMap;
        public bool UseMapRecommendations = true;
        public bool Diagnostics;
        public CameraTrigger UpTrigger, DownTrigger, FocusTrigger;
        public List<FocusBinding> FocusBindings = new List<FocusBinding>();
        public List<FocusBinding> UpBindings = new List<FocusBinding>();
        public List<FocusBinding> DownBindings = new List<FocusBinding>();
        public List<SavedMapProfile> MapProfiles = new List<SavedMapProfile>();
        internal CameraSettings Copy()
        {
            var next = new CameraSettings { Version = Version, Smooth = Smooth, HighRefresh = HighRefresh,
                AutomaticallyDisabled = AutomaticallyDisabled, DisabledByMap = DisabledByMap,
                UseMapRecommendations = UseMapRecommendations, Diagnostics = Diagnostics,
                UpTrigger = UpTrigger, DownTrigger = DownTrigger, FocusTrigger = FocusTrigger };
            next.TakeProfile(this);
            foreach (CameraMode action in CameraControls.Actions)
            foreach (var binding in Bindings(action) ?? new List<FocusBinding>())
            {
                if (binding == null) continue;
                var copy = new FocusBinding { Device = binding.Device };
                foreach (var chord in binding.Chords ?? new List<CameraChord>())
                    if (chord != null) copy.Chords.Add(new CameraChord { Buttons = chord.Buttons == null ? new int[0] : (int[])chord.Buttons.Clone() });
                next.Bindings(action).Add(copy);
            }
            foreach (var map in MapProfiles ?? new List<SavedMapProfile>())
                if (map != null) next.MapProfiles.Add(new SavedMapProfile { Key = map.Key, Profile = map.Profile.CopyProfile() });
            return next;
        }
        internal List<FocusBinding> Bindings(CameraMode action)
        { return action == CameraMode.Up ? UpBindings : action == CameraMode.Down ? DownBindings : FocusBindings; }
        internal CameraTrigger Trigger(CameraMode action)
        { return action == CameraMode.Up ? UpTrigger : action == CameraMode.Down ? DownTrigger : FocusTrigger; }
        internal void SetTrigger(CameraMode action, CameraTrigger value)
        { if (action == CameraMode.Up) UpTrigger = value; else if (action == CameraMode.Down) DownTrigger = value; else FocusTrigger = value; }
        internal CameraProfile ForMap(string key)
        {
            foreach (var map in MapProfiles ?? new List<SavedMapProfile>())
                if (map != null && string.Equals(map.Key, key, StringComparison.OrdinalIgnoreCase)) return map.Profile;
            return this;
        }
        internal static void Validate(CameraSettings value)
        {
            if (value.Version != 2) throw new InvalidDataException("Unsupported camera settings version");
            value.ValidateProfile();
            foreach (CameraMode action in CameraControls.Actions)
                if (!Enum.IsDefined(typeof(CameraTrigger), value.Trigger(action))) throw new InvalidDataException("Unknown camera input mode");
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var map in value.MapProfiles ?? new List<SavedMapProfile>())
            {
                if (map == null || string.IsNullOrWhiteSpace(map.Key) || map.Profile == null || !keys.Add(map.Key))
                    throw new InvalidDataException("Invalid or duplicate camera map profile");
                map.Profile.ValidateProfile();
            }
        }
    }

    internal static class Settings
    {
        private static bool loaded;
        private static SettingsFile<CameraSettings> file;
        internal static CameraSettings Current = new CameraSettings();
        internal static readonly Setting<bool> Toggle = new Setting<bool>("smooth-camera.enabled", "Enable",
            delegate { Load(); return Current.Smooth; }, Set, ApplyPresentationSetting);
        // stable setting IDs remain available to existing bindings and integrations
        internal static readonly Setting<bool> HorizontalToggle = new Setting<bool>("smooth-camera.horizontal", "Horizontal",
            delegate { Load(); return Current.Horizontal; }, SetHorizontal, ApplyPresentationSetting);
        internal static readonly Setting<bool> RefreshToggle = new Setting<bool>("smooth-camera.high-refresh", "240 Hz",
            delegate { Load(); return Current.HighRefresh; }, SetHighRefresh);
        private static string PathName { get { return Path.Combine(JKRuntime.PackageHost.GetDataDirectory(Assembly.GetExecutingAssembly()), "SmoothCamera.Settings.xml"); } }
        internal static void Load()
        {
            if (loaded) return;
            file = new SettingsFile<CameraSettings>(PathName, delegate { return new CameraSettings(); }, CameraSettings.Validate);
            Current = file.Value; loaded = true;
        }
        internal static void Save(CameraSettings next) { Load(); file.Save(next); Current = next; }
        internal static void Set(bool enabled)
        {
            Load();
            JKRuntime.Gameplay.MapMechanics.RequireUserEnable(MapPolicy.MechanicId, enabled);
            if (enabled && MapPolicy.Current.DisableOnEnter) throw new InvalidOperationException("This map forces native camera outside marked areas");
            var next = Current.Copy(); next.Smooth = enabled; next.AutomaticallyDisabled = false; next.DisabledByMap = null; Save(next);
        }
        internal static CameraSettings DisableForMap(CameraSettings current, string key)
        { var next = current.Copy(); next.Smooth = false; next.AutomaticallyDisabled = true; next.DisabledByMap = key; return next; }
        internal static void DisableOnEntry(string key)
        {
            Load(); if (!Current.Smooth) return;
            Save(DisableForMap(Current, key));
        }
        private static void SetHorizontal(bool enabled) { Load(); var next = Current.Copy(); next.Horizontal = enabled; Save(next); }
        private static void SetHighRefresh(bool enabled) { Load(); var next = Current.Copy(); next.HighRefresh = enabled; Save(next); }
        internal static CameraSettings WithFocus(CameraSettings current, string device, UiChord[] chords)
        { return WithBinding(current, CameraMode.Focus, device, chords); }
        internal static CameraSettings WithBinding(CameraSettings current, CameraMode action, string device, UiChord[] chords)
        {
            var next = current.Copy(); var bindings = next.Bindings(action); bindings.RemoveAll(b => b.Device == device);
            // null restores inherited defaults; an empty list explicitly unbinds
            if (chords == null) return next;
            var replacement = new FocusBinding { Device = device };
            foreach (var chord in chords ?? new UiChord[0])
                if (chord != null && !chord.IsEmpty) replacement.Chords.Add(new CameraChord { Buttons = chord.Buttons });
            bindings.Add(replacement); return next;
        }
        internal static void SetFocus(string device, UiChord[] chords) { Load(); Save(WithFocus(Current, device, chords)); }
        internal static void ApplyPresentationSetting()
        {
            Renderer.SettingsChanged();
            if (Renderer.Running || Hooks.Installed)
            { if (MapPolicy.NeedsHooks) Hooks.Install(); else Hooks.Uninstall(); }
        }
    }
}
