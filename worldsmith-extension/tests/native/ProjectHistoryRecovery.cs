using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;

internal static class ProjectHistoryRecovery
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    static string Entry(string name, string directory, string id, string date)
    {
        return "<Project><Name>" + SecurityElement.Escape(name) + "</Name><Directory>" + SecurityElement.Escape(directory) +
            "</Directory><Type>Level</Type><LastOpened>" + date + "</LastOpened><SteamPublishedId>" + id + "</SteamPublishedId></Project>";
    }

    internal static void Run(Assembly engine, Assembly native, string temporary)
    {
        string folder = Path.Combine(temporary, "history-recovery");
        Directory.CreateDirectory(folder);
        string projectFolder = Path.Combine(folder, "map");
        Directory.CreateDirectory(projectFolder);
        string marker = Path.Combine(projectFolder, "keep.txt");
        File.WriteAllText(marker, "project content");
        var history = engine.GetType("WorldsmithExtension.ProjectHistory");
        var load = history.GetMethod("Load", All);
        var save = engine.GetType("WorldsmithExtension.SavePatches").GetMethod("SaveXml", All);
        var projectType = native.GetType("JKWorldsmith.Models.Projects.Project");
        var reports = new List<string>();
        Action<string> report = reports.Add;
        const string title = "Map > < & \"title\" | ? *";
        const string date = "2026-09-16T17:31:44.3223861+02:00";
        foreach (bool recent in new[] { true, false })
        {
            string root = recent ? "RecentData" : "FavoriteData";
            string path = Path.Combine(folder, recent ? "recents.xml" : "favorites.xml");
            string valid = "<" + root + "><Projects>" + Entry(title, projectFolder, "0", date) +
                Entry("Other map", projectFolder + "-missing", "3268247819", "2025-08-09T15:54:47Z") + "</Projects></" + root + ">";
            File.WriteAllText(path, valid);
            var type = native.GetType("JKWorldsmith.Models.Projects." + (recent ? "RecentProjects" : "FavoriteProjects"));
            var pathField = type.GetField(recent ? "RECENT_FILE_NAMES" : "FAVES_FILE_NAMES", All);
            var instanceField = type.GetField("Instance", All);
            var events = type.GetField("OnUpdate", All);
            object oldPath = pathField.GetValue(null), oldInstance = instanceField.GetValue(null), oldEvents = events.GetValue(null);
            try
            {
                pathField.SetValue(null, path);
                events.SetValue(null, null);
                object instance = Activator.CreateInstance(type);
                instanceField.SetValue(null, instance);
                for (int i = 0; i < 2; i++) type.GetMethod("InitProjects").Invoke(instance, null);
                var items = (IList)type.GetProperty("Items").GetValue(instance, null);
                if (items.Count != 2 || File.ReadAllText(path) != valid) throw new Exception("History initialization lost entries or wrote the source");
                if (((Delegate)events.GetValue(null)).GetInvocationList().Length != 1) throw new Exception("History subscribed its save handler twice");
                object project = items[0];
                if ((string)projectType.GetProperty("Name").GetValue(project, null) != title ||
                    (string)projectType.GetProperty("Directory").GetValue(project, null) != projectFolder ||
                    (ulong)projectType.GetProperty("SteamPublishedId").GetValue(project, null) != 0 ||
                    (DateTime)projectType.GetProperty("LastOpened").GetValue(project, null) != System.Xml.XmlConvert.ToDateTime(date, System.Xml.XmlDateTimeSerializationMode.RoundtripKind))
                    throw new Exception("History changed saved project metadata");
                projectType.GetProperty("Name").SetValue(project, title + " renamed", null);
                if ((string)projectType.GetProperty("Directory").GetValue(project, null) != projectFolder) throw new Exception("Renaming a loaded project changed its directory");
                type.GetMethod(recent ? "OrderAndSave" : "Save", All).Invoke(instance, null);
                var reread = (IList)load.Invoke(null, new object[] { path, root, report });
                if (reread.Count != 2 || (string)projectType.GetProperty("Name").GetValue(reread[0], null) != title + " renamed")
                    throw new Exception("History save/reload lost the title");
            }
            finally
            {
                pathField.SetValue(null, oldPath);
                instanceField.SetValue(null, oldInstance);
                events.SetValue(null, oldEvents);
            }
            if (reports.Count != 0) throw new Exception("Valid history produced a recovery warning");

            string damaged = valid.Replace("<SteamPublishedId>0</SteamPublishedId>", "<SteamPublishedId>bad</SteamPublishedId>");
            File.WriteAllText(path, damaged);
            var partial = (IList)load.Invoke(null, new object[] { path, root, report });
            if (partial.Count != 1 || reports.Count == 0 || File.ReadAllText(path) != damaged)
                throw new Exception("Invalid history entry discarded other entries or rewrote the source");
            var dataType = native.GetType("JKWorldsmith.Models.Projects." + root);
            object data = Activator.CreateInstance(dataType);
            var array = Array.CreateInstance(projectType, partial.Count);
            partial.CopyTo(array, 0);
            dataType.GetField("Projects").SetValue(data, array);
            bool blocked = false;
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { save.Invoke(null, new object[] { path, data }); }
                catch (TargetInvocationException error) { blocked = error.InnerException is IOException; }
            }
            if (!blocked || File.ReadAllText(path) != damaged) throw new Exception("A failed recovery copy did not block the save");
            save.Invoke(null, new object[] { path, data });
            save.Invoke(null, new object[] { path, data });
            if (!Directory.GetFiles(folder, Path.GetFileName(path) + ".recovery-*").Any(file => File.ReadAllText(file) == damaged))
                throw new Exception("A later save lost the damaged history");

            // try both backup formats, the native editor leaves a .temp file
            foreach (string suffix in new[] { ".worldsmith-backup", ".temp" })
            {
                File.Delete(path + ".worldsmith-backup");
                File.Delete(path + ".temp");
                File.WriteAllText(path, "<" + root + "><Projects><Project>");
                File.WriteAllText(path + suffix, valid);
                reports.Clear();
                var restored = (IList)load.Invoke(null, new object[] { path, root, report });
                if (restored.Count != 2 || reports.Count == 0) throw new Exception("History backup was not recovered");
                save.Invoke(null, new object[] { path, data });
                if (!Directory.GetFiles(folder, Path.GetFileName(path + suffix) + ".recovery-*").Any(file => File.ReadAllText(file) == valid))
                    throw new Exception("Repairing history lost the backup used for recovery");
                File.WriteAllText(path + suffix, valid);
                File.Delete(path);
                if (((IList)load.Invoke(null, new object[] { path, root, report })).Count != 2)
                    throw new Exception("Missing primary history did not recover from its backup");
            }
            File.Delete(path + ".temp");
            File.Delete(path + ".worldsmith-backup");
            File.WriteAllText(path, "<" + root + "><Projects><Project>");
            if (((IList)load.Invoke(null, new object[] { path, root, report })).Count != 0)
                throw new Exception("Truncated history without a backup did not fall back to an empty list");
            File.WriteAllText(path, "<WrongRoot />");
            if (((IList)load.Invoke(null, new object[] { path, root, report })).Count != 0) throw new Exception("Wrong XML root accepted");
            File.Delete(path);
            reports.Clear();
            if (((IList)load.Invoke(null, new object[] { path, root, report })).Count != 0 || reports.Count != 0)
                throw new Exception("Fresh history reported corruption");
            File.WriteAllText(path, "<" + root + "><Projects /></" + root + ">");
            if (((IList)load.Invoke(null, new object[] { path, root, report })).Count != 0 || reports.Count != 0)
                throw new Exception("Empty history did not load");
            reports.Clear();
        }
        object fresh = Activator.CreateInstance(projectType);
        projectType.GetProperty("Name").SetValue(fresh, "New map", null);
        if (!(bool)projectType.GetProperty("AutoCreateFolder").GetValue(fresh, null) ||
            !((string)projectType.GetProperty("Directory").GetValue(fresh, null)).EndsWith("New map"))
            throw new Exception("Reading history changed new-project folder behavior");
        if (File.ReadAllText(marker) != "project content") throw new Exception("History recovery changed project files");
        Console.WriteLine("[OK] Recent/Favorites preserve special titles, paths, dates and IDs; invalid entries and damaged/missing history recover without losing originals.");
    }
}
