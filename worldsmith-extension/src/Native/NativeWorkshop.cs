using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Steamworks;

namespace WorldsmithExtension
{
    internal static class NativeWorkshop
    {
        static Type Manager { get { return Engine.Type("JKWorldsmith.Models.Steamworks.SteamManager"); } }
        static object Model { get { var manager = Engine.Get(Manager, "Instance"); return manager == null ? null : Engine.Get(manager, "Model"); } }
        internal static ulong Owner { get { return SteamUser.GetSteamID().m_SteamID; } }
        internal static bool Subscribe(Action refresh)
        {
            var model = Model;
            if (model == null) return false;
            model.GetType().GetEvent("Updated").AddEventHandler(model, new EventHandler((sender, args) => Engine.UI(refresh)));
            return true;
        }
        internal static WorkshopEntry FromNative(object value)
        {
            var tags = ((IEnumerable)Engine.Get(value, "Tags")).Cast<string>().ToArray();
            return new WorkshopEntry { Id = Convert.ToUInt64(Engine.Get(value, "PublishedFileId")), Owner = Convert.ToUInt64(Engine.Get(value, "SteamIDOwner")),
                Title = (string)Engine.Get(value, "Title"), Description = (string)Engine.Get(value, "Description"), Tags = tags,
                Category = WorkshopLibrary.CategoryOf(tags), Visibility = Convert.ToInt32(Engine.Get(value, "Visibility")), PreviewUrl = (string)Engine.Get(value, "PreviewURL") };
        }
        internal static List<WorkshopEntry> Items()
        {
            return ((IEnumerable)Engine.Get(Model, "Items")).Cast<object>().Select(FromNative).ToList();
        }
        internal static void Refresh()
        {
            // the host method accepts one params array whose delegate type is private
            var method = Manager.GetMethods().Single(m => m.Name == "GetAllUGCQueryItems" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsArray);
            method.Invoke(null, new object[] { Array.CreateInstance(method.GetParameters()[0].ParameterType.GetElementType(), 0) });
        }
        internal static string InstalledFolder(ulong id)
        {
            ulong size; string folder; uint timestamp;
            return SteamUGC.GetItemInstallInfo(new PublishedFileId_t(id), out size, out folder, 32768, out timestamp) && Directory.Exists(folder) ? folder : null;
        }
    }
}
