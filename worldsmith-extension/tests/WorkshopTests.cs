using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class WorkshopTests
    {
        internal static void Run(string root, Action<bool, string> assert, Action<Action, string> fails)
        {
            string project = Path.Combine(root, "workshop", "UPLOAD_TO_WORKSHOP");
            Directory.CreateDirectory(project);
            string legacy = Path.Combine(root, "legacy.set");
            Files.Text(legacy, new XElement("CachedSavedFolders", new XElement("ugc_list",
                new XElement("CachedUGC", new XElement("id", 42), new XElement("value", project)),
                new XElement("CachedUGC", new XElement("id", 77), new XElement("value", Path.Combine(root, "missing"))))).ToString());
            var folders = WorkshopLibrary.ReadLegacy(legacy);
            assert(folders.Count == 2 && folders[0].Root == project, "Legacy links imported without dropping missing folders");
            var library = new WorkshopLibrary(Path.Combine(root, "workshop-library.xml"));
            assert(WorkshopLibrary.ResolveId(project.ToUpperInvariant() + "\\", 0, library.Folders(), folders) == 42, "Legacy folder links ignore case and trailing separators");
            var ambiguous = folders.Concat(new[] { new WorkshopFolder { Id = 43, Root = project } });
            fails(() => WorkshopLibrary.ResolveId(project, 0, library.Folders(), ambiguous), "ambiguous Legacy links require an explicit item selection");
            library.Link(project, 43);
            assert(WorkshopLibrary.ResolveId(project, 42, library.Folders(), ambiguous) == 43, "explicit folder choice overrides imported and recent links");
            library.Link(project, 0);
            assert(WorkshopLibrary.ResolveId(project, 42, library.Folders(), folders) == 0, "unlink is preserved even when Legacy still remembers the old item");
            library.Link(project, 42);
            var item = new WorkshopEntry { Id = 42, Owner = 100, Title = "Map <one> & more", Description = "First line\nSecond line", Category = "Level", Tags = new[] { "Level", "Long", "Gameplay" }, Visibility = 3, PreviewUrl = "https://example.test/preview.png" };
            library.Remember(new[] { item });
            var saved = library.Items().Single();
            assert(saved.Title == item.Title && saved.Description == item.Description && saved.Visibility == 3 && saved.Tags.SequenceEqual(item.Tags) && saved.Owner == 100, "Workshop metadata round trips without losing description tags visibility or owner");
            assert(library.Folders().Single().Id == 42, "metadata caching preserves folder links");
            System.Threading.Tasks.Task.WaitAll(Enumerable.Range(0, 8).Select(index => System.Threading.Tasks.Task.Run(() =>
                new WorkshopLibrary(Path.Combine(root, "workshop-library.xml")).Remember(new[] { new WorkshopEntry
                { Id = (ulong)(1000 + index), Owner = 100, Title = "Concurrent item " + index, Category = "Mod", Tags = new[] { "Mod" } } }))).ToArray());
            assert(library.Items().Count == 9 && library.Folders().Single().Id == 42, "concurrent metadata callbacks retain every item and folder link");
            item.Title = "Renamed map";
            library.Remember(new[] { item });
            assert(library.Items().Count == 9 && library.Items().Single(entry => entry.Id == 42).Title == "Renamed map", "remote metadata updates the existing item");
            string recoveryFile = Path.Combine(root, "recover-library.xml");
            var recoverable = new WorkshopLibrary(recoveryFile);
            recoverable.Link(project, 42);
            recoverable.Remember(new[] { item });
            File.WriteAllText(recoveryFile, "<broken");
            assert(recoverable.Folders().Single().Id == 42, "damaged library reads the last valid backup");
            recoverable.Remember(new[] { item });
            assert(Directory.GetFiles(root, "recover-library.xml.damaged-*").Length == 1 && recoverable.Items().Single().Id == 42, "recovery preserves the damaged file before saving valid data");
            File.WriteAllText(Path.Combine(root, "invalid-library.xml"), "<WorkshopLibrary><Item id='wrong'/></WorkshopLibrary>");
            fails(() => new WorkshopLibrary(Path.Combine(root, "invalid-library.xml")).Items(), "invalid metadata without a backup is rejected");
            string receipt = Path.Combine(project, ".worldsmith-extension", "workshop.xml");
            Files.Text(receipt, "<Workshop id='55' />");
            fails(() => WorkshopLibrary.ResolveId(project, 42, new WorkshopFolder[0], folders), "conflicting native and receipt ids rejected");
            assert(WorkshopLibrary.ResolveId(project, 0, new WorkshopFolder[0], folders) == 55, "existing extension receipt recognized for compiled folders");
            Files.Text(Path.Combine(project, "level_settings.xml"), "<LevelSettings><About><title>The Glass Observatory</title></About></LevelSettings>");
            assert(WorkshopLibrary.LocalTitle(project, "Level") == "The Glass Observatory", "map title comes from settings instead of UPLOAD_TO_WORKSHOP");
            string template = "<LevelSettings><About><title>Sample Level</title></About></LevelSettings>";
            File.WriteAllText(Path.Combine(project, "level_settings.xml"), template);
            assert(WorkshopLibrary.ProjectTitle(project, "Level", "My renamed map", null) == "My renamed map", "reopening keeps the local name over the template title");
            assert(WorkshopLibrary.ProjectTitle(project, "Level", "Local draft", item) == "Local draft", "Workshop metadata cannot rename an existing local project");
            assert(WorkshopLibrary.ProjectTitle(project, "Level", null, item) == item.Title, "first opening uses a linked Workshop title");
            assert(WorkshopLibrary.ProjectTitle(project, "Level", " ", null) == "Sample Level", "unnamed projects still read local metadata");
            assert(File.ReadAllText(Path.Combine(project, "level_settings.xml")) == template, "resolving a project name leaves map metadata unchanged");
            string mod = Path.Combine(root, "workshop", "mod"); Directory.CreateDirectory(mod);
            Files.Text(Path.Combine(mod, "WORKSHOP.md"), "# Overlay+\n\nWorkshop description.");
            assert(WorkshopLibrary.LocalTitle(mod, "Mod") == "Overlay+", "compiled mod title comes from its Workshop document");
            string skin = Path.Combine(root, "workshop", "skin"); Directory.CreateDirectory(skin);
            Files.Text(Path.Combine(skin, "cosmetic_settings.xml"), "<ReskinSettings><name>Blue Knight</name></ReskinSettings>");
            assert(WorkshopLibrary.LocalTitle(skin, "Skin") == "Blue Knight", "skin name comes from native settings");
            assert(WorkshopLibrary.CategoryOf(new[] { "Skin", "Set" }) == "Set" && WorkshopLibrary.CategoryOf(new[] { "Skin", "Single" }) == "Skin", "set and single-skin links stay distinct");
            fails(() => WorkshopLibrary.RequireCategory(item, "Mod"), "wrong package category cannot be linked");
            fails(() => WorkshopLibrary.RequireOwner(item, 200), "another user's item cannot be updated");
            fails(() => WorkshopLibrary.RequireOwner(item, 0), "unknown account cannot be treated as the owner");
            WorkshopLibrary.RequireOwner(item, 100);
            assert(File.ReadAllText(legacy).Contains("42"), "Legacy cache retained");
        }
    }
}
