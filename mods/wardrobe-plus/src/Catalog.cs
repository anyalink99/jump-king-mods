using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.Workshop;

namespace WardrobePlus
{
    internal sealed class Source
    {
        internal string Id, Name, Asset, ParentId;
        internal int Item;
        internal bool Collection;
    }
    internal sealed class SetSource { internal string Id, Name; }
    internal sealed class Catalog
    {
        internal readonly List<Source> Sources = new List<Source>();
        internal readonly List<SetSource> Sets = new List<SetSource>();
        internal readonly List<string> Problems = new List<string>();
        internal static string PackageId(IUGC item)
        {
            if (item.ID != 0) return "workshop:" + item.ID;
            // Offline Steam packages can have no cached metadata; the numeric folder is their ID.
            ulong id;
            if (ulong.TryParse(Path.GetFileName(item.Root.TrimEnd('\\', '/')), out id)) return "workshop:" + id;
            return "local:" + Path.GetFileName(item.Root.TrimEnd('\\', '/'));
        }
        internal static string SourceId(IUGC item, int target, bool collection)
        { return PackageId(item) + (collection ? ":set:" : ":skin:") + target; }
        internal static string AssetPath(string root, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name)) throw new InvalidDataException("Invalid texture name");
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string result = Path.GetFullPath(Path.Combine(prefix, name));
            if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Texture is outside its package");
            return result;
        }
        internal static Catalog Discover()
        {
            var catalog = new Catalog();
            if (WorkshopManager.instance == null) return catalog;
            foreach (var skin in WorkshopManager.instance.reskins)
                try { catalog.Add(new Source { Id = SourceId(skin, (int)skin.Info.skin, false), Name = Name(skin, skin.Info.name),
                    Item = (int)skin.Info.skin, Asset = AssetPath(skin.Root, skin.Info.name), ParentId = "" }); }
                catch (Exception error) { catalog.Problems.Add(error.Message); }
            foreach (var set in WorkshopManager.instance.collections)
            {
                string parent = PackageId(set);
                if (catalog.Sets.Any(x => x.Id == parent)) { catalog.Problems.Add("Ambiguous collection: " + parent); continue; }
                catalog.Sets.Add(new SetSource { Id = parent, Name = Name(set, Path.GetFileName(set.Root)) });
                foreach (var entry in set.Info.Reskins ?? new Collection.Reskin[0])
                    try { catalog.Add(new Source { Id = SourceId(set, (int)entry.skin, true), Name = Name(set, Path.GetFileName(set.Root)) + " / " + entry.skin,
                        Item = (int)entry.skin, Asset = AssetPath(set.Root, entry.name), Collection = true, ParentId = parent }); }
                    catch (Exception error) { catalog.Problems.Add(error.Message); }
            }
            return catalog;
        }
        private static string Name(IUGC item, string fallback) { return string.IsNullOrWhiteSpace(item.Name) ? fallback : item.Name; }
        private void Add(Source source)
        {
            if (Sources.Any(x => x.Id == source.Id)) throw new InvalidDataException("Ambiguous texture source: " + source.Id);
            Sources.Add(source);
        }
        internal List<Source> ForItem(int item) { return Sources.Where(x => x.Item == item).ToList(); }
        internal Source Find(string id, int item) { return Sources.Find(x => x.Id == id && x.Item == item); }
    }

    internal sealed class Resolution
    {
        internal string Id, Asset, Label, Warning = "";
    }
    internal static class Resolver
    {
        internal static Resolution Resolve(Outfit outfit, int item, Catalog catalog, Func<int, bool, Resolution> defaults, Func<string, bool> exists)
        {
            var choice = outfit.Choice(item);
            if (choice.Mode == ChoiceMode.OriginalGame) return defaults(item, true);
            if (choice.Mode == ChoiceMode.LevelDefault) return defaults(item, false);
            string warning = "";
            if (choice.Mode == ChoiceMode.Source)
            {
                var source = catalog.Find(choice.SourceId, item);
                if (source != null && exists(source.Asset)) return From(source);
                warning = "Missing: " + (string.IsNullOrEmpty(choice.Label) ? choice.SourceId : choice.Label);
            }
            var parent = catalog.Sources.Find(x => x.Collection && x.ParentId == outfit.ParentId && x.Item == item);
            if (parent != null && exists(parent.Asset)) { var result = From(parent); result.Warning = warning; return result; }
            if (!string.IsNullOrEmpty(outfit.ParentId) && !catalog.Sets.Any(x => x.Id == outfit.ParentId))
                warning += (warning.Length == 0 ? "" : "; ") + "Missing collection: " + outfit.ParentName;
            else if (parent != null) warning += (warning.Length == 0 ? "" : "; ") + "Missing texture: " + parent.Name;
            var fallback = defaults(item, false); fallback.Warning = warning; return fallback;
        }
        private static Resolution From(Source source) { return new Resolution { Id = source.Id, Asset = source.Asset, Label = source.Name }; }
        internal static Outfit Randomize(Outfit outfit, Catalog catalog, IEnumerable<int> items, IEnumerable<string> favorites, Random random)
        {
            var result = outfit.Copy(); var selected = new HashSet<string>(favorites);
            foreach (int item in items)
            {
                if (outfit.Choice(item).Locked) continue;
                var options = catalog.ForItem(item).Where(x => selected.Contains(x.Id)).ToList();
                if (options.Count == 0) continue;
                var source = options[random.Next(options.Count)];
                result.Set(new AppearanceChoice { Item = item, Mode = ChoiceMode.Source, SourceId = source.Id, Label = source.Name });
            }
            return result;
        }
    }
}
