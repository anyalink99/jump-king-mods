using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Serialization;

namespace WorldsmithExtension
{
    internal static class ProjectSafety
    {
        internal static void LoadLayerAnimation(object origin_struct)
        {
            // frames can be missing in XML; native FromStruct can't take null
            // leave its change handlers alone
            if (origin_struct != null && Engine.Get(origin_struct, "frames") == null)
                Engine.Set(origin_struct, "frames", new float[0]);
        }

        static readonly HashSet<string> BlockedProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal static void AssertWritable(string path)
        {
            path = Path.GetFullPath(path);
            lock (BlockedProps)
                if (BlockedProps.Any(root => path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("Prop saving is blocked because this project's prop data failed to load. Repair the XML and reopen the project before editing props.");
        }

        internal static Exception PropLoadFinished(object __instance, Exception __exception)
        {
            if (__exception != null)
                lock (BlockedProps)
                    BlockedProps.Add(Path.GetFullPath((string)Engine.Get(__instance, "Folder")).TrimEnd('\\'));
            return __exception;
        }

        static BitmapImage missingTexture;
        internal static BitmapImage MissingTexture()
        {
            lock (typeof(ProjectSafety))
            {
                if (missingTexture != null)
                    return missingTexture;
                using (var bitmap = new System.Drawing.Bitmap(16, 16))
                {
                    using (var draw = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        draw.Clear(System.Drawing.Color.FromArgb(50, 44, 57));
                        using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(237, 160, 76), 2))
                        {
                            draw.DrawRectangle(pen, 1, 1, 13, 13);
                            draw.DrawLine(pen, 4, 4, 11, 11);
                            draw.DrawLine(pen, 11, 4, 4, 11);
                        }
                    }

                    using (var stream = new MemoryStream())
                    {
                        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        stream.Position = 0;
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                        image.Freeze();
                        missingTexture = image;
                        return image;
                    }
                }
            }
        }

        internal static bool LoadProps(object __instance, DirectoryInfo folder, object settings, ref object __result)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var byName = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (object setting in (IEnumerable)settings)
            {
                string name = (string)Engine.Get(setting, "Name");
                if (byName.ContainsKey(name))
                    throw new InvalidDataException("Duplicate prop setting: " + name);
                byName.Add(name, setting);
            }

            var dataType = Engine.Type("JKWorldsmith.Shared.Structs.PropDataObject");
            var listType = Engine.Type("JKWorldsmith.Models.PageModels.Screen.ScreenListObject`1").MakeGenericType(dataType);
            object result = Activator.CreateInstance(Engine.Type("JKWorldsmith.Models.PageModels.Screen.PropScreenListDictionary"));
            var collectionType = Engine.Type("JKWorldsmith.Shared.Structs.PropCollection");
            var serializer = new XmlSerializer(collectionType);
            int unresolved = 0;
            if (folder.Exists)
                foreach (var file in folder.GetFiles("*.xml").OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(file.Name, @"^prop([0-9]+)\.xml$", RegexOptions.IgnoreCase);
                    if (!match.Success)
                        continue;
                    int screen = Int32.Parse(match.Groups[1].Value);
                    if (screen < 1)
                        throw new InvalidDataException("Invalid prop screen: " + file.Name);
                    object collection;
                    try
                    {
                        using (var reader = XmlReader.Create(file.FullName, new XmlReaderSettings{DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null}))
                            collection = serializer.Deserialize(reader);
                        var props = collectionType.GetField("Props");
                        if (props.GetValue(collection) == null)
                            props.SetValue(collection, Array.CreateInstance(props.FieldType.GetElementType(), 0));
                    }
                    catch (Exception error) { throw new InvalidDataException("Cannot read prop placements: " + file.FullName, error); }
                    object objects = Engine.Call(collection, "GetObjectsFromList");
                    foreach (object prop in (IEnumerable)objects)
                    {
                        string name = (string)Engine.Get(prop, "Type");
                        object setting;
                        if (!byName.TryGetValue(name, out setting))
                        {
                            unresolved++;
                            setting = Activator.CreateInstance(Engine.Type("JKWorldsmith.Shared.Structs.PropSettingObject"));
                            Engine.Set(setting, "Name", name);
                            Engine.Set(setting, "Texture", MissingTexture());
                            Engine.Set(setting, "Textures", new List<ImageSource>{MissingTexture()});
                        }

                        Engine.Set(prop, "Setting", setting);
                    }

                    object entry = Activator.CreateInstance(listType, new[]{(object)screen, objects});
                    object list = Engine.Get(entry, "List");
                    list.GetType().GetEvent("ListChanged").AddEventHandler(list, new ListChangedEventHandler((sender, e) => Engine.Call(__instance, "PropFilesSave", sender, screen, folder.FullName)));
                    result.GetType().GetMethod("Add").Invoke(result, new[]{entry});
                }

