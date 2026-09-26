using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal sealed class GimmickRegion
    {
        internal int Number, First, Last;
        internal string Name, RawName;
    }
    internal sealed class GimmickMap
    {
        internal string Id, Title, Root, Error;
        internal GimmickRegion[] Regions = new GimmickRegion[0];
        internal Dictionary<uint, HashSet<int>> Colours = new Dictionary<uint, HashSet<int>>();
        internal string[] Tags = new string[0];
    }
    internal static class GimmickMaps
    {
        private static Task<GimmickMap[]> pending;
        internal static GimmickMap[] Maps = new GimmickMap[0];
        internal static string Status = "Installed maps have not been indexed.";
        internal static bool Busy { get { return pending != null; } }
        internal static void Start()
        {
            if (pending != null) return;
            string game = Path.GetDirectoryName(typeof(JumpKing.Game1).Assembly.Location);
            Status = "Reading installed map files...";
            pending = Task.Factory.StartNew(() => Scan(game));
        }
        internal static bool Poll()
        {
            if (pending == null || !pending.IsCompleted) return false;
            try { Maps = pending.Result; Status = Maps.Length + " maps indexed; " + Maps.Count(m => m.Error != null) + " map errors."; }
            catch (Exception error) { Status = error.GetBaseException().Message; }
            pending = null; return true;
        }
        private static GimmickMap[] Scan(string game)
        {
            var roots = new List<string> { Path.Combine(game, "Content") };
            string workshop = Path.Combine(Directory.GetParent(game).Parent.FullName, "workshop", "content", "1061090");
            if (Directory.Exists(workshop)) roots.AddRange(Directory.GetFiles(workshop, "level_settings.xml", SearchOption.AllDirectories).Select(Path.GetDirectoryName));
            return roots.Select((root, i) => Read(root, i == 0 ? "native" : "workshop:" + root.Substring(workshop.Length).TrimStart('\\', '/'))).ToArray();
        }
        internal static GimmickMap Read(string root, string id)
        {
            var map = new GimmickMap { Root = root, Id = id, Title = id == "native" ? "Jump King + DLC" : id };
            try
            {
                string settings = Path.Combine(root, "level_settings.xml");
                if (File.Exists(settings))
                {
                    var xml = XDocument.Load(settings);
                    var about = xml.Root.Element("About");
                    map.Title = about == null ? id : (string)about.Element("title") ?? id;
                    var tags = xml.Root.Element("Tags"); if (tags != null) map.Tags = tags.Elements().Select(e => e.Value).ToArray();
                }
                string regions = Path.Combine(root, "gui", "location_settings.xml");
                if (File.Exists(regions)) map.Regions = ParseRegions(XDocument.Load(regions));
                string path = Path.Combine(root, "level.xnb");
                if (new FileInfo(path).Length > 64 * 1024 * 1024) throw new InvalidDataException("Atlas exceeds 64 MiB");
                using (var reader = new BinaryReader(File.OpenRead(path))) map.Colours = ReadAtlas(reader);
            }
            catch (Exception error) { map.Error = error.GetBaseException().Message; }
            return map;
        }
        internal static GimmickRegion[] ParseRegions(XDocument doc)
        {
            var locations = doc.Root.Element("locations"); if (locations == null) return new GimmickRegion[0];
            var regions = locations.Elements("Location").Select((node, i) => new GimmickRegion {
                Number = i + 1, RawName = (string)node.Element("name"), Name = RegionName((string)node.Element("name") ?? "Unnamed"),
                First = (int)node.Element("start"), Last = (int)node.Element("end") }).ToArray();
            if (regions.Any(r => r.First < 1 || r.Last < r.First)) throw new InvalidDataException("Invalid region screen range");
            return regions;
        }
        private static string RegionName(string name)
        {
            string display = LanguageJK.language.ResourceManager.GetString(name, System.Globalization.CultureInfo.GetCultureInfo("en")) ?? name;
            return System.Text.RegularExpressions.Regex.Replace(display, "\\{[^}]*\\}", "");
        }
        private static int Seven(BinaryReader reader)
        {
            int result = 0;
            for (int shift = 0; shift < 35; shift += 7)
            { byte b = reader.ReadByte(); if (shift == 28 && b > 15) throw new InvalidDataException("Invalid XNB integer"); result |= (b & 127) << shift; if (b < 128) return result; }
            throw new InvalidDataException("Invalid XNB integer");
        }
        internal static Dictionary<uint, HashSet<int>> ReadAtlas(BinaryReader reader)
        {
            if (new string(reader.ReadChars(4)) != "XNBw" || reader.ReadByte() != 5 || (reader.ReadByte() & 0xc0) != 0)
                throw new InvalidDataException("Offline indexing supports uncompressed Windows XNB5 atlases");
            if (reader.ReadInt32() != reader.BaseStream.Length) throw new InvalidDataException("XNB size mismatch");
            int count = Seven(reader); if (count < 1 || count > 64) throw new InvalidDataException("Invalid reader count");
            var readers = new List<string>();
            for (int i = 0; i < count; i++) { readers.Add(reader.ReadString()); reader.ReadInt32(); }
            if (Seven(reader) != 0) throw new InvalidDataException("Shared XNB resources are unsupported");
            int index = Seven(reader);
            if (index < 1 || index > readers.Count || !readers[index - 1].StartsWith("Microsoft.Xna.Framework.Content.Texture2DReader", StringComparison.Ordinal)
                || reader.ReadInt32() != 0) throw new InvalidDataException("Expected a Color Texture2D atlas");
            int width = reader.ReadInt32(), height = reader.ReadInt32();
            if (width <= 0 || height <= 0 || width % 60 != 0 || height % 45 != 0 || (long)width * height > 16000000)
                throw new InvalidDataException("Invalid collision atlas dimensions");
            if (reader.ReadInt32() < 1 || reader.ReadInt32() != width * height * 4) throw new InvalidDataException("Invalid mip data");
            var data = reader.ReadBytes(width * height * 4); if (data.Length != width * height * 4) throw new EndOfStreamException();
            int columns = width / 60, rows = height / 45;
            var result = new Dictionary<uint, HashSet<int>>();
            for (int screen = 0; screen < columns * rows; screen++)
            {
                int sx = screen / columns * 60, sy = screen % columns * 45;
                if (sx + 60 > width || sy + 45 > height) throw new InvalidDataException("Atlas cannot be addressed by native screen order");
                for (int y = sy; y < sy + 45; y++) for (int x = sx; x < sx + 60; x++)
                {
                    int offset = (y * width + x) * 4; if (data[offset + 3] != 255) continue;
                    uint colour = (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
                    HashSet<int> found; if (!result.TryGetValue(colour, out found)) result.Add(colour, found = new HashSet<int>());
                    found.Add(screen + 1);
                }
            }
            return result;
        }
        internal static bool Occurs(GimmickMap map, GimmickRegion region, GimmickEntry entry)
        {
            if (entry.Kind == "Wind") return map.Colours.Any(pair => GimmickWind.SourceColour(new Color { PackedValue = pair.Key })
                && (region == null || pair.Value.Any(s => s >= region.First && s <= region.Last)));
            HashSet<int> screens;
            return entry.Colour.HasValue && map.Colours.TryGetValue(entry.Colour.Value.PackedValue, out screens)
                && (region == null || screens.Any(s => s >= region.First && s <= region.Last));
        }
    }
}
