using System;
using System.Collections;
using System.IO;
using System.Reflection;

internal static class ProjectHistory
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    internal static void Run(Assembly engine, Assembly native, string temporary)
    {
        var recentType = native.GetType("JKWorldsmith.Models.Projects.RecentProjects");
        var favoriteType = native.GetType("JKWorldsmith.Models.Projects.FavoriteProjects");
        object oldRecent = recentType.GetField("Instance").GetValue(null), oldFavorite = favoriteType.GetField("Instance").GetValue(null);
        object recentHandlers = recentType.GetField("OnUpdate", All).GetValue(null), favoriteHandlers = favoriteType.GetField("OnUpdate", All).GetValue(null);
        try
        {
            // keep this test out of the user's recents.xml and favorites.xml
            recentType.GetField("OnUpdate", All).SetValue(null, null);
            favoriteType.GetField("OnUpdate", All).SetValue(null, null);
            object recent = Activator.CreateInstance(recentType), favorite = Activator.CreateInstance(favoriteType);
            recentType.GetField("Instance").SetValue(null, recent);
            favoriteType.GetField("Instance").SetValue(null, favorite);
            var recents = (IList)recentType.GetProperty("Items").GetValue(recent, null);
            var favorites = (IList)favoriteType.GetProperty("Items").GetValue(favorite, null);
            var projectType = native.GetType("JKWorldsmith.Models.Projects.Project");
            var category = Enum.Parse(projectType.GetProperty("Type").PropertyType, "Level");
            string folder = Path.Combine(temporary, "favorite-map");
            object project = Activator.CreateInstance(projectType, new object[] { "My favorite map", folder, category, DateTime.MinValue, (ulong)42 });
            favorites.Add(project);
            var adapter = engine.GetType("WorldsmithExtension.NativeProjects");
            object found = adapter.GetMethod("Find", All).Invoke(null, new object[] { folder.ToUpperInvariant() + "\\" });
            if (!Object.ReferenceEquals(project, found)) throw new Exception("Favorite project was not recognized on reopen");
            for (int i = 0; i < 2; i++) adapter.GetMethod("Remember", All).Invoke(null, new[] { found });
            if (recents.Count != 0 || favorites.Count != 1 || (string)projectType.GetProperty("Name").GetValue(found, null) != "My favorite map"
                || (ulong)projectType.GetProperty("SteamPublishedId").GetValue(found, null) != 42)
                throw new Exception("Reopening a favorite changed its name, Workshop link or list membership");
            var links = (IList)adapter.GetMethod("FolderLinks", All).Invoke(null, null);
            if (links.Count != 1 || (ulong)links[0].GetType().GetField("Id", All).GetValue(links[0]) != 42)
                throw new Exception("Favorite Workshop folder link was lost");
            object package = Activator.CreateInstance(projectType, new object[] { "Compiled map", Path.Combine(temporary, "package"), category, DateTime.MinValue, (ulong)0 });
            for (int i = 0; i < 2; i++) adapter.GetMethod("Remember", All).Invoke(null, new[] { package });
            recents = (IList)recentType.GetProperty("Items").GetValue(recent, null);
            if (recents.Count != 1 || favorites.Count != 1) throw new Exception("Reopening a package duplicated its Recent entry");
        }
        finally
        {
            recentType.GetField("Instance").SetValue(null, oldRecent);
            favoriteType.GetField("Instance").SetValue(null, oldFavorite);
            recentType.GetField("OnUpdate", All).SetValue(null, recentHandlers);
            favoriteType.GetField("OnUpdate", All).SetValue(null, favoriteHandlers);
        }
        Console.WriteLine("[OK] Reopening favorites preserves local names, Workshop links and list membership; package recents do not duplicate.");
    }
}
