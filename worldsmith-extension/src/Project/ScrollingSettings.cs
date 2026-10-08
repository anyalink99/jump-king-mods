using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;

namespace WorldsmithExtension
{
    internal static class ScrollingSettings
    {
        sealed class Sources
        {
            internal readonly Dictionary<object, string> Paths = new Dictionary<object, string>();
        }
        static readonly ConditionalWeakTable<object, Sources> Loaded = new ConditionalWeakTable<object, Sources>();
        static readonly Regex FileName = new Regex(@"^scroll([0-9]+)\.xml$", RegexOptions.IgnoreCase);

        internal static bool Load(object __instance)
        {
            string folder = Path.GetFullPath((string)Engine.Get(__instance, "Folder"));
            var type = __instance.GetType();
            var wrappers = (IList)Activator.CreateInstance(type.GetProperty("Scrollings").PropertyType);
            var paths = new Sources();
            var textures = new Dictionary<string, ImageSource>();
            var files = new List<string>();
            var errors = new List<Exception>();
            var dataType = Engine.Type("JKWorldsmith.Shared.Structs.ScrollingBGdata");
            var serializer = new XmlSerializer(dataType);
            if (Directory.Exists(folder))
                foreach (var group in Directory.GetFiles(folder, "*.xml").Where(p => FileName.IsMatch(Path.GetFileName(p)))
                    .GroupBy(p => FileName.Match(Path.GetFileName(p)).Groups[1].Value.TrimStart('0')))
                {
                    string path = group.First();
                    try
                    {
                        int screen;
                        if (!Int32.TryParse(group.Key, out screen) || screen < 1 || group.Count() != 1)
                            throw new InvalidDataException("Invalid or duplicate scrolling screen number.");
                        object data;
                        using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                            data = serializer.Deserialize(reader);
                        var layers = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Engine.Type("JKWorldsmith.Shared.Structs.LayerObject")));
                        foreach (object layer in (IEnumerable)Engine.Get(data, "layers") ?? new object[0])
                        {
                            string texture = (string)Engine.Get(layer, "texture");
                            if (!textures.ContainsKey(texture))
                            {
                                string textureRoot = Path.Combine(folder, "textures");
                                string image = FileSafety.CheckedPath(textureRoot, Path.Combine(textureRoot, texture + ".png"));
                                textures.Add(texture, (ImageSource)Engine.Call(Engine.Type("JKWorldsmith.Extensions.DisposableImage"), "Get", image));
                            }
                            layers.Add(Engine.Call(Activator.CreateInstance(Engine.Type("JKWorldsmith.Shared.Structs.LayerObject")), "FromStruct", layer));
                        }
                        object value = Activator.CreateInstance(Engine.Type("JKWorldsmith.Shared.Structs.ScrollingObject"),
                            new[] { Engine.Get(data, "scroll_speed"), layers, Engine.Get(data, "foreground") });
                        object wrapper = Activator.CreateInstance(type.GetNestedType("ScrollingWrapper"), new[] { (object)screen, value });
                        wrappers.Add(wrapper);
                        paths.Paths.Add(wrapper, path);
                        files.Add(path);
                    }
                    catch (Exception error)
                    {
                        errors.Add(new InvalidDataException("Cannot load scrolling settings: " + path + ". The file has been left unchanged.", error));
                    }
                }

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                var handler = (ListChangedEventHandler)Delegate.CreateDelegate(typeof(ListChangedEventHandler), __instance,
                    type.GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic));
                var previous = Engine.Get(__instance, "Scrollings") as IBindingList;
                if (previous != null) previous.ListChanged -= handler;
                // keep each wrapper tied to its own file, a skipped XML mustn't shift later saves
                Loaded.Remove(__instance);
                Loaded.Add(__instance, paths);
                Engine.Set(type, "ScrollingTextures", textures);
                Engine.Set(__instance, "ScrollingFiles", files);
                Engine.Set(__instance, "Scrollings", wrappers);
                ((IBindingList)wrappers).ListChanged += handler;
                foreach (var error in errors)
                    Engine.Call(Engine.Type("JKWorldsmith.Models.DialogManager"), "ShowError", error, "Scrolling settings could not be loaded");
            }));
            return false;
        }

        internal static bool Save(object __instance, ListChangedEventArgs e)
        {
            if (e.ListChangedType != ListChangedType.ItemAdded && e.ListChangedType != ListChangedType.ItemChanged) return false;
            var wrappers = (IList)Engine.Get(__instance, "Scrollings");
            if (e.NewIndex < 0 || e.NewIndex >= wrappers.Count) return false;
            object wrapper = wrappers[e.NewIndex];
            int screen = (int)Engine.Get(wrapper, "Key");
            if (screen < 1) throw new InvalidDataException("Invalid scrolling screen number.");
            var sources = Loaded.GetValue(__instance, key => new Sources());
            string folder = (string)Engine.Get(__instance, "Folder"), path;
            bool known = sources.Paths.TryGetValue(wrapper, out path);
            if (!known)
            {
                path = Path.Combine(folder, "scroll" + screen + ".xml");
                // an unreadable file may already own this screen, don't replace it with defaults
                if (Directory.Exists(folder) && Directory.GetFiles(folder, "*.xml").Any(p =>
                    FileName.IsMatch(Path.GetFileName(p)) && FileName.Match(Path.GetFileName(p)).Groups[1].Value.TrimStart('0') == screen.ToString()))
                    throw new IOException("Scrolling settings already exist for screen " + screen + ". Repair the file and reload it before editing this screen.");
            }
            else if (FileName.Match(Path.GetFileName(path)).Groups[1].Value.TrimStart('0') != screen.ToString())
                throw new IOException("Reload scrolling settings before changing their screen number.");
            path = FileSafety.CheckedPath(folder, path);
            object data = Engine.Call(Activator.CreateInstance(Engine.Type("JKWorldsmith.Shared.Structs.ScrollingBGdata")), "SetObjectsToList", Engine.Get(wrapper, "Value"));
            Engine.Call(Engine.Type("XmlSerializerHelper"), "SafeSerialize", path, data);
            if (!known) sources.Paths.Add(wrapper, path);
            return false;
        }
    }
}
