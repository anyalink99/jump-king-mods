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
        internal int Version=1,Columns,Rows,CellWidth=60,CellHeight=45;
        internal readonly Dictionary<int,int> Resolutions=new Dictionary<int,int>();
        internal bool Pixel { get { return Version==2; } }
        internal MegaMappingExpansion.MapLayout PixelLayout()
        {
            var value=new MegaMappingExpansion.MapLayout{Version=2,Screens=Screens,Columns=Columns,Rows=Rows,CellWidth=CellWidth,CellHeight=CellHeight,Source=Source};
            foreach(var pair in Resolutions)value.Resolutions.Add(pair.Key,pair.Value);return value;
        }
        internal string Source = "strip";
        internal readonly List<Tuple<int, string, int>> Links = new List<Tuple<int, string, int>>();
        private XDocument settingsSource;
        private XElement transitionsSource;
        internal static int AtlasSide(int screens)
        {
            if (screens < 1 || screens > 4096)
                throw new InvalidDataException("Screen count must be 1..4096.");
            return Math.Max(13, (int)Math.Ceiling(Math.Sqrt(screens)));
        }

        internal static string Metadata(string root)
        {
            return Path.Combine(root,MegaMappingExpansion.MappingFiles.Settings);
        }

        internal static Layout Read(string root, int fallback)
        {
            var v = new Layout{Screens = fallback};
            string meta = Metadata(root);
            if(Path.GetFileName(meta)==MegaMappingExpansion.MappingFiles.Settings && File.Exists(meta))v.settingsSource=Files.Xml(meta);
            var parsed=MegaMappingExpansion.MapLayout.ReadForScreens(root, fallback);
            var external=MegaMappingExpansion.MappingFiles.Links(root);
            if(external!=null)v.transitionsSource=XElement.Parse(external.OuterXml);
            if (parsed!=null)
            {
                v.Version=parsed.Pixel?2:1;v.Screens=parsed.Screens;v.Side=parsed.AtlasSide;v.Columns=parsed.Columns;v.Rows=parsed.Rows;
                v.CellWidth=parsed.CellWidth;v.CellHeight=parsed.CellHeight;
                foreach(var pair in parsed.Resolutions)v.Resolutions.Add(pair.Key,pair.Value);
                foreach(var pair in parsed.Links)for(int side=0;side<2;side++)if(pair.Value[side]>0)v.Links.Add(Tuple.Create(pair.Key+1,side==0?"left":"right",pair.Value[side]));
                if(parsed.Pixel){v.Source=parsed.Source;v.Validate();return v;}
            }

            v.Source = CollisionSource(root);
            v.Validate();
            return v;
        }

        internal static string CollisionSource(string root)
        {
            var pixel = DeclaredPixelLayout(root);
            if (pixel != null) return pixel.Source;
            string config = Path.Combine(root, "worldsmith-extension.xml");
            if (File.Exists(config))
                return (string)Files.Xml(config).Root.Attribute("collisionSource") ?? "strip";
            string atlas = Path.Combine(root, "level.png"), strip = Path.Combine(root, "visual_level.png");
            // ordinary Worldsmith uses the newer image; explicit extension settings still win
            return File.Exists(atlas) && (!File.Exists(strip) || File.GetLastWriteTimeUtc(atlas) > File.GetLastWriteTimeUtc(strip)) ? "atlas" : "strip";
        }

        internal static MegaMappingExpansion.MapLayout DeclaredPixelLayout(string root)
        {
            var entry = MegaMappingExpansion.MappingFiles.SettingsLayout(root);
            int version;
            // infer ordinary image capacity before checking links that may point above screen 169
            return entry != null && entry.GetAttribute("source")!="native" && Int32.TryParse(entry.GetAttribute("version"), out version) && version == 2 ?
                MegaMappingExpansion.MapLayout.ReadForScreens(root, 169) : null;
        }

        internal void Validate()
        {
            int defaultSide = AtlasSide(Screens);
            if(Pixel)PixelLayout().ValidatePixel();
            int min = (int)Math.Ceiling(Math.Sqrt(Screens));
            if (Side == 0)
                Side = defaultSide;
            if (Side < min || Side > 64)
                throw new InvalidDataException("Atlas capacity does not match authored screen count.");
            if (Source != "strip" && Source != "atlas")
                throw new InvalidDataException("Collision source must be strip or atlas.");
            if(Pixel&&Links.Count>512)throw new InvalidDataException("MapLayout supports at most 512 SideLink entries.");
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
            var e = new XElement("MapLayout", new XAttribute("version", 2), new XAttribute("screens", Screens), new XAttribute("source", "native"));
            if(Pixel)e=new XElement("MapLayout",new XAttribute("version",2),new XAttribute("screens",Screens),new XAttribute("columns",Columns),new XAttribute("rows",Rows),new XAttribute("cellWidth",CellWidth),new XAttribute("cellHeight",CellHeight),new XAttribute("source",Source));
            if(Pixel)foreach(var pair in Resolutions.OrderBy(p=>p.Key))e.Add(new XElement("Resolution",new XAttribute("screen",pair.Key+1),new XAttribute("width",pair.Value),new XAttribute("height",pair.Value*3/4)));
            string metadata=settingsSource==null?Metadata(root):Path.Combine(root,MegaMappingExpansion.MappingFiles.Settings);
            var settings=File.Exists(metadata)?Files.Xml(metadata):settingsSource==null?
                new XDocument(new XElement("MappingSettings",new XAttribute("version",1))):new XDocument(settingsSource);
            var previous=settings.Root.Element("MapLayout");
            // links belong to transitions.xml now; don't duplicate them on every editor save
            if(previous==null)settings.Root.Add(e);else previous.ReplaceWith(e);
            string transitions=Path.Combine(root,MegaMappingExpansion.MappingFiles.Transitions);
            byte[] oldRoutes=File.Exists(transitions)?File.ReadAllBytes(transitions):null;
            bool routesWritten=false;
            try
            {
                if(Links.Count>0||oldRoutes!=null||transitionsSource!=null)
                {
                    var routes=oldRoutes!=null?new XElement(Files.Xml(transitions).Root):transitionsSource!=null?new XElement(transitionsSource):new XElement("Transitions",new XAttribute("version",1));
                    routes.Elements("SideLink").Remove();
                    foreach(var link in Links)routes.Add(new XElement("SideLink",new XAttribute("screen",link.Item1),new XAttribute("side",link.Item2),new XAttribute("target",link.Item3)));
                    var document=new XmlDocument();document.LoadXml(routes.ToString());
                    var staticLinks=new Dictionary<int,int[]>();
                    foreach(var link in Links){int[] sides;if(!staticLinks.TryGetValue(link.Item1-1,out sides))staticLinks.Add(link.Item1-1,sides=new[]{-1,-1});sides[link.Item2=="left"?0:1]=link.Item3;}
                    MegaMappingExpansion.TransitionPlan.Parse(document.DocumentElement,Screens,staticLinks);
                    Files.Atomic(transitions,s=>new XDocument(routes).Save(s));routesWritten=true;
                }
                Files.Atomic(metadata, s => settings.Save(s));
            }
            catch
            {
                if(routesWritten)
                {
                    if(oldRoutes==null)File.Delete(transitions);
                    else Files.Atomic(transitions,s=>s.Write(oldRoutes,0,oldRoutes.Length));
                }
                throw;
            }
        }
    }
}
