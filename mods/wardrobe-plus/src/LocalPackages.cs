using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JumpKing.Workshop;
using Steamworks;

namespace WardrobePlus
{
    // native menus consume IIUGC. local items supply an author without asking
    // Steam for user zero, their real Workshop ID remains zero
    internal sealed class LocalCollection : Collection, IIUGC
    {
        internal LocalCollection(string root) : base(root) { }
        string IIUGC.Author { get { return "Local package"; } }
    }
    internal sealed class LocalReskin : Reskin, IIUGC
    {
        internal LocalReskin(string root) : base(root) { }
        string IIUGC.Author { get { return "Local package"; } }
    }
    internal static class LocalPackages
    {
        internal static readonly string Root = Path.Combine(Path.GetDirectoryName(typeof(JumpKing.Game1).Assembly.Location), "Content", "WardrobePlus", "Skins");
        internal static List<string> Sync() { return Sync(WorkshopManager.instance, Root); }
        internal static List<string> Sync(WorkshopManager manager, string directory)
        {
            var problems = new List<string>();
            if (manager == null) return problems;
            string[] roots;
            try { roots = Directory.Exists(directory) ? Directory.GetDirectories(directory).Take(512).ToArray() : new string[0]; }
            catch (Exception error) { problems.Add(error.Message); return problems; }
            foreach (var item in manager.collections.OfType<LocalCollection>().ToArray())
                if (!roots.Contains(item.Root) || !File.Exists(Path.Combine(item.Root, Collection.FileName))) manager.collections.Remove(item);
            foreach (var item in manager.reskins.OfType<LocalReskin>().ToArray())
                if (!roots.Contains(item.Root) || !File.Exists(Path.Combine(item.Root, Reskin.FileName)) || File.Exists(Path.Combine(item.Root, Collection.FileName))) manager.reskins.Remove(item);
            foreach (string root in roots)
                try
                {
                    if (manager.collections.Any(x => string.Equals(x.Root, root, StringComparison.OrdinalIgnoreCase))
                        || manager.reskins.Any(x => string.Equals(x.Root, root, StringComparison.OrdinalIgnoreCase))) continue;
                    if (File.Exists(Path.Combine(root, Collection.FileName)))
                    {
                        var item = new LocalCollection(root);
                        if (item.Info.Reskins == null || item.Info.Reskins.Length == 0) throw new InvalidDataException("Empty collection");
                        foreach (var entry in item.Info.Reskins) ValidateAsset(root, entry.name);
                        Describe(item); manager.collections.Add(item);
                    }
                    else if (File.Exists(Path.Combine(root, Reskin.FileName)))
                    {
                        var item = new LocalReskin(root); ValidateAsset(root, item.Info.name);
                        Describe(item); manager.reskins.Add(item);
                    }
                }
                catch (Exception error) { problems.Add(root + ": " + error.GetBaseException().Message); }
            return problems;
        }
        private static void ValidateAsset(string root, string name)
        { if (!File.Exists(Catalog.AssetPath(root, name) + ".xnb")) throw new InvalidDataException("Missing native atlas: " + name); }
        private static void Describe(IUGC item)
        {
            string name = Path.GetFileName(item.Root), manifest = Path.Combine(item.Root, "wardrobe", "skin.json");
            // bad advanced data shouldn't hide a working native fallback
            if (File.Exists(manifest)) try { name = Advanced.ManifestIO.Read(manifest).name; } catch { }
            // the native conversion initializes Steamworks' fixed UTF-8 buffers;
            // setters on a default SteamUGCDetails_t don't allocate them
            item.SetDetails((SteamUGCDetails_t)new SerializedUGCDetails { m_rgchTitle = name, m_rgchDescription = "Local cosmetic package",
                m_rgchTags = "Skins", m_pchFileName = "", m_rgchURL = "",
                m_rtimeUpdated = (uint)Math.Max(0, (Directory.GetLastWriteTimeUtc(item.Root) - new DateTime(1970,1,1)).TotalSeconds) });
        }
    }
}