            __result = result;
            Engine.Log("Loaded props " + folder.FullName + " in " + watch.ElapsedMilliseconds + " ms; unresolved=" + unresolved);
            if (unresolved > 0)
                Panel.Status("Preserved " + unresolved + " unresolved props. Repair their settings or textures; no prop files were rewritten.");
            return false;
        }

        internal static bool ReadSettings(object __instance, ref object __result)
        {
            string path = Path.Combine((string)Engine.Get(__instance, "Folder"), "textures", "prop_settings.xml");
            var type = Engine.Type("JKWorldsmith.Shared.Structs.PropSettings");
            object value;
            try
            {
                using (var reader = XmlReader.Create(path, new XmlReaderSettings{DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null}))
                    value = new XmlSerializer(type).Deserialize(reader);
            }
            catch (FileNotFoundException) { value = Activator.CreateInstance(type); }
            catch (DirectoryNotFoundException) { value = Activator.CreateInstance(type); }
            catch (Exception error) { throw new InvalidDataException("Cannot read prop settings: " + path, error); }
            // no props means there may be no settings file
            // empty XML also gives us null, which the native converter can't handle
            var field = type.GetField("settings");
            if (field.GetValue(value) == null)
                field.SetValue(value, Array.CreateInstance(field.FieldType.GetElementType(), 0));
            __result = Engine.Call(value, "GetObjectsFromList");
            lock (BlockedProps)
                BlockedProps.Remove(Path.GetFullPath((string)Engine.Get(__instance, "Folder")).TrimEnd('\\'));
            return false;
        }

        sealed class ImageEntry
        {
            internal long Time, Length;
            internal WeakReference Image;
        }

        static readonly Dictionary<string, ImageEntry> Images = new Dictionary<string, ImageEntry>(StringComparer.OrdinalIgnoreCase);
        internal static bool LocalImage(string path_and_name, ref BitmapImage __result)
        {
            if (path_and_name == null || path_and_name.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return true;
            string path = Path.GetFullPath(path_and_name);
            var file = new FileInfo(path);
            long stamp = file.LastWriteTimeUtc.Ticks, length = file.Length;
            lock (Images)
            {
                ImageEntry cached;
                if (Images.TryGetValue(path, out cached) && cached.Time == stamp && cached.Length == length)
                {
                    var image = cached.Image.Target as BitmapImage;
                    if (image != null)
                    {
                        __result = image;
                        return false;
                    }
                }
            }

            // don't hold up other image loads while decoding this one
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                __result = image;
            }

            lock (Images)
            {
                if (Images.Count > 8192)
                    foreach (string stale in Images.Where(p => !p.Value.Image.IsAlive).Select(p => p.Key).ToArray())
                        Images.Remove(stale);
                Images[path] = new ImageEntry{Time = stamp, Length = length, Image = new WeakReference(__result)};
            }

            return false;
        }
    }
}
