using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

internal static class ProjectLoading
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    internal static void Run(Assembly engine, Assembly native, string temporary)
    {
        string root = Path.Combine(temporary, "optional-settings"), props = Path.Combine(root, "props");
        Directory.CreateDirectory(props);
        var type = native.GetType("JKWorldsmith.Models.PageModels.Screen.PropsModel");
        var model = FormatterServices.GetUninitializedObject(type);
        type.GetProperty("Folder").SetValue(model, props, null);
        var read = type.GetMethod("GetPropSettings", All);
        if (((IList)read.Invoke(model, null)).Count != 0 || Directory.Exists(Path.Combine(props, "textures")))
            throw new Exception("Missing prop settings must load empty without creating source files");
        string path = Path.Combine(props, "textures", "prop_settings.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "<PropSettings/>");
        if (((IList)read.Invoke(model, null)).Count != 0 || File.ReadAllText(path) != "<PropSettings/>")
            throw new Exception("Empty optional prop settings changed on load");
        File.WriteAllText(path, "<PropSettings><broken>");
        try { read.Invoke(model, null); throw new Exception("Malformed settings loaded as defaults"); }
        catch (TargetInvocationException error)
        {
            string message = (string)engine.GetType("WorldsmithExtension.ErrorDetails").GetMethod("Message", All).Invoke(null, new object[] { error });
            if (!message.Contains(path)) throw new Exception("Prop error lost its filename");
        }
        try
        {
            engine.GetType("WorldsmithExtension.ProjectSafety").GetMethod("AssertWritable", All).Invoke(null, new object[] { path });
            throw new Exception("Malformed settings allowed a destructive save");
        }
        catch (TargetInvocationException) { }
        File.WriteAllText(path, "<PropSettings/>");
        read.Invoke(model, null);
        engine.GetType("WorldsmithExtension.ProjectSafety").GetMethod("AssertWritable", All).Invoke(null, new object[] { path });
        string placement = Path.Combine(props, "prop1.xml");
        File.WriteAllText(placement, "<PropCollection><screen>1</screen></PropCollection>");
        var settings = Activator.CreateInstance(native.GetType("JKWorldsmith.Models.PageModels.Screen.BindingSettings"));
        var loaded = (IList)type.GetMethod("GetProps", All).Invoke(model, new object[] { new DirectoryInfo(props), settings });
        if (loaded.Count != 1 || ((IList)loaded[0].GetType().GetProperty("List").GetValue(loaded[0], null)).Count != 0)
            throw new Exception("Empty prop screen did not load");
        var wardrobeType = native.GetType("JKWorldsmith.ViewModels.Level.WardrobeViewModel");
        foreach (string category in new[] { "Level", "Skin", "Set" })
        {
            object wardrobe = FormatterServices.GetUninitializedObject(wardrobeType);
            wardrobeType.GetProperty("Type").SetValue(wardrobe, Enum.Parse(wardrobeType.GetProperty("Type").PropertyType, category), null);
            string folder = Path.Combine(root, category);
            wardrobeType.GetField("WardrobeFolder").SetValue(wardrobe, folder);
            wardrobeType.GetMethod("LoadSettings", All).Invoke(wardrobe, null);
            string name = category == "Level" ? "skin" : category == "Skin" ? "cosmetic" : "set";
            if (wardrobeType.GetField(name + "Settings", All).GetValue(wardrobe) == null || Directory.Exists(folder))
                throw new Exception("Missing wardrobe defaults wrote project files");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, name + "_settings.xml");
            object previous = wardrobeType.GetField(name + "Settings", All).GetValue(wardrobe);
            object data = previous.GetType().GetMethod("ToStruct").Invoke(previous, null);
            using (var writer = new StreamWriter(file)) new System.Xml.Serialization.XmlSerializer(data.GetType()).Serialize(writer, data);
            string original = File.ReadAllText(file);
            wardrobeType.GetMethod("LoadSettings", All).Invoke(wardrobe, null);
            if (File.ReadAllText(file) != original || File.Exists(file + ".worldsmith-backup"))
                throw new Exception("Reading valid wardrobe settings triggered a save");
            var notify = previous.GetType().GetMethod("OnPropertyChanged", All, null, new[] { typeof(string) }, null);
            notify.Invoke(previous, new object[] { "Settings" });
            if (File.Exists(file + ".worldsmith-backup")) throw new Exception("Previous wardrobe still owns the save handler");
            object current = wardrobeType.GetField(name + "Settings", All).GetValue(wardrobe);
            notify.Invoke(current, new object[] { "Settings" });
            if (!File.Exists(file + ".worldsmith-backup")) throw new Exception("Loaded wardrobe lost its save handler");
            File.WriteAllText(file, "<Broken>");
            try { wardrobeType.GetMethod("LoadSettings", All).Invoke(wardrobe, null); throw new Exception("Malformed wardrobe settings were ignored"); }
            catch (TargetInvocationException) { }
            if (File.ReadAllText(file) != "<Broken>") throw new Exception("Malformed wardrobe settings overwritten");
        }
        var managerType = native.GetType("JKWorldsmith.Models.Projects.CurrentProjectManager");
        var state = engine.GetType("WorldsmithExtension.LoadState");
        var instance = managerType.GetField("Instance");
        object previousProject = instance.GetValue(null);
        try
        {
            instance.SetValue(null, Activator.CreateInstance(managerType));
            var projectType = native.GetType("JKWorldsmith.Models.Projects.Project");
            object missing = FormatterServices.GetUninitializedObject(projectType);
            projectType.GetField("name", All).SetValue(missing, "Missing project");
            projectType.GetField("directory", All).SetValue(missing, Path.Combine(root, "does-not-exist"));
            try { managerType.GetMethod("SetCurrentProject").Invoke(null, new[] { missing }); throw new Exception("Missing project unexpectedly loaded"); }
            catch (TargetInvocationException) { }
            if ((bool)state.GetField("Busy", All).GetValue(null) || !(bool)state.GetField("Failed", All).GetValue(null))
                throw new Exception("Synchronous native load failure left the editor permanently busy");
        }
        finally
        {
            instance.SetValue(null, previousProject);
            state.GetField("Busy", All).SetValue(null, false);
            state.GetField("Failed", All).SetValue(null, false);
        }
        Console.WriteLine("[OK] Missing/empty optional props and wardrobes load without writes; malformed XML keeps its filename and blocks prop saves.");
    }
}
