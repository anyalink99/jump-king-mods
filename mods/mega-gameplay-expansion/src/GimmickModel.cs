using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JKRuntime.Settings;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    // Keep the original numeric/XML meanings. Auto applies only to newly created rules.
    public enum GimmickApplication { OrdinarySolid, SolidTerrain, ExistingBlocks, FillSpace, Overlay, Auto, Surface, FillEmpty, ReplaceMedium }
    public sealed class GimmickRule
    {
        public string Id { get; set; }
        public bool Enabled { get; set; }
        public GimmickApplication Application { get; set; }
        public int FirstScreen { get; set; }
        public int LastScreen { get; set; }
        public string Value { get; set; }
        public bool ConvertSlopes { get; set; }
        public string Contract { get; set; }
        public float WindStrength { get; set; }
        public bool WindImmediate { get; set; }
        public int[] Screens { get; set; }
        public string SourceId { get; set; }
        public string Name { get; set; }
    }
    internal sealed class GimmickEntry
    {
        internal string Id, Label, Owner, Kind, Detail, Error;
        internal Color? Colour;
        internal IBlock Template;
        internal JumpKing.API.IBlockFactory Factory;
        internal bool ConstructionAttempted;
        internal Func<bool> Read;
        internal Action<bool> Write;
        internal StateSlot Slot;
        internal bool Native;
        internal string Family = "Unknown", Geometry = "Unknown", Trigger = "Unknown", Classification;
    }
    internal static class Gimmicks
    {
        internal const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static readonly Dictionary<string, GimmickEntry> Entries = new Dictionary<string, GimmickEntry>(StringComparer.Ordinal);
        internal static readonly string[] DefaultPins = { "mega.warp", "mega.no-walk-off", "mega.air-dash" };
        internal static GimmickSession Session;
        internal static long Generation;
        internal static string Status = "Load a level to inspect live states and apply blocks.";
        internal static string TypeId(Type type) { return type.Assembly.GetName().Name + ":" + type.FullName; }
        internal static string BlockId(Type factory, Color colour)
        { return "block:" + TypeId(factory) + ":" + colour.PackedValue.ToString("x8"); }
        internal static string Human(string text)
        {
            return System.Text.RegularExpressions.Regex.Replace(text ?? "", "([a-z0-9])([A-Z])", "$1 $2").Replace('_', ' ');
        }
        internal static string Short(string text, int count) { return text.Length <= count ? text : text.Substring(0, count - 3) + "..."; }
        internal static void Add(GimmickEntry item)
        {
            GimmickEntry old;
            if (Entries.TryGetValue(item.Id, out old) && old.Template != null && item.Template == null) return;
            Entries[item.Id] = item; Generation++;
        }
        internal static void Initialize()
        {
            if (Entries.ContainsKey(DefaultPins[0])) return;
            AddNative(DefaultPins[0], Settings.Warp);
            AddNative(DefaultPins[1], Settings.EdgeStop);
            AddNative(DefaultPins[2], Settings.Dash);
            GimmickWind.AddEntry();
        }
        private static void AddNative(string id, Setting<bool> setting)
        {
            Add(new GimmickEntry { Id = id, Label = setting.Label, Owner = "Mega Gameplay Expansion", Kind = "Built-in",
                Native = true, Read = () => setting.Value, Write = value => setting.Set(value),
                Detail = "Global enable; authored surface, zone and screen activation remains independent." });
        }
        internal static string[] Pins { get { return Settings.Current.GimmickPins ?? DefaultPins; } }
        internal static void Pin(string id)
        {
            var pins = Pins.ToList(); if (!pins.Remove(id)) pins.Add(id);
            Settings.Edit(value => value.GimmickPins = pins.ToArray()); Generation++;
        }
        internal static GimmickRule Rule(string id)
        {
            return (Settings.Current.GimmickRules ?? new GimmickRule[0]).FirstOrDefault(r => r != null && r.Id == id)
                ?? new GimmickRule { Id = id, Application = GimmickApplication.Auto, WindStrength = 1f, WindImmediate = true, Value = id == GimmickWind.Id ? "Alternating" : null };
        }
        internal static GimmickRule Copy(GimmickRule rule)
        { return new GimmickRule { Id = rule.Id, Enabled = rule.Enabled, Application = rule.Application,
            FirstScreen = rule.FirstScreen, LastScreen = rule.LastScreen, Value = rule.Value, ConvertSlopes = rule.ConvertSlopes, Contract = rule.Contract,
            WindStrength = rule.WindStrength, WindImmediate = rule.WindImmediate, Screens = rule.Screens == null ? null : (int[])rule.Screens.Clone(), SourceId = rule.SourceId, Name = rule.Name }; }
        internal static bool Enabled(GimmickEntry entry)
        {
            try { return entry.Read != null ? entry.Read() : Session != null && Session.IsEnabled(entry.Id); }
            catch { return false; }
        }
        internal static void Toggle(GimmickEntry entry)
        {
            if (entry.Write != null) { entry.Write(!Enabled(entry)); Generation++; return; }
            var rule = Copy(Rule(entry.Id)); rule.Enabled = !Enabled(entry); Configure(rule);
        }
        internal static void Configure(GimmickRule rule)
        {
            if (!Enum.IsDefined(typeof(GimmickApplication), rule.Application) || rule.FirstScreen < 0 || rule.LastScreen < 0
                || (rule.LastScreen > 0 && rule.LastScreen < rule.FirstScreen) || (rule.Screens != null && rule.Screens.Any(s => s <= 0))) throw new ArgumentException("Invalid application range");
            GimmickEntry entry;
            if (Entries.TryGetValue(rule.Id, out entry) && entry.Slot != null) {
                if (rule.Enabled) entry.Slot.RequireWritable();
                rule.Contract = entry.Slot.Contract;
            }
            var previous = Settings.Current.GimmickRules;
            var next = (previous ?? new GimmickRule[0]).Where(r => r != null && r.Id != rule.Id).Select(Copy).ToList();
            next.Add(Copy(rule));
            if (Session == null && rule.Enabled) throw new InvalidOperationException("Start a level before enabling an override.");
            if (Session != null) Session.Apply(next.ToArray());
            try { Settings.Current.GimmickRules = next.ToArray(); Settings.Save(); }
            catch
            {
                Settings.Current.GimmickRules = previous;
                if (Session != null) Session.Apply(previous ?? new GimmickRule[0]);
                throw;
            }
            Generation++;
        }
        internal static void AddConfiguration(GimmickRule rule)
        {
            GimmickEntry source;
            if (string.IsNullOrEmpty(rule.SourceId) || !Entries.TryGetValue(rule.SourceId, out source)) return;
            if (source.Kind != "Block" && source.Kind != "Wind") throw new InvalidOperationException("Only block and wind configurations can be duplicated.");
            Entries[rule.Id] = new GimmickEntry { Id = rule.Id, Label = rule.Name ?? source.Label, Owner = source.Owner, Kind = source.Kind,
                Detail = source.Detail, Error = source.Error, Colour = source.Colour, Template = source.Template, Factory = source.Factory,
                ConstructionAttempted = source.ConstructionAttempted, Native = source.Native, Family = source.Family, Geometry = source.Geometry,
                Trigger = source.Trigger, Classification = source.Classification };
        }
        internal static void RefreshConfigurations()
        { foreach (var rule in Settings.Current.GimmickRules ?? new GimmickRule[0]) if (rule != null) AddConfiguration(rule); }
        internal static void DeleteConfiguration(string id)
        {
            if (!id.StartsWith("config:", StringComparison.Ordinal)) throw new InvalidOperationException("Only saved copies can be deleted.");
            var previous = Settings.Current.GimmickRules; var previousPins = Settings.Current.GimmickPins;
            var next = (previous ?? new GimmickRule[0]).Where(r => r != null && r.Id != id).ToArray();
            if (Session != null) Session.Apply(next);
            try { Settings.Current.GimmickRules = next; Settings.Current.GimmickPins = Pins.Where(p => p != id).ToArray(); Settings.Save(); }
            catch { Settings.Current.GimmickRules = previous; Settings.Current.GimmickPins = previousPins; if (Session != null) Session.Apply(previous ?? new GimmickRule[0]); throw; }
            Entries.Remove(id); Generation++;
        }
        internal static void Release()
        {
            var previous = Settings.Current.GimmickRules;
            if (Session != null) Session.Apply(new GimmickRule[0]);
            try { Settings.Edit(value => value.GimmickRules = new GimmickRule[0]); }
            catch { if (Session != null) Session.Apply(previous ?? new GimmickRule[0]); throw; }
            Generation++;
        }
        internal static void DisableOverrides()
        {
            var previous = Settings.Current.GimmickRules;
            var next = (previous ?? new GimmickRule[0]).Where(r => r != null).Select(Copy).ToArray();
            foreach (var rule in next) rule.Enabled = false;
            if (Session != null) Session.Apply(next);
            try { Settings.Current.GimmickRules = next; Settings.Save(); }
            catch { Settings.Current.GimmickRules = previous; if (Session != null) Session.Apply(previous ?? new GimmickRule[0]); throw; }
            Generation++;
        }
    }
}
