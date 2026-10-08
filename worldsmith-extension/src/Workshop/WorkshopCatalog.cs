using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WorldsmithExtension
{
    internal static class WorkshopCatalog
    {
        static bool initialized;
        static readonly object CacheGate = new object();
        static long cacheRevision;
        static List<WorkshopEntry> items = new List<WorkshopEntry>();
        internal static event Action Changed;
        static WorkshopLibrary library;
        internal static WorkshopLibrary Library { get { return library ?? (library = new WorkshopLibrary(Path.Combine(Engine.Data, "workshop-library.xml"))); } }

        internal static void Initialize()
        {
            if (initialized) return;
            if (!NativeWorkshop.Subscribe(Refresh)) return;
            initialized = true;
            Refresh();
        }

        static void Refresh()
        {
            try
            {
                ulong owner = NativeWorkshop.Owner;
                var fresh = NativeWorkshop.Items().Where(item => item.Owner == owner && item.Category.Length != 0).ToList();
                try { items = Library.Items().Where(item => owner != 0 && item.Owner == owner).ToList(); }
                catch (Exception error) { Engine.Log("Workshop cache could not load: " + error); items = items.Where(item => item.Owner == owner).ToList(); }
                if (fresh.Count != 0)
                {
                    long revision;
                    lock (CacheGate) revision = ++cacheRevision;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try { lock (CacheGate) if (revision == cacheRevision) Library.Remember(fresh); }
                        catch (Exception error) { Engine.Log("Workshop cache could not save: " + error); }
                    });
                }
                foreach (var item in fresh)
                {
                    items.RemoveAll(previous => previous.Id == item.Id);
                    items.Add(item);
                }
                Notify();
            }
            catch (Exception error) { Engine.Log("Workshop library unavailable: " + error); }
        }

        internal static void RequestRefresh()
        {
            Initialize();
            LegacyFolderImport.Invalidate();
            NativeWorkshop.Refresh();
        }

        internal static List<WorkshopEntry> Items()
        {
            Initialize();
            return items.ToList();
        }

        internal static List<WorkshopFolder> ImportedFolders()
        {
            return LegacyFolderImport.Read().Concat(NativeProjects.FolderLinks()).ToList();
        }

        internal static ulong IdFor(string root, ulong nativeId = 0)
        {
            return WorkshopLibrary.ResolveId(root, nativeId, Library.Folders(), ImportedFolders());
        }

        internal static WorkshopEntry Find(ulong id) { return Items().FirstOrDefault(item => item.Id == id); }

        internal static string NameFor(string root, string category, ulong id, string savedName = null)
        {
            return WorkshopLibrary.ProjectTitle(root, category, savedName, String.IsNullOrWhiteSpace(savedName) ? Find(id) : null);
        }

        internal static string FolderFor(ulong id)
        {
            var overrides = Library.Folders();
            var saved = overrides.Where(folder => folder.Id == id).Select(folder => folder.Root).ToList();
            if (saved.Count != 0) return saved.FirstOrDefault(Directory.Exists) ?? saved[0];
            var imported = ImportedFolders().Where(folder => folder.Id == id && !overrides.Any(preferred => WorkshopLibrary.SameFolder(preferred.Root, folder.Root))).Select(folder => folder.Root).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return imported.FirstOrDefault(Directory.Exists) ?? imported.FirstOrDefault();
        }

        internal static string InstalledFolder(ulong id) { return NativeWorkshop.InstalledFolder(id); }
        internal static string CategoryFor(string root) { return NativeProjects.Category(root).ToString(); }

        static void Notify()
        {
            var handlers = Changed;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
                try { handler(); }
                catch (Exception error) { Engine.Log("Workshop view could not refresh: " + error); }
        }

        internal static void Remember(WorkshopEntry entry)
        {
            lock (CacheGate)
            {
                cacheRevision++;
                Library.Remember(new[] { entry });
            }
            items.RemoveAll(item => item.Id == entry.Id);
            items.Add(entry);
        }

        internal static void Link(string root, WorkshopEntry item)
        {
            if (Operations.Current.Busy || LoadState.Busy) throw new InvalidOperationException("Finish the current operation before changing the Workshop link.");
            WorkshopLibrary.RequireCategory(item, CategoryFor(root));
            WorkshopLibrary.RequireOwner(item, NativeWorkshop.Owner);
            Library.Link(root, item.Id);
            Remember(item);
            if (WorkshopLibrary.SameFolder(Engine.ProjectRoot, root)) Engine.Set(Engine.Project, "SteamPublishedId", item.Id);
            Notify();
        }
    }
}
