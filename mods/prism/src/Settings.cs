using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using JKRuntime;
using JKRuntime.Settings;

namespace Prism
{
    [Serializable] public sealed class ThemePreferences
    {
        public string Id = "event-horizon";
        public bool Reflections = true;
        public bool Gentle;
        public bool OverlayOnly;
        public bool DisableProps = true;
        public int MusicVolume = 80;
        public int Glow = 100;
        public int WindVisibility = 100;
        internal ThemePreferences Copy() { return (ThemePreferences)MemberwiseClone(); }
    }
    [Serializable] public sealed class Preferences
    {
        public int Version = 1;
        public bool Enabled = true;
        public string SelectedTheme = "event-horizon";
        public List<ThemePreferences> Themes;
        // Read the 0.1 keys indefinitely; new saves use individual theme profiles.
        public bool Reflections = true;
        public bool Gentle;
        public bool OverlayOnly;
        public bool ShouldSerializeReflections() { return false; }
        public bool ShouldSerializeGentle() { return false; }
        public bool ShouldSerializeOverlayOnly() { return false; }
        internal ThemePreferences For(string id) { return Themes.Find(t => t.Id == id); }
        [XmlIgnore] public ThemePreferences Current { get { return For(SelectedTheme); } }
        internal static Preferences Defaults() { var p = new Preferences(); Validate(p); return p; }
        internal static void Validate(Preferences p)
        {
            if (p.Version < 1 || p.Version > 2) throw new InvalidDataException("Unsupported Prism settings version");
            if (p.Themes == null || p.Themes.Count == 0) p.Themes = new List<ThemePreferences> { new ThemePreferences { Reflections = p.Reflections, Gentle = p.Gentle, OverlayOnly = p.OverlayOnly } };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var theme in p.Themes)
            {
                if (theme == null || string.IsNullOrWhiteSpace(theme.Id) || !ids.Add(theme.Id)) throw new InvalidDataException("Invalid or duplicate theme profile");
                if (theme.MusicVolume < 0 || theme.MusicVolume > 100 || theme.Glow < 0 || theme.Glow > 150 || theme.WindVisibility < 50 || theme.WindVisibility > 150) throw new InvalidDataException("Theme value is outside its range");
            }
            foreach (var theme in ThemeCatalog.All) if (p.For(theme.Id) == null) p.Themes.Add(new ThemePreferences { Id = theme.Id });
            if (ThemeCatalog.Find(p.SelectedTheme) == null) throw new InvalidDataException("Selected Prism theme is unavailable");
            p.Version = 2;
        }
        internal Preferences Copy()
        {
            var p = (Preferences)MemberwiseClone(); p.Themes = new List<ThemePreferences>();
            foreach (var theme in Themes) p.Themes.Add(theme.Copy()); return p;
        }
    }
    internal static class Settings
    {
        internal static SettingsFile<Preferences> File;
        internal static int Revision;
        internal static Preferences Current { get { Load(); return File.Value; } }
        internal static string DirectoryPath { get { return PackageHost.GetDataDirectory(typeof(ModEntry).Assembly); } }
        internal static void Load() { if (File == null) File = new SettingsFile<Preferences>(Path.Combine(DirectoryPath, "Prism.Settings.xml"), Preferences.Defaults, Preferences.Validate); }
        internal static void Change(Action<Preferences> edit)
        {
            var next = Current.Copy(); edit(next); Preferences.Validate(next);
            ModEntry.SavePreferences(next); Revision++;
        }
        internal static void ChangeTheme(string id, Action<ThemePreferences> edit) { Change(p => edit(p.For(id))); }
    }
    internal sealed class ThemeDefinition
    {
        internal readonly string Id, Name, Description;
        internal ThemeDefinition(string id, string name, string description) { Id = id; Name = name; Description = description; }
    }
    internal static class ThemeCatalog
    {
        internal static readonly ThemeDefinition[] All = { new ThemeDefinition("event-horizon", "Event Horizon", "xi / .357 Magnum. Glass platforms, starlight and a living accretion disk.") };
        internal static ThemeDefinition Find(string id) { return Array.Find(All, t => t.Id == id); }
    }
}
