using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    public enum CameraRule { [XmlEnum("inherit")] Inherit, [XmlEnum("smooth")] Smooth, [XmlEnum("native")] Native }
    public enum CameraTransition { [XmlEnum("snap")] Snap, [XmlEnum("smooth")] Smooth }
    [Serializable] public sealed class MapCameraProfile
    {
        [XmlAttribute("id")] public string Id;
        [XmlElement("Settings")] public CameraProfile Settings = new CameraProfile();
    }
    [Serializable] public class CameraControlRules
    {
        [XmlAttribute("allow-focus")] public bool AllowFocus = true;
        [XmlAttribute("allow-look")] public bool AllowLook = true;
    }
    [Serializable] public class ScreenRule : CameraControlRules
    {
        [XmlAttribute("from")] public int From;
        [XmlAttribute("to")] public int To;
        [XmlAttribute("mode")] public CameraRule Mode;
        [XmlAttribute("profile")] public string Profile;
        [XmlAttribute("transition")] public CameraTransition Transition;
    }
    [Serializable] public sealed class CameraZone : CameraControlRules
    {
        [XmlAttribute("id")] public string Id;
        [XmlAttribute("screen")] public int Screen;
        [XmlAttribute("x")] public int X;
        [XmlAttribute("y")] public int Y;
        [XmlAttribute("width")] public int Width;
        [XmlAttribute("height")] public int Height;
        [XmlAttribute("priority")] public int Priority;
        [XmlAttribute("mode")] public CameraRule Mode;
        [XmlAttribute("profile")] public string Profile;
        [XmlAttribute("transition")] public CameraTransition Transition;
        internal Rectangle Bounds { get { return new Rectangle(X, Y, Width, Height); } }
    }
    [Serializable, XmlRoot("SmoothCamera")] public sealed class CameraMapFile : CameraControlRules
    {
        [XmlAttribute("version")] public int Version = 1;
        [XmlElement("Profile")] public MapCameraProfile[] Profiles = new MapCameraProfile[0];
        [XmlElement("Screens")] public ScreenRule[] Screens = new ScreenRule[0];
        [XmlElement("Zone")] public CameraZone[] Zones = new CameraZone[0];
    }
    internal struct CameraDecision
    {
        internal bool Active, Forced, AllowFocus, AllowLook;
        internal int First, Last;
        internal object Region;
        internal CameraProfile Profile;
        internal CameraTransition Transition;
        internal string Reason;
    }
    internal sealed class MapRules
    {
        internal string Key = "", Error = "";
        internal bool ExplicitOnly, DisableOnEnter, HasForced;
        internal bool AllowFocus = true, AllowLook = true;
        internal ScreenRule[] Screens = new ScreenRule[0];
        internal CameraZone[] Zones = new CameraZone[0];
        private readonly Dictionary<string, CameraProfile> profiles = new Dictionary<string, CameraProfile>(StringComparer.Ordinal);
        private bool[] coverage;
        private int[] first, last;
        private bool coverageEnabled, coverageRestricted, coverageStrict;
        private long coverageGeneration = -1;
        internal void Prepare(CameraSettings settings) { PrepareCoverage(settings, Screens.Length); }
        private void PrepareCoverage(CameraSettings settings, int count)
        {
            if (coverage != null && coverage.Length == count && coverageEnabled == settings.Smooth && coverageRestricted == ExplicitOnly && coverageStrict == DisableOnEnter && coverageGeneration == JKRuntime.Gameplay.MapMechanics.Generation) return;
            coverageGeneration = JKRuntime.Gameplay.MapMechanics.Generation;
            coverageEnabled = settings.Smooth; coverageRestricted = ExplicitOnly; coverageStrict = DisableOnEnter;
            coverage = new bool[count]; first = new int[count]; last = new int[count];
            for (int i = 0; i < count; i++)
            {
                var rule = i < Screens.Length ? Screens[i] : null;
                coverage[i] = Error.Length == 0 && (rule != null && rule.Mode != CameraRule.Inherit ? rule.Mode == CameraRule.Smooth : settings.Smooth && !ExplicitOnly && !DisableOnEnter);
                coverage[i] = Error.Length == 0 && JKRuntime.Gameplay.MapMechanics.Resolve(MapPolicy.MechanicId, i, Rectangle.Empty, coverage[i]).Enabled;
            }
            foreach (var zone in Zones) if (zone.Mode == CameraRule.Native && zone.Screen <= count) coverage[zone.Screen - 1] = false;
            for (int i = 0; i < count; i++) first[i] = i > 0 && coverage[i] && coverage[i - 1] ? first[i - 1] : i;
            for (int i = count - 1; i >= 0; i--) last[i] = i + 1 < count && coverage[i] && coverage[i + 1] ? last[i + 1] : i;
        }
        internal static MapRules Parse(TextReader text, int count, bool explicitOnly, bool disableOnEnter)
        {
            if (count < 0 || count > 100000) throw new InvalidDataException("Invalid native screen count");
            var result = new MapRules { ExplicitOnly = explicitOnly, DisableOnEnter = disableOnEnter, Screens = new ScreenRule[count] };
            if (text == null) return result;
            var serializer = new XmlSerializer(typeof(CameraMapFile));
            serializer.UnknownElement += delegate(object sender, XmlElementEventArgs e) { throw new InvalidDataException("Unknown camera element: " + e.Element.Name); };
            serializer.UnknownAttribute += delegate(object sender, XmlAttributeEventArgs e) { throw new InvalidDataException("Unknown camera attribute: " + e.Attr.Name); };
            CameraMapFile map;
            using (var reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 }))
                map = (CameraMapFile)serializer.Deserialize(reader);
            if (map.Version != 1) throw new InvalidDataException("Unsupported SmoothCamera map version");
            result.AllowFocus = map.AllowFocus; result.AllowLook = map.AllowLook;
            map.Profiles = map.Profiles ?? new MapCameraProfile[0]; map.Screens = map.Screens ?? new ScreenRule[0]; map.Zones = map.Zones ?? new CameraZone[0];
            foreach (var profile in map.Profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Id) || result.profiles.ContainsKey(profile.Id) || profile.Settings == null)
                    throw new InvalidDataException("Invalid or duplicate camera profile");
                profile.Settings.ValidateProfile(); result.profiles.Add(profile.Id, profile.Settings);
            }
            foreach (var rule in map.Screens)
            {
                if (rule.To == 0) rule.To = rule.From;
                if (rule.From < 1 || rule.To < rule.From || rule.To > count) throw new InvalidDataException("Invalid camera screen range");
                result.CheckProfile(rule.Profile);
                for (int i = rule.From - 1; i < rule.To; i++)
                { if (result.Screens[i] != null) throw new InvalidDataException("Overlapping camera screen ranges"); result.Screens[i] = rule; }
                result.HasForced |= rule.Mode == CameraRule.Smooth;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var zone in map.Zones)
            {
                if (string.IsNullOrWhiteSpace(zone.Id) || !ids.Add(zone.Id) || zone.Screen < 1 || zone.Screen > count
                    || zone.Width <= 0 || zone.Height <= 0 || zone.X < 0 || zone.Y < 0 || zone.Width > 480 || zone.Height > 360
                    || zone.X > 480 - zone.Width || zone.Y > 360 - zone.Height) throw new InvalidDataException("Invalid camera zone");
                result.CheckProfile(zone.Profile);
                foreach (var other in map.Zones)
                    if (!ReferenceEquals(zone, other) && zone.Screen == other.Screen && zone.Priority == other.Priority && zone.Bounds.Intersects(other.Bounds))
                        throw new InvalidDataException("Overlapping camera zones need different priorities");
                result.HasForced |= zone.Mode == CameraRule.Smooth;
            }
            result.Zones = map.Zones;
            return result;
        }
        private void CheckProfile(string name)
        { if (!string.IsNullOrEmpty(name) && !profiles.ContainsKey(name)) throw new InvalidDataException("Unknown camera profile: " + name); }
        internal bool FullScreenActive(CameraSettings settings, int screen)
        {
            if (screen < 0 || (Screens.Length > 0 && screen >= Screens.Length) || Error.Length > 0) return false;
            if (Screens.Length > 0) { PrepareCoverage(settings, Screens.Length); return coverage[screen]; }
            foreach (var zone in Zones) if (zone.Screen == screen + 1 && zone.Mode == CameraRule.Native) return false;
            var rule = screen < Screens.Length ? Screens[screen] : null;
            if (rule != null && rule.Mode != CameraRule.Inherit) return rule.Mode == CameraRule.Smooth;
            return JKRuntime.Gameplay.MapMechanics.Resolve(MapPolicy.MechanicId, screen, Rectangle.Empty, settings.Smooth && !ExplicitOnly && !DisableOnEnter).Enabled;
        }
        internal CameraDecision Resolve(CameraSettings settings, int screen, float x, float localY, int count)
        {
            var result = new CameraDecision { First = screen, Last = screen, Profile = settings.ForMap(Key), Reason = "Disabled by you",
                AllowFocus = AllowFocus, AllowLook = AllowLook };
            if (screen < 0 || screen >= count || Error.Length > 0) { result.Reason = Error.Length > 0 ? "Invalid map rules (native camera)" : "No active screen"; return result; }
            ScreenRule rule = screen < Screens.Length ? Screens[screen] : null;
            CameraRule mode = rule == null ? CameraRule.Inherit : rule.Mode;
            string profile = rule == null ? null : rule.Profile;
            result.Region = rule; result.Transition = rule == null ? CameraTransition.Snap : rule.Transition;
            if (rule != null) { result.AllowFocus &= rule.AllowFocus; result.AllowLook &= rule.AllowLook; }
            CameraZone selected = null;
            foreach (var zone in Zones)
                if (zone.Screen == screen + 1 && x >= zone.X && x < zone.X + zone.Width && localY >= zone.Y && localY < zone.Y + zone.Height
                    && (selected == null || zone.Priority > selected.Priority)) selected = zone;
            if (selected != null)
            {
                if (selected.Mode != CameraRule.Inherit) mode = selected.Mode;
                if (!string.IsNullOrEmpty(selected.Profile)) profile = selected.Profile;
                result.Region = selected; result.Transition = selected.Transition;
                result.AllowFocus &= selected.AllowFocus; result.AllowLook &= selected.AllowLook;
            }
            result.Forced = mode == CameraRule.Smooth;
            result.Active = result.Forced || (mode != CameraRule.Native && settings.Smooth && !ExplicitOnly && !DisableOnEnter);
            var permission = JKRuntime.Gameplay.MapMechanics.Resolve(MapPolicy.MechanicId, screen, new Rectangle((int)x, (int)localY, 1, 1),
                result.Active, selected != null && mode == CameraRule.Smooth);
            result.Active = permission.Enabled;
            result.AllowFocus &= permission.Parameter("allow-focus", "true") == "true";
            result.AllowLook &= permission.Parameter("allow-look", "true") == "true";
            if (permission.Mode != JKRuntime.Gameplay.MapMechanicMode.Inherit || permission.Controlled)
                result.Forced = permission.Enabled && permission.Authored;
            if (settings.UseMapRecommendations && ReferenceEquals(result.Profile, settings) && !string.IsNullOrEmpty(profile)) result.Profile = profiles[profile];
            result.Reason = result.Forced ? "Forced by map" : mode == CameraRule.Native ? "Native area" : DisableOnEnter ? "Disabled on map entry"
                : ExplicitOnly ? "Restricted by map" : result.Active ? "Active" : settings.AutomaticallyDisabled ? "Disabled on map entry" : "Disabled by you";
            if (permission.Controlled || permission.Mode != JKRuntime.Gameplay.MapMechanicMode.Inherit) result.Reason = permission.Reason;
            if (result.Active && FullScreenActive(settings, screen))
            {
                PrepareCoverage(settings, count);
                result.First = first[screen]; result.Last = last[screen];
            }
            return result;
        }
    }
    internal static class MapPolicy
    {
        internal const string MechanicId = "smooth-camera.tracking";
        internal static MapRules Current = new MapRules();
        private static string enteredKey;
        internal static bool NeedsHooks { get { return Settings.Current.Smooth || Current.HasForced || JKRuntime.Gameplay.MapMechanics.HasRules(MechanicId) || Settings.Current.Diagnostics; } }
        internal static void Reset() { Current = new MapRules(); enteredKey = null; }
        internal static void LoadLevel()
        {
            if (Game1.instance == null || Game1.instance.contentManager == null || string.IsNullOrEmpty(Game1.instance.contentManager.root)) return;
            var content = Game1.instance.contentManager;
            string key = Path.GetFullPath(content.root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool explicitOnly = content.level != null && HasTag(content.level.Info.Tags, "SmoothCameraExplicitOnly");
            bool disable = content.level != null && HasTag(content.level.Info.Tags, "SmoothCameraDisableOnEnter");
            string path = Path.Combine(key, "props/smooth-camera/camera.xml");
            try
            {
                using (var reader = File.Exists(path) ? File.OpenText(path) : null)
                    Current = MapRules.Parse(reader, LevelManager.TotalScreens, explicitOnly, disable);
            }
            catch (Exception error)
            {
                Current = new MapRules { ExplicitOnly = explicitOnly, DisableOnEnter = disable, Error = error.GetBaseException().Message };
                Console.WriteLine("[Smooth Camera] Invalid map rules; using native camera: " + Current.Error);
            }
            Adopt(Current, key, Settings.DisableOnEntry);
            for (int i = 0; i < Current.Screens.Length; i++) if (Current.Screens[i] != null && Current.Screens[i].Mode != CameraRule.Inherit)
                JKRuntime.Gameplay.MapMechanics.ImportScreen(MechanicId, i,
                    Current.Screens[i].Mode == CameraRule.Smooth ? JKRuntime.Gameplay.MapMechanicMode.On : JKRuntime.Gameplay.MapMechanicMode.Off, "camera.xml");
        }
        internal static void Adopt(MapRules rules, string key, Action<string> disable)
        {
            Current = rules; Current.Key = key;
            if (!string.Equals(enteredKey, key, StringComparison.OrdinalIgnoreCase))
            {
                if (rules.DisableOnEnter) disable(key);
                enteredKey = key;
            }
            Current.Prepare(Settings.Current);
        }
        internal static int PortalDestination(LevelScreen[] screens, int source, bool left, float y = float.NaN)
        {
            int target = JKRuntime.Geometry.MapTopology.PreviewDestination(screens, source, left, y);
            return target >= 0 && Current.FullScreenActive(Settings.Current, source) && Current.FullScreenActive(Settings.Current, target) ? target : -1;
        }
        private static bool HasTag(Array tags, string name)
        { if (tags != null) foreach (var tag in tags) if (string.Equals(tag as string, name, StringComparison.Ordinal)) return true; return false; }
    }
}
