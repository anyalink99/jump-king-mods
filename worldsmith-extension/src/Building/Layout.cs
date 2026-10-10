using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal sealed class Layout
    {
        internal int Screens, Side;
        internal string Source = "strip";
        internal readonly List<Tuple<int, string, int>> Links = new List<Tuple<int, string, int>>();
        internal static int AtlasSide(int screens)
        {
            if (screens < 1 || screens > 4096)
                throw new InvalidDataException("Screen count must be 1..4096.");
            return Math.Max(13, (int)Math.Ceiling(Math.Sqrt(screens)));
        }

        internal static string Metadata(string root)
        {
            return Path.Combine(root, "props", "mega-mapping-expansion", "map.xml");
        }

        internal static Layout Read(string root, int fallback)
        {
            var v = new Layout{Screens = fallback};
            string meta = Metadata(root);
            if (File.Exists(meta))
            {
                var e = Files.Xml(meta).Root;
                if (e.Name != "MapLayout" || (string)e.Attribute("version") != "1")
                    throw new InvalidDataException("Unsupported map.xml");
                v.Screens = (int)e.Attribute("screens");
                v.Side = (int)e.Attribute("atlasSide");
                foreach (var a in e.Attributes())
                    if (!new[]{"version", "screens", "atlasSide"}.Contains(a.Name.ToString()))
                        throw new InvalidDataException("Unknown map.xml attribute: " + a.Name);
                foreach (var l in e.Elements())
                {
                    if (l.Name != "SideLink" || l.Attributes().Any(a => !new[]{"screen", "side", "target"}.Contains(a.Name.ToString())))
                        throw new InvalidDataException("Unknown map.xml link field");
                    v.Links.Add(Tuple.Create((int)l.Attribute("screen"), (string)l.Attribute("side"), (int)l.Attribute("target")));
                }
            }

            string config = Path.Combine(root, "worldsmith-extension.xml");
            if (File.Exists(config))
                v.Source = (string)Files.Xml(config).Root.Attribute("collisionSource") ?? "strip";
            v.Validate();
            return v;
        }

        internal void Validate()
        {
            int defaultSide = AtlasSide(Screens);
            int min = (int)Math.Ceiling(Math.Sqrt(Screens));
            if (Side == 0)
                Side = defaultSide;
            if (Side < min || Side > 64)
                throw new InvalidDataException("Atlas capacity does not match authored screen count.");
            if (Source != "strip" && Source != "atlas")
                throw new InvalidDataException("Collision source must be strip or atlas.");
            var seen = new HashSet<string>();
            foreach (var l in Links)
            {
                if (l.Item1 < 1 || l.Item1 > Screens || l.Item3 < 1 || l.Item3 > Screens || l.Item1 == l.Item3 || (l.Item2 != "left" && l.Item2 != "right") || !seen.Add(l.Item1 + ":" + l.Item2))
                    throw new InvalidDataException("Invalid or duplicate side link.");
            }
        }

        internal void Save(string root)
        {
            SaveMetadata(root);
            Files.Atomic(Path.Combine(root, "worldsmith-extension.xml"), s => new XDocument(new XElement("WorldsmithExtension", new XAttribute("collisionSource", Source))).Save(s));
        }

        internal void SaveMetadata(string root)
        {
            Validate();
            var e = new XElement("MapLayout", new XAttribute("version", 1), new XAttribute("screens", Screens), new XAttribute("atlasSide", Side));
            foreach (var l in Links)
                e.Add(new XElement("SideLink", new XAttribute("screen", l.Item1), new XAttribute("side", l.Item2), new XAttribute("target", l.Item3)));
            Files.Atomic(Metadata(root), s => new XDocument(e).Save(s));
        }
    }
}
