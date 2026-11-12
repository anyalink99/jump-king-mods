using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WorldsmithExtension
{
    internal static class NativeProjects
    {
        internal static object Recent { get { return Engine.Get(Engine.Type("JKWorldsmith.Models.Projects.RecentProjects"), "Instance"); } }
        internal static object Favorites { get { return Engine.Get(Engine.Type("JKWorldsmith.Models.Projects.FavoriteProjects"), "Instance"); } }
        static IEnumerable<object> Items(object collection) { return ((IEnumerable)Engine.Get(collection, "Items")).Cast<object>(); }
        internal static object Find(string root)
        {
            return Items(Favorites).Concat(Items(Recent)).FirstOrDefault(project => WorkshopLibrary.SameFolder((string)Engine.Get(project, "Directory"), root));
        }
        internal static object Category(string root)
        {
            object[] args = { root, null };
            var type = Engine.Type("JKWorldsmith.Shared.Workshop.Workshop");
            var category = NativeMembers.Method(type, "IsValidType", typeof(string), typeof(string).MakeByRefType()).Invoke(null, args);
            if (category == null) throw new InvalidDataException((string)args[1] ?? "Choose a map, skin, set or mod folder.");
            return category;
        }
        internal static List<WorkshopFolder> FolderLinks()
        {
            return Items(Favorites).Concat(Items(Recent)).Select(project => new WorkshopFolder
            { Id = Convert.ToUInt64(Engine.Get(project, "SteamPublishedId")), Root = (string)Engine.Get(project, "Directory") }).Where(folder => folder.Id != 0).ToList();
        }
        internal static void Load(object project)
        {
            Engine.Call(Engine.Type("JKWorldsmith.Models.Projects.CurrentProjectManager"), "SetCurrentProject", project);
            Remember(project);
        }
        internal static void Remember(object project)
        {
            string root = (string)Engine.Get(project, "Directory");
            object favorite = Items(Favorites).FirstOrDefault(item => WorkshopLibrary.SameFolder((string)Engine.Get(item, "Directory"), root));
            object recent = Items(Recent).FirstOrDefault(item => WorkshopLibrary.SameFolder((string)Engine.Get(item, "Directory"), root));
            object saved = favorite ?? recent;
            Engine.Set(project, "LastOpened", DateTime.Now);
            if (saved == null)
                Engine.ExecuteCommand(Engine.Get(Recent, "AddProjectCommand"), project);
            else
            {
                if (!Object.ReferenceEquals(saved, project))
                {
                    Engine.Set(saved, "AutoCreateFolder", false);
                    Engine.Set(saved, "Name", Engine.Get(project, "Name"));
                    Engine.Set(saved, "SteamPublishedId", Engine.Get(project, "SteamPublishedId"));
                }
                Engine.Set(saved, "LastOpened", Engine.Get(project, "LastOpened"));
                Engine.Call(favorite != null ? Favorites : Recent, favorite != null ? "Save" : "OrderAndSave");
            }
        }
    }
}
