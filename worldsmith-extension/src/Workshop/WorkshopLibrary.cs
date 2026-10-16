using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal sealed class WorkshopEntry
    {
        internal ulong Id, Owner;
        internal string Title, Description, Category, PreviewUrl;
        internal string[] Tags = new string[0];
        internal int Visibility = 2;
        public override string ToString() { return Title + "  (" + Category + ", " + Id + ")"; }
    }

    internal sealed class WorkshopFolder
    {
        internal ulong Id;
        internal string Root;
    }

    internal sealed class WorkshopLibrary
    {
        static readonly object Gate = new object();
        static readonly Dictionary<string, long> Revisions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        long revision = -1;
        readonly string file;
        internal WorkshopLibrary(string path) { file = Path.GetFullPath(path); }
        XDocument cached;
        long cachedTicks = -1, cachedLength = -1;
        bool recovered;
        static XDocument Validated(string path)
        {
            var document = Files.Xml(path);
            if (document.Root == null || document.Root.Name != "WorkshopLibrary") throw new InvalidDataException("Invalid Workshop library root.");
            foreach (var folder in document.Root.Elements("Folder"))
            {
                ulong id;
                string root = (string)folder.Attribute("path");
                if (!UInt64.TryParse((string)folder.Attribute("id"), out id) || String.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
                    throw new InvalidDataException("Invalid Workshop folder link.");
                Path.GetFullPath(root);
            }
            foreach (var item in document.Root.Elements("Item"))
            {
                ulong id, owner; int visibility;
                if (!UInt64.TryParse((string)item.Attribute("id"), out id) || id == 0 ||
                    !UInt64.TryParse((string)item.Attribute("owner"), out owner) ||
                    !Int32.TryParse((string)item.Attribute("visibility"), out visibility) || visibility < 0 || visibility > 3 ||
                    item.Element("Tags") == null || item.Element("Title") == null)
                    throw new InvalidDataException("Invalid Workshop item metadata.");
            }
            return document;
        }
        XDocument Read()
        {
            lock (Gate)
            {
                if (!File.Exists(file)) return new XDocument(new XElement("WorkshopLibrary"));
                long latest; Revisions.TryGetValue(file, out latest);
                var info = new FileInfo(file);
                if (cached != null && revision == latest && info.LastWriteTimeUtc.Ticks == cachedTicks && info.Length == cachedLength)
                    return new XDocument(cached);
                recovered = false;
                try { cached = Validated(file); }
                catch (Exception error)
                {
                    if (!(error is System.Xml.XmlException || error is InvalidDataException || error is ArgumentException)) throw;
                    string backup = file + ".worldsmith-backup";
                    if (!File.Exists(backup)) throw new InvalidDataException("Workshop library is damaged. Restore " + file + " from a backup.", error);
                    cached = Validated(backup);
                    recovered = true;
                }
                revision = latest;
                cachedTicks = info.LastWriteTimeUtc.Ticks; cachedLength = info.Length;
                return new XDocument(cached);
            }
        }
        void Save(XDocument document)
        {
            if (recovered && File.Exists(file)) File.Copy(file, file + ".damaged-" + Guid.NewGuid().ToString("N"), false);
            Files.Text(file, document.ToString());
            long latest; Revisions.TryGetValue(file, out latest); Revisions[file] = latest + 1;
            cached = null; recovered = false;
        }

        internal static bool SameFolder(string left, string right)
        {
            return !String.IsNullOrWhiteSpace(left) && !String.IsNullOrWhiteSpace(right) &&
                String.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'), Path.GetFullPath(right).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        internal static List<WorkshopFolder> ReadLegacy(string path)
        {
            if (!File.Exists(path)) return new List<WorkshopFolder>();
            return Files.Xml(path).Descendants("CachedUGC").Select(element => new WorkshopFolder
            { Id = (ulong?)element.Element("id") ?? 0, Root = (string)element.Element("value") })
                .Where(folder => folder.Id != 0 && !String.IsNullOrWhiteSpace(folder.Root) && Path.IsPathRooted(folder.Root)).ToList();
        }

        internal List<WorkshopFolder> Folders()
        {
            return Read().Root.Elements("Folder").Select(element => new WorkshopFolder
            { Id = (ulong)element.Attribute("id"), Root = (string)element.Attribute("path") }).ToList();
        }

        internal void Link(string root, ulong id)
        {
            lock (Gate)
            {
                if (!Directory.Exists(root)) throw new InvalidDataException("Choose an existing project folder and Workshop item.");
                root = Path.GetFullPath(root).TrimEnd('\\', '/');
                var document = Read();
                foreach (var element in document.Root.Elements("Folder").Where(element => SameFolder((string)element.Attribute("path"), root)).ToList()) element.Remove();
                document.Root.AddFirst(new XElement("Folder", new XAttribute("id", id), new XAttribute("path", root)));
                Save(document);
            }
        }

        internal void Remember(IEnumerable<WorkshopEntry> entries)
        {
            lock (Gate)
            {
                var document = Read();
                foreach (var item in entries)
                {
                    foreach (var previous in document.Root.Elements("Item").Where(element => (ulong)element.Attribute("id") == item.Id).ToList()) previous.Remove();
                    document.Root.Add(new XElement("Item", new XAttribute("id", item.Id), new XAttribute("owner", item.Owner),
                        new XAttribute("visibility", item.Visibility), new XAttribute("category", item.Category ?? ""),
                        new XElement("Title", item.Title), new XElement("Description", item.Description), new XElement("Preview", item.PreviewUrl),
                        new XElement("Tags", item.Tags.Select(tag => new XElement("Tag", tag)))));
                }
                Save(document);
            }
        }

        internal List<WorkshopEntry> Items()
        {
            return Read().Root.Elements("Item").Select(element => new WorkshopEntry
            {
                Id = (ulong)element.Attribute("id"), Owner = (ulong)element.Attribute("owner"), Visibility = (int)element.Attribute("visibility"),
                Category = (string)element.Attribute("category"), Title = (string)element.Element("Title"), Description = (string)element.Element("Description"),
                PreviewUrl = (string)element.Element("Preview"), Tags = element.Element("Tags").Elements("Tag").Select(tag => tag.Value).ToArray()
            }).ToList();
        }

        internal static ulong ResolveId(string root, ulong nativeId, IEnumerable<WorkshopFolder> preferred, IEnumerable<WorkshopFolder> imported)
        {
            string receipt = Path.Combine(root, ".worldsmith-extension", "workshop.xml");
            ulong receiptId = File.Exists(receipt) ? (ulong?)Files.Xml(receipt).Root.Attribute("id") ?? 0 : 0;
            var saved = preferred.Where(folder => SameFolder(folder.Root, root)).Select(folder => folder.Id).Distinct().ToArray();
            if (saved.Length == 1) return saved[0];
            var explicitIds = new[] { nativeId, receiptId }.Where(id => id != 0).Distinct().ToArray();
            if (explicitIds.Length > 1) throw new InvalidDataException("This project contains conflicting Workshop links. Choose its item on the Workshop page and select the project folder again.");
            if (explicitIds.Length == 1) return explicitIds[0];
            var legacy = imported.Where(folder => SameFolder(folder.Root, root)).Select(folder => folder.Id).Distinct().ToArray();
            if (legacy.Length > 1) throw new InvalidDataException("Legacy linked this folder to several Workshop items. Select the intended item on the Workshop page.");
            return legacy.Length == 1 ? legacy[0] : 0;
        }

        internal static string ProjectTitle(string root, string category, string savedName, WorkshopEntry item)
        {
            // keep the name the author picked, even if level_settings.xml
            // still says Sample Level
            if (!String.IsNullOrWhiteSpace(savedName)) return savedName;
            if (item != null && !String.IsNullOrWhiteSpace(item.Title)) return item.Title;
            return LocalTitle(root, category);
        }

        internal static string LocalTitle(string root, string category)
        {
            string file = Path.Combine(root, category == "Level" ? "level_settings.xml" : "cosmetic_settings.xml");
            if (File.Exists(file))
            {
                var xml = Files.Xml(file);
                string title = category == "Level" ? (string)(xml.Root.Element("About") ?? new XElement("About")).Element("title") : (string)xml.Root.Element("name");
                if (!String.IsNullOrWhiteSpace(title)) return title.Trim();
            }
            foreach (string name in new[] { "WORKSHOP.md", "README.md" })
            {
                string path = Path.Combine(root, name);
                if (!File.Exists(path)) continue;
                string heading = File.ReadLines(path).Take(20).FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));
                if (heading != null && heading.Length > 2) return heading.Substring(2).Trim();
            }
            return Path.GetFileName(Path.GetFullPath(root).TrimEnd('\\', '/'));
        }

        internal static string LocalPreview(string root)
        {
            return new[] { "workshop-preview.png", "preview.png", "workshop.png" }.Select(name => Path.Combine(root, name)).FirstOrDefault(File.Exists) ?? "";
        }

        internal static string CategoryOf(IEnumerable<string> tags)
        {
            return tags.Contains("Mod") ? "Mod" : tags.Contains("Level") ? "Level" : tags.Contains("Set") ? "Set" : tags.Contains("Skin") || tags.Contains("Single") ? "Skin" : "";
        }

        internal static void RequireOwner(WorkshopEntry entry, ulong owner)
        {
            if (entry == null || owner == 0 || entry.Owner != owner) throw new InvalidOperationException("Only your own Workshop items can be updated.");
        }

        internal static void RequireCategory(WorkshopEntry entry, string category)
        {
            if (entry != null && entry.Category != category) throw new InvalidDataException("This Workshop item is a " + entry.Category + ", but the selected folder is a " + category + ".");
        }
    }
}
