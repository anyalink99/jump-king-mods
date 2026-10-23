using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Gameplay
{
    public enum MapMechanicMode { Inherit, On, Off, Local }

    /// <summary>A validated parameter declaration, map files can't write arbitrary mod fields</summary>
    public sealed class MapMechanicParameter
    {
        public string Name { get; private set; }
        private readonly string[] choices;
        private readonly double min, max;
        private readonly bool numeric;
        public MapMechanicParameter(string name, params string[] allowed)
        { Name = ModuleDefinition.ValidId(name); choices = (string[])allowed.Clone(); if (choices.Length == 0) throw new ArgumentException("Parameter choices required"); }
        public MapMechanicParameter(string name, double minimum, double maximum)
        { Name = ModuleDefinition.ValidId(name); min = minimum; max = maximum; numeric = true; if (double.IsNaN(min) || double.IsNaN(max) || min > max) throw new ArgumentException("Invalid bounds"); }
        internal void Validate(string value)
        {
            double number;
            if (numeric ? !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || double.IsNaN(number) || double.IsInfinity(number) || number < min || number > max : !choices.Contains(value))
                throw new InvalidDataException("Invalid " + Name + " parameter: " + value);
        }
    }

    /// <summary>Permission is separate from actual effect activity and controller availability</summary>
    public struct MapMechanicDecision
    {
        public bool Enabled { get; internal set; }
        public bool Controlled { get; internal set; }
        public bool Authored { get; internal set; }
        public MapMechanicMode Mode { get; internal set; }
        public string Source { get; internal set; }
        public string Reason { get; internal set; }
        internal IDictionary<string, string> values, zoneValues;
        public string Parameter(string name, string fallback = null)
        { string value; return zoneValues != null && zoneValues.TryGetValue(name, out value) ? value : values != null && values.TryGetValue(name, out value) ? value : fallback; }
        public bool Boolean(string name, bool fallback)
        { return bool.Parse(Parameter(name, fallback ? "true" : "false")); }
        public float Number(string name, float fallback)
        { return float.Parse(Parameter(name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture); }
    }

    /// <summary>shared map authority and On/Off/Local screen permission. no physics is executed here</summary>
    public static partial class MapMechanics
    {
        public const string ControlledTagPrefix = "JKRuntime.MapControlled:";
        private sealed class Provider
        {
            internal string Owner;
            internal Action Disable;
            internal Dictionary<string, MapMechanicParameter> Parameters;
        }
        internal sealed class Rule
        {
            internal string Id, Source;
            internal int Screen, Priority;
            internal MapMechanicMode Mode;
            internal Rectangle? Bounds;
            internal bool Overlap;
            internal Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal);
        }
        private static readonly Dictionary<string, Provider> providers = new Dictionary<string, Provider>(StringComparer.Ordinal);
        private static readonly List<Rule> pixels = new List<Rule>();
        private static readonly Dictionary<string, Dictionary<int, Rule>> screens = new Dictionary<string, Dictionary<int, Rule>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Dictionary<int, List<Rule>>> zones = new Dictionary<string, Dictionary<int, List<Rule>>>(StringComparer.Ordinal);
        private static readonly HashSet<string> controlled = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> committed = new HashSet<string>(StringComparer.Ordinal);
        private static string fault;
        private static bool compiled;
        public static long Generation { get; private set; }

        /// <summary>Declare a provider before world decoding. Disable writes only its global enable preference</summary>
        public static void Register(string owner, string id, Action disableOnEntry, params MapMechanicParameter[] parameters)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(owner); ModuleDefinition.ValidId(id);
            if (disableOnEntry == null) throw new ArgumentNullException("disableOnEntry");
            Provider old;
            if (providers.TryGetValue(id, out old) && old.Owner != owner) throw new InvalidOperationException("Mechanic already owned: " + id);
            providers[id] = new Provider { Owner = owner, Disable = disableOnEntry,
                Parameters = parameters.ToDictionary(p => p.Name, StringComparer.Ordinal) };
            EnsureFactory();
        }
        public static bool AnyControlled { get { return controlled.Count != 0; } }
        public static bool IsControlled(string id) { return controlled.Contains(id); }
        public static bool CanConfigure(string id)
        { return fault == null && !IsControlled(id) && MapPolicy.AllowsMechanic(id); }
        public static void RequireUserEnable(string id, bool enabling)
        { if (enabling && !CanConfigure(id)) throw new InvalidOperationException("Controlled by map: " + id); }
        public static bool HasRules(string id) { return screens.ContainsKey(id) || zones.ContainsKey(id) || pixels.Any(p => p.Id == id); }
        public static void ClearMarkers(string id) { RuntimeApi.Kernel.CheckThread(); pixels.RemoveAll(p => p.Id == id); }
        public static bool NeedsController(string id, bool userEnabled)
        { return fault == null && MapPolicy.AllowsMechanic(id) && (userEnabled || HasRules(id)); }

        /// <summary>Import existing screen metadata without changing settings or creating collision geometry</summary>
        public static void DeclareScreen(string id, int screen, MapMechanicMode mode, string source, params string[] parameters)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(id);
            if (screen < 0 || parameters.Length % 2 != 0 || !Enum.IsDefined(typeof(MapMechanicMode), mode)) throw new ArgumentException("Invalid screen declaration");
            var rule = new Rule { Id = id, Screen = screen, Mode = mode, Source = source };
            for (int i = 0; i < parameters.Length; i += 2) rule.Values.Add(parameters[i], parameters[i + 1]);
            ValidateValues(rule); MergePixel(rule); Generation++;
        }
        private static void MergePixel(Rule rule)
        {
            var old = pixels.FirstOrDefault(p => p.Id == rule.Id && p.Screen == rule.Screen);
            if (old == null) pixels.Add(rule); else Merge(old, rule);
        }
        private static void Merge(Rule old, Rule next)
        {
            if (old.Mode != next.Mode) throw new InvalidDataException("Conflicting screen modes: " + old.Id + " screen " + (old.Screen + 1));
            foreach (var pair in next.Values)
            {
                string value;
                if (old.Values.TryGetValue(pair.Key, out value) && value != pair.Value) throw new InvalidDataException("Conflicting " + pair.Key + ": " + old.Id);
                old.Values[pair.Key] = pair.Value;
            }
        }
        private static Rule Copy(Rule rule)
        { return new Rule { Id = rule.Id, Screen = rule.Screen, Mode = rule.Mode, Source = rule.Source, Bounds = rule.Bounds, Priority = rule.Priority, Overlap = rule.Overlap, Values = new Dictionary<string, string>(rule.Values) }; }
        private static void ValidateValues(Rule rule)
        {
            Provider provider;
            if (!providers.TryGetValue(rule.Id, out provider)) throw new InvalidDataException("Mechanic provider unavailable: " + rule.Id);
            foreach (var pair in rule.Values)
            {
                MapMechanicParameter parameter;
                if (!provider.Parameters.TryGetValue(pair.Key, out parameter)) throw new InvalidDataException("Unknown parameter " + pair.Key + " for " + rule.Id);
                parameter.Validate(pair.Value);
            }
        }
        internal static void ClearWorld()
        { pixels.Clear(); screens.Clear(); zones.Clear(); decodedTexture = null; decodedRoot = null; controlled.Clear(); committed.Clear(); fault = null; compiled = false; Generation++; }
        internal static void Prepare(string root, string[] tags, int count)
        {
            string path = Path.Combine(root, "jk-runtime", "mechanics.xml");
            try
            {
                XElement xml = null;
                if (File.Exists(path)) using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 })) xml = XElement.Load(reader, LoadOptions.SetLineInfo);
                Parse(xml, tags, count);
            }
            catch (Exception error) { fault = error.Message; throw new InvalidDataException(path + ": " + error.Message, error); }
        }
        internal static void Parse(XElement xml, string[] tags, int count)
        {
            if (count < 0 || count > 100000) throw new InvalidDataException("Invalid screen count");
            var nextControlled = new HashSet<string>(StringComparer.Ordinal);
            foreach (string tag in tags ?? new string[0]) if (tag != null && tag.StartsWith(ControlledTagPrefix, StringComparison.Ordinal))
            { string id = tag.Substring(ControlledTagPrefix.Length); if (!providers.ContainsKey(id)) throw new InvalidDataException("Map requires mechanic provider: " + id); nextControlled.Add(id); }
            var rules = pixels.Select(Copy).ToList();
            if (xml != null)
            {
                Attributes(xml, "version");
                if (xml.Name != "Mechanics" || (string)xml.Attribute("version") != "1") throw new InvalidDataException("Expected Mechanics version=1");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                int budget = 0;
                foreach (var entry in xml.Elements())
                {
                    Attributes(entry, "id"); string id = (string)entry.Attribute("id");
                    if (entry.Name != "Mechanic" || id == null || !providers.ContainsKey(id) || !ids.Add(id)) throw new InvalidDataException("Unknown/duplicate mechanic: " + id);
                    var zoneIds = new HashSet<string>();
                    foreach (var node in entry.Elements())
                    {
                        if (++budget > 4096) throw new InvalidDataException("Mechanic rule budget exceeded");
                        var rule = new Rule { Id = id, Source = "mechanics.xml:" + id };
                        if (node.Name == "Screens")
                        {
                            Attributes(node, "from", "to", "mode", "equipment");
                            int first = Integer(node, "from", 1, count), last = node.Attribute("to") == null ? first : Integer(node, "to", first, count);
                            rule.Mode = Mode((string)node.Attribute("mode")); Values(node, rule);
                            if ((long)rules.Count + last - first + 1 > 65536) throw new InvalidDataException("Expanded screen rule budget exceeded");
                            for (int screen = first - 1; screen < last; screen++) { var copy = Copy(rule); copy.Screen = screen; rules.Add(copy); }
                        }
                        else if (node.Name == "Zone")
                        {
                            Attributes(node, "id", "screen", "x", "y", "width", "height", "priority", "test", "equipment");
                            string name = (string)node.Attribute("id"); if (string.IsNullOrWhiteSpace(name) || !zoneIds.Add(name)) throw new InvalidDataException("Unique zone id required");
                            rule.Source += ":" + name; rule.Screen = Integer(node, "screen", 1, count) - 1; rule.Mode = MapMechanicMode.Local;
                            int x = Integer(node, "x", 0, 479), y = Integer(node, "y", 0, 359);
                            rule.Bounds = new Rectangle(x, y, Integer(node, "width", 1, 480 - x), Integer(node, "height", 1, 360 - y));
                            rule.Priority = node.Attribute("priority") == null ? 0 : Integer(node, "priority", -100000, 100000);
                            string test = (string)node.Attribute("test") ?? "center";
                            if (test != "center" && test != "overlap") throw new InvalidDataException("Unknown zone test"); rule.Overlap = test == "overlap";
                            Values(node, rule); rules.Add(rule);
                        }
                        else throw new InvalidDataException("Unknown mechanic rule: " + node.Name);
                    }
                }
            }
            var nextScreens = new Dictionary<string, Dictionary<int, Rule>>(StringComparer.Ordinal);
            var nextZones = new Dictionary<string, Dictionary<int, List<Rule>>>(StringComparer.Ordinal);
            foreach (Rule rule in rules)
            {
                if (rule.Screen >= count) throw new InvalidDataException("Screen outside map: " + rule.Id);
                ValidateValues(rule);
                if (!rule.Bounds.HasValue)
                {
                    Dictionary<int, Rule> table; if (!nextScreens.TryGetValue(rule.Id, out table)) nextScreens.Add(rule.Id, table = new Dictionary<int, Rule>());
                    Rule old; if (table.TryGetValue(rule.Screen, out old)) Merge(old, rule); else table.Add(rule.Screen, rule);
                }
                else
                {
                    Dictionary<int, List<Rule>> table; if (!nextZones.TryGetValue(rule.Id, out table)) nextZones.Add(rule.Id, table = new Dictionary<int, List<Rule>>());
                    List<Rule> list; if (!table.TryGetValue(rule.Screen, out list)) table.Add(rule.Screen, list = new List<Rule>());
                    if (list.Any(other => other.Priority == rule.Priority && other.Bounds.Value.Intersects(rule.Bounds.Value))) throw new InvalidDataException("Overlapping zones require different priorities: " + rule.Id);
                    list.Add(rule);
                }
            }
            screens.Clear(); foreach (var pair in nextScreens) screens.Add(pair.Key, pair.Value);
            zones.Clear(); foreach (var pair in nextZones) zones.Add(pair.Key, pair.Value);
            controlled.Clear(); foreach (var id in nextControlled) controlled.Add(id);
            fault = null; compiled = true; Generation++;
        }
        private static void Attributes(XElement node, params string[] names)
        {
            if (node.Attributes().Any(a => !names.Contains(a.Name.ToString())) || node.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value))) throw new InvalidDataException("Unknown attribute/text on " + node.Name);
        }
        private static int Integer(XElement node, string name, int min, int max)
        { int value; if (!int.TryParse((string)node.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < min || value > max) throw new InvalidDataException("Invalid " + node.Name + " @" + name); return value; }
        private static MapMechanicMode Mode(string value)
        { switch (value) { case "on": return MapMechanicMode.On; case "off": return MapMechanicMode.Off; case "local": return MapMechanicMode.Local; case "inherit": return MapMechanicMode.Inherit; default: throw new InvalidDataException("Expected on/off/local/inherit mode"); } }
        private static void Values(XElement node, Rule rule)
        {
            if (node.Attribute("equipment") != null) rule.Values.Add("equipment", (string)node.Attribute("equipment"));
            foreach (var value in node.Elements())
            {
                Attributes(value, "name", "value");
                string name = (string)value.Attribute("name"), text = (string)value.Attribute("value");
                if (value.Name != "Parameter" || value.HasElements || name == null || text == null || rule.Values.ContainsKey(name)) throw new InvalidDataException("Invalid/duplicate parameter");
                rule.Values.Add(name, text);
            }
        }
        internal static void CommitEntry()
        {
            if (fault != null) throw new InvalidOperationException(fault);
            foreach (string id in controlled.OrderBy(x => x, StringComparer.Ordinal)) if (!committed.Contains(id))
            { providers[id].Disable(); committed.Add(id); }
        }
        public static MapMechanicDecision Resolve(string id, int screen, Rectangle localHitbox, bool userEnabled, bool localTrigger = false, bool legacyScreen = false)
        {
            RuntimeApi.Kernel.CheckThread();
            var result = new MapMechanicDecision { Controlled = IsControlled(id), Mode = MapMechanicMode.Inherit, Source = "player", Reason = "Disabled by player" };
            if (fault != null || !MapPolicy.AllowsMechanic(id)) { result.Mode = MapMechanicMode.Off; result.Source = "map-policy"; result.Reason = fault ?? MapPolicy.MechanicReason(id); return result; }
            Dictionary<int, Rule> table; Rule rule = null;
            if (screens.TryGetValue(id, out table)) table.TryGetValue(screen, out rule);
            // isolated native tests can use the legacy factories without the Runtime world host
            if (rule == null && !compiled) rule = pixels.FirstOrDefault(p => p.Id == id && p.Screen == screen);
            MapMechanicMode mode = rule == null ? (legacyScreen ? MapMechanicMode.On : MapMechanicMode.Inherit) : rule.Mode;
            result.Mode = mode; result.values = rule == null ? null : rule.Values;
            result.Source = rule == null ? (legacyScreen ? "screen marker" : "player") : rule.Source;
            bool permission = mode != MapMechanicMode.Off && (!result.Controlled || mode == MapMechanicMode.On || mode == MapMechanicMode.Local);
            if (!permission) { result.Reason = result.Controlled ? "Controlled by map; screen disabled" : "Disabled on this screen"; return result; }
            Rule selected = null; Dictionary<int, List<Rule>> areas; List<Rule> candidates;
            if (localHitbox.Width > 0 && localHitbox.Height > 0 && zones.TryGetValue(id, out areas) && areas.TryGetValue(screen, out candidates)) foreach (Rule zone in candidates)
                if ((zone.Overlap ? zone.Bounds.Value.Intersects(localHitbox) : zone.Bounds.Value.Contains(localHitbox.Center)) && (selected == null || zone.Priority > selected.Priority)) selected = zone;
            if (selected != null)
            {
                result.zoneValues = selected.Values; result.Source = selected.Source;
            }
            bool local = localTrigger || selected != null;
            result.Enabled = mode == MapMechanicMode.On || (mode == MapMechanicMode.Local ? local : userEnabled || local);
            result.Authored = result.Enabled && (mode == MapMechanicMode.On || local);
            result.Reason = result.Enabled ? (result.Authored ? "Enabled by map" : "Enabled by player") : mode == MapMechanicMode.Local ? "Waiting for local trigger" : "Disabled by player";
            return result;
        }
        internal static void ReleaseOwner(string owner)
        { foreach (string id in providers.Where(p => p.Value.Owner == owner).Select(p => p.Key).ToArray()) providers.Remove(id); }
        internal static object Diagnostics()
        {
            return providers.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new {
                id = p.Key, owner = p.Value.Owner, controlled = IsControlled(p.Key),
                configurable = CanConfigure(p.Key), hasRules = HasRules(p.Key),
                screen = Current(p.Key, false), error = fault
            }).ToArray();
        }
        public static MapMechanicDecision Current(string id, bool userEnabled, bool localTrigger = false, bool legacyScreen = false)
        {
            int screen = Camera.CurrentScreen;
            var player = JumpKing.GameManager.GameLoop.m_player;
            Rectangle box = player == null ? Rectangle.Empty : player.m_body.GetHitbox(); box.Y += screen * 360;
            return Resolve(id, screen, box, userEnabled, localTrigger, legacyScreen);
        }
    }
}
