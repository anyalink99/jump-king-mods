using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace WardrobePlus
{
    internal sealed class Store
    {
        internal readonly string DirectoryPath;
        internal string Warning = "";
        internal bool ReadOnly;
        internal Store(string directory) { DirectoryPath = directory; }
        internal string FilePath { get { return Path.Combine(DirectoryPath, "wardrobe.xml"); } }
        internal WardrobeData Load()
        {
            if (!File.Exists(FilePath)) return new WardrobeData();
            try { var data = Read<WardrobeData>(FilePath); Validate(data); return data; }
            catch (Exception error)
            {
                Warning = "Settings could not be read: " + error.GetBaseException().Message;
                // Do not replace an unknown/newer schema or a corrupt primary automatically.
                ReadOnly = true;
                if (File.Exists(FilePath + ".bak"))
                    try { var backup = Read<WardrobeData>(FilePath + ".bak"); Validate(backup); Warning += "; using backup (read-only)"; return backup; }
                    catch { }
                return new WardrobeData { Enabled = false };
            }
        }
        internal void Save(WardrobeData value)
        {
            if (ReadOnly) throw new InvalidOperationException(Warning + ". Restore or move the settings file before saving.");
            Validate(value);
            value.Version = 3;
            Write(FilePath, value);
        }
        internal static void Validate(WardrobeData data)
        {
            if (data == null || data.Version < 1 || data.Version > 3) throw new InvalidDataException("Unsupported Wardrobe settings version");
            if (data.Current == null || data.Presets == null || data.Favorites == null || data.Maps == null || data.Binding == null) throw new InvalidDataException("Incomplete Wardrobe settings");
            if (data.Presets.Count > 1000) throw new InvalidDataException("Too many presets");
            ValidateOutfit(data.Current);
            if (data.Undo != null) ValidateOutfit(data.Undo);
            foreach (var preset in data.Presets) ValidateOutfit(preset);
            if (data.Presets.GroupBy(x => x.Id).Any(x => x.Count() > 1)) throw new InvalidDataException("Duplicate preset ID");
            if (data.Favorites.Count > 10000 || data.Favorites.Any(x => x == null || x.Length > 512)
                || data.Maps.Count > 10000 || data.Maps.Any(x => x == null || string.IsNullOrEmpty(x.MapId) || x.MapId.Length > 512 || string.IsNullOrEmpty(x.OutfitId))
                || data.Maps.GroupBy(x => x.MapId).Any(x => x.Count() > 1)) throw new InvalidDataException("Invalid favorites or map assignments");
            foreach (var chord in data.Binding)
                if (chord == null || chord.Buttons == null || chord.Buttons.Length > 2) throw new InvalidDataException("Invalid binding");
        }
        internal static void ValidateOutfit(Outfit outfit)
        {
            if (outfit == null || string.IsNullOrWhiteSpace(outfit.Id) || !System.Text.RegularExpressions.Regex.IsMatch(outfit.Id, "^[a-zA-Z0-9_-]{1,80}$")
                || outfit.Name == null || outfit.Name.Length > 64 || outfit.ParentId == null || outfit.ParentId.Length > 512 || outfit.ParentName == null || outfit.ParentName.Length > 1024 || outfit.Choices == null || outfit.Fits == null)
                throw new InvalidDataException("Invalid outfit");
            if (outfit.Choices.Count > 512 || outfit.Fits.Count > 8192) throw new InvalidDataException("Outfit exceeds limits");
            if (outfit.Equipment != null && (outfit.Equipment.Items == null || outfit.Equipment.Items.Count > 512
                || outfit.Equipment.Items.Any(x => x < 0) || outfit.Equipment.Items.Distinct().Count() != outfit.Equipment.Items.Count))
                throw new InvalidDataException("Invalid equipment selection");
            if (!Enum.IsDefined(typeof(MaterialKind), outfit.Material) || outfit.Materials == null || outfit.Materials.Count > 512
                || outfit.Materials.Any(x => x == null || !Enum.IsDefined(typeof(MaterialKind), x.Kind))
                || outfit.Materials.GroupBy(x => x.Item).Any(x => x.Count() > 1)) throw new InvalidDataException("Invalid materials");
            if (outfit.Choices.Any(x => x == null || !Enum.IsDefined(typeof(ChoiceMode), x.Mode) || x.SourceId == null || x.SourceId.Length > 512 || x.Label == null || x.Label.Length > 1024)
                || outfit.Choices.GroupBy(x => x.Item).Any(x => x.Count() > 1)) throw new InvalidDataException("Invalid item choices");
            foreach (var fit in outfit.Fits)
                if (fit == null || fit.BaseId == null || fit.BaseId.Length > 512 || fit.SourceId == null || fit.SourceId.Length > 512 || fit.X < -32 || fit.X > 32 || fit.Y < -32 || fit.Y > 32
                    || fit.Group < -1 || fit.Group > 64 || fit.Frame < -1 || fit.Frame > 1024 || (fit.Group == -1) != (fit.Frame == -1))
                    throw new InvalidDataException("Invalid fit adjustment");
            if (outfit.Fits.GroupBy(x => new { x.BaseId, x.SourceId, x.Item, x.Group, x.Frame }).Any(x => x.Count() > 1))
                throw new InvalidDataException("Duplicate fit adjustment");
        }
        internal static T Read<T>(string path)
        {
            if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("XML file exceeds 8 MB");
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
        }
        internal static void Write<T>(string path, T value)
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { new XmlSerializer(typeof(T)).Serialize(stream, value); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        internal static T Clone<T>(T value)
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var stream = new MemoryStream())
            { serializer.Serialize(stream, value); stream.Position = 0; return (T)serializer.Deserialize(stream); }
        }
        internal string Export(Outfit outfit)
        {
            ValidateOutfit(outfit);
            var recipe = new Recipe { Outfit = outfit.Copy() };
            var ids = outfit.Choices.Where(x => x.Mode == ChoiceMode.Source).Select(x => x.SourceId).Concat(new[] { outfit.ParentId });
            foreach (var id in ids.Distinct())
            {
                ulong workshopId;
                if (id != null && id.StartsWith("workshop:") && ulong.TryParse(id.Substring(9).Split(':')[0], out workshopId))
                    recipe.WorkshopLinks.Add("https://steamcommunity.com/sharedfiles/filedetails/?id=" + workshopId);
            }
            string path = Path.Combine(DirectoryPath, "Recipes", outfit.Id + ".xml");
            Write(path, recipe);
            return path;
        }
        internal Outfit Import(string path)
        {
            var recipe = Read<Recipe>(path);
            if (recipe.Version < 1 || recipe.Version > 3) throw new InvalidDataException("Unsupported recipe version");
            ValidateOutfit(recipe.Outfit);
            var outfit = recipe.Outfit.Copy(); outfit.Id = Guid.NewGuid().ToString("N");
            return outfit;
        }
    }
}
