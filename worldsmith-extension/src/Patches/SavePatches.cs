using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace WorldsmithExtension
{
    internal static class SavePatches
    {
        static bool SaveXml(string path, object p_object)
        {
            string projectRoot = Engine.ProjectRoot;
            if (LoadState.Failed && !String.IsNullOrEmpty(projectRoot) && Path.GetFullPath(path).StartsWith(Path.GetFullPath(projectRoot).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Saving is blocked because the project did not load successfully. Repair the reported source file and reopen it first.");
            ProjectSafety.AssertWritable(path);
            ProjectHistory.BeforeSave(path);
            Files.Atomic(path, s => new XmlSerializer(p_object.GetType(), p_object.GetType().GetNestedTypes()).Serialize(s, p_object));
            Panel.Status("Saved " + Path.GetFileName(path) + " at " + DateTime.Now.ToString("HH:mm:ss"));
            return false;
        }

        static bool SaveWardrobe(object __instance, MethodBase __originalMethod)
        {
            if (LoadState.Busy)
                return false;
            string name = __originalMethod.Name == "SaveSkinSettings" ? "skin" : __originalMethod.Name == "SaveCosmeticSettings" ? "cosmetic" : "set";
            SaveXml(Path.Combine((string)Engine.Get(__instance, "WardrobeFolder"), name + "_settings.xml"), Engine.Call(Engine.Get(__instance, name + "Settings"), "ToStruct"));
            return false;
        }

    }
}
