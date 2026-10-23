using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace MegaMappingExpansion
{
    internal sealed class TransitionLink
    {
        internal int Screen, Side, Target;
        internal string Pool;
        internal int Key { get { return Screen * 2 + Side; } }
    }
    internal sealed class TransitionScheme
    {
        internal string Id;
        internal TransitionLink[] Links;
    }
    internal sealed class TransitionPool
    {
        internal string Id, Mode, Refresh;
        internal int AvoidLast;
        internal int[] Screens, Weights;
        internal TransitionScheme[] Schemes;
        internal int Count { get { return Schemes == null ? Screens.Length : Schemes.Length; } }
    }
    internal sealed class TransitionTrigger
    {
        internal string Id, On, Event;
        internal int Screen, X, Y, Width, Height;
        internal string[] Pools;
    }
    internal sealed class TransitionPlan
    {
        internal string Identity, SaveId;
        internal ulong? FixedSeed;
        internal int Screens;
        internal readonly List<TransitionPool> Pools = new List<TransitionPool>();
        internal readonly List<TransitionLink> Exits = new List<TransitionLink>();
        internal readonly List<TransitionTrigger> Triggers = new List<TransitionTrigger>();
        internal bool Dynamic { get { return Pools.Count != 0; } }
        internal static TransitionPlan FromText(string text, int screens)
        {
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1048576 })) document.Load(reader);
            if (document.DocumentElement == null) Fail("Missing Transitions root");
            var links = new Dictionary<int, int[]>();
            foreach (var child in Children(document.DocumentElement).Where(c => c.Name == "SideLink"))
            {
                var link = Link(child, screens, false); int[] sides;
                if (!links.TryGetValue(link.Screen, out sides)) links.Add(link.Screen, sides = new[] { -1, -1 });
                if (sides[link.Side] != -1) Fail("Duplicate SideLink"); sides[link.Side] = link.Target;
            }
            return Parse(document.DocumentElement, screens, links);
        }
        internal static TransitionPlan Read(string root, int screens, IDictionary<int, int[]> staticLinks)
        {
            XmlElement entry = MappingFiles.Links(root);
            if (entry == null) return null;
            return Parse(entry, screens, staticLinks);
        }
        internal static TransitionPlan Parse(XmlElement entry, int screens, IDictionary<int, int[]> staticLinks)
        {
            var value = new TransitionPlan { Screens = screens, SaveId = "transitions" };
            Fields(entry, "version", "seed", "saveId");
            if (entry.Name != "Transitions" || entry.GetAttribute("version") != "1") Fail("Expected <Transitions version=\"1\">");
            if (entry.HasAttribute("saveId")) value.SaveId = Id(entry.GetAttribute("saveId"));
            string seed = entry.GetAttribute("seed");
            if (seed != "" && seed != "auto")
            {
                ulong parsed;
                if (!ulong.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out parsed)) Fail("seed must be auto or an unsigned 64-bit integer");
                value.FixedSeed = parsed;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (XmlElement child in Children(entry))
            {
                if (child.Name == "SideLink") { Link(child, screens, false); continue; }
                if (child.Name == "Pool" || child.Name == "SchemePool")
                {
                    bool schemes = child.Name == "SchemePool";
                    Fields(child, schemes ? new[] { "id", "mode", "avoidLast", "refresh", "weights" } : new[] { "id", "screens", "mode", "avoidLast", "refresh", "weights" });
                    var pool = new TransitionPool { Id = Id(child.GetAttribute("id")), Mode = Default(child, "mode", "random"), Refresh = Default(child, "refresh", schemes ? "event" : "crossing") };
                    if (!ids.Add(pool.Id)) Fail("Duplicate pool ID: " + pool.Id);
                    if (!new[] { "random", "weighted", "shuffle", "sequence" }.Contains(pool.Mode)) Fail(pool.Id + ": unknown mode " + pool.Mode);
                    if (!new[] { "crossing", "attempt", "event" }.Contains(pool.Refresh) || schemes && pool.Refresh == "crossing") Fail(pool.Id + ": invalid refresh mode");
                    if (schemes)
                    {
                        var choices = new List<TransitionScheme>(); var names = new HashSet<string>();
                        foreach (XmlElement scheme in Children(child))
                        {
                            if (scheme.Name != "Scheme") Fail(pool.Id + ": expected Scheme");
                            Fields(scheme, "id"); string name = Id(scheme.GetAttribute("id"));
                            if (!names.Add(name)) Fail(pool.Id + ": duplicate scheme " + name);
                            var links = Children(scheme).Select(c => Link(c, screens, false)).ToArray();
                            if (links.Length == 0 || links.Select(l => l.Key).Distinct().Count() != links.Length) Fail(pool.Id + ": empty or duplicate scheme links");
                            choices.Add(new TransitionScheme { Id = name, Links = links });
                        }
                        pool.Schemes = choices.ToArray();
                        if (pool.Schemes.Length != 0)
                        {
                            var keys = pool.Schemes[0].Links.Select(l => l.Key).OrderBy(k => k).ToArray();
                            if (pool.Schemes.Any(s => !s.Links.Select(l => l.Key).OrderBy(k => k).SequenceEqual(keys))) Fail(pool.Id + ": every scheme must own the same exits");
                        }
                    }
                    else { NoChildren(child); pool.Screens = Numbers(child.GetAttribute("screens"), 1, screens); if (pool.Screens.Distinct().Count() != pool.Screens.Length) Fail(pool.Id + ": duplicate destination screen"); }
                    if (pool.Count < 1 || pool.Count > 64) Fail(pool.Id + ": pools need 1..64 choices");
                    pool.AvoidLast = child.HasAttribute("avoidLast") ? Number(child, "avoidLast", 0, pool.Count - 1) : Math.Min(1, pool.Count - 1);
                    pool.Weights = child.HasAttribute("weights") ? Numbers(child.GetAttribute("weights"), 1, 1000000) : Enumerable.Repeat(1, pool.Count).ToArray();
                    if (pool.Weights.Length != pool.Count || child.HasAttribute("weights") && pool.Mode != "weighted") Fail(pool.Id + ": weights need weighted mode and one positive value per choice");
                    value.Pools.Add(pool);
                }
                else if (child.Name == "Exit") value.Exits.Add(Link(child, screens, true));
                else if (child.Name == "Reroll")
                {
                    Fields(child, "id", "on", "screen", "x", "y", "width", "height", "pools", "event");
                    var trigger = new TransitionTrigger { Id = Id(child.GetAttribute("id")), On = Default(child, "on", "touch"), Pools = Tokens(child.GetAttribute("pools")), Event = child.GetAttribute("event") };
                    if (value.Triggers.Any(t => t.Id == trigger.Id)) Fail("Duplicate reroll ID " + trigger.Id);
                    if (trigger.On == "event")
                    {
                        if (string.IsNullOrWhiteSpace(trigger.Event) || trigger.Event.Length > 128) Fail(trigger.Id + ": event needs 1..128 characters");
                        foreach (string field in new[] { "screen", "x", "y", "width", "height" }) if (child.HasAttribute(field)) Fail(trigger.Id + ": event rerolls have no rectangle");
                    }
                    else
                    {
                        if (trigger.Event != "" || trigger.On != "touch" && trigger.On != "screen-enter") Fail(trigger.Id + ": invalid reroll event/trigger");
                        trigger.Screen = Number(child, "screen", 1, screens) - 1;
                        if (trigger.On == "touch")
                        {
                            trigger.X = Number(child, "x", 0, 479); trigger.Y = Number(child, "y", 0, 359);
                            trigger.Width = Number(child, "width", 1, 480 - trigger.X); trigger.Height = Number(child, "height", 1, 360 - trigger.Y);
                        }
                        else foreach (string field in new[] { "x", "y", "width", "height" }) if (child.HasAttribute(field)) Fail(trigger.Id + ": screen-enter rerolls have no rectangle");
                    }
                    NoChildren(child); value.Triggers.Add(trigger);
                }
                else Fail("Unknown transition element " + child.Name);
            }
            if (value.Pools.Count > 32 || value.Pools.Sum(p => p.Count) > 256 || value.Triggers.Count > 64) Fail("Transitions allow 32 pools, 256 total choices and 64 reroll triggers");
            var owned = new HashSet<int>();
            if (staticLinks != null) foreach (var pair in staticLinks) for (int side = 0; side < 2; side++) if (pair.Value[side] > 0) owned.Add(pair.Key * 2 + side);
            foreach (var exit in value.Exits)
            {
                var pool = value.Pools.Find(p => p.Id == exit.Pool);
                if (pool == null || pool.Schemes != null) Fail("Exit references unknown destination pool " + exit.Pool);
                if (pool.Screens.Contains(exit.Screen + 1)) Fail(pool.Id + ": a destination cannot be its own exit screen " + (exit.Screen + 1));
                if (!owned.Add(exit.Key)) Fail("Two owners for screen " + (exit.Screen + 1) + ", " + Side(exit.Side));
            }
            foreach (var pool in value.Pools.Where(p => p.Schemes != null)) foreach (var link in pool.Schemes[0].Links)
                if (!owned.Add(link.Key)) Fail("Two owners for screen " + (link.Screen + 1) + ", " + Side(link.Side));
            if (owned.Count > 512) Fail("Transitions support at most 512 owned exits");
            foreach (var trigger in value.Triggers)
                if (trigger.Pools.Length == 0 || trigger.Pools.Distinct().Count() != trigger.Pools.Length || trigger.Pools.Any(id => !value.Pools.Any(p => p.Id == id && p.Refresh == "event"))) Fail(trigger.Id + ": rerolls need unique event-refresh pool IDs");
            foreach (var pool in value.Pools.Where(p => p.Schemes == null)) if (!value.Exits.Any(e => e.Pool == pool.Id)) Fail("Unused pool " + pool.Id);
            using (var hash = SHA256.Create()) value.Identity = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(screens.ToString(CultureInfo.InvariantCulture) + "/" + Canonical(entry)))).Replace("-", "");
            if (!entry.HasAttribute("saveId")) value.SaveId = value.Identity;
            return value;
        }
        private static string Canonical(XmlElement element)
        {
            var value = new StringBuilder(); value.Append(element.Name).Append('{');
            foreach (XmlAttribute attribute in element.Attributes.Cast<XmlAttribute>().OrderBy(a => a.Name, StringComparer.Ordinal)) value.Append(attribute.Name.Length).Append(':').Append(attribute.Name).Append(attribute.Value.Length).Append(':').Append(attribute.Value);
            foreach (XmlElement child in Children(element)) value.Append(Canonical(child));
            return value.Append('}').ToString();
        }
        private static TransitionLink Link(XmlElement child, int screens, bool pooled)
        {
            if (child.Name != (pooled ? "Exit" : "SideLink")) Fail("Expected " + (pooled ? "Exit" : "SideLink"));
            Fields(child, pooled ? new[] { "screen", "side", "pool" } : new[] { "screen", "side", "target" }); NoChildren(child);
            var link = new TransitionLink { Screen = Number(child, "screen", 1, screens) - 1, Side = child.GetAttribute("side") == "left" ? 0 : 1 };
            if (child.GetAttribute("side") != "left" && child.GetAttribute("side") != "right") Fail("side must be left or right");
            if (pooled) link.Pool = Id(child.GetAttribute("pool"));
            else { link.Target = Number(child, "target", 1, screens); if (link.Target == link.Screen + 1) Fail("Self-link on screen " + link.Target); }
            return link;
        }
        internal static string Side(int side) { return side == 0 ? "left" : "right"; }
        private static string Default(XmlElement e, string name, string fallback) { return e.HasAttribute(name) ? e.GetAttribute(name) : fallback; }
        private static string[] Tokens(string text) { return text.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries); }
        private static int[] Numbers(string text, int min, int max)
        {
            var result = new List<int>();
            foreach (string part in Tokens(text)) { int number; if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number < min || number > max) Fail("Expected integer " + min + ".." + max + ": " + part); result.Add(number); }
            return result.ToArray();
        }
        private static int Number(XmlElement e, string name, int min, int max) { var values = Numbers(e.GetAttribute(name), min, max); if (values.Length != 1) Fail(e.Name + " needs @" + name + " (" + min + ".." + max + ")"); return values[0]; }
        private static string Id(string id) { if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || id.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')) Fail("IDs need 1..64 letters, digits, dots, underscores or hyphens: " + id); return id; }
        private static void Fields(XmlElement e, params string[] names) { foreach (XmlAttribute a in e.Attributes) if (!names.Contains(a.Name)) Fail(e.Name + ": unknown @" + a.Name); }
        private static IEnumerable<XmlElement> Children(XmlElement e)
        {
            foreach (XmlNode node in e.ChildNodes) { var child = node as XmlElement; if (child != null) yield return child; else if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.CDATA) Fail(e.Name + ": unexpected text"); }
        }
        private static void NoChildren(XmlElement e) { if (Children(e).Any()) Fail(e.Name + " cannot contain child elements"); }
        private static void Fail(string message) { throw new InvalidDataException(MappingFiles.Transitions + ": " + message); }
    }
}
