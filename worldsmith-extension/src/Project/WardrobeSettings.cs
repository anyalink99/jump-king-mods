using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace WorldsmithExtension
{
    internal static class WardrobeSettings
    {
        internal static bool Load(object __instance)
        {
            string category = Engine.Get(__instance, "Type").ToString();
            string name = category == "Level" ? "skin" : category == "Skin" ? "cosmetic" : category == "Set" ? "set" : null;
            if (name == null) return false;
            string path = Path.Combine((string)Engine.Get(__instance, "WardrobeFolder"), name + "_settings.xml");
            var settingsType = __instance.GetType().GetField(name + "Settings", BindingFlags.Instance | BindingFlags.NonPublic).FieldType;
            var fromStruct = settingsType.GetMethod("FromStruct");
            object settings = Activator.CreateInstance(settingsType);
            object data = null;
            try
            {
                using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    data = new XmlSerializer(fromStruct.GetParameters()[0].ParameterType).Deserialize(reader);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (Exception error) { throw new InvalidDataException("Cannot read wardrobe settings: " + path, error); }
            if (data != null)
            {
                foreach (var field in data.GetType().GetFields())
                    if (field.FieldType.IsArray && field.GetValue(data) == null)
                        field.SetValue(data, Array.CreateInstance(field.FieldType.GetElementType(), 0));
                try { settings = Engine.Call(settings, "FromStruct", data); }
                catch (Exception error) { throw new InvalidDataException("Cannot load wardrobe settings: " + path, error); }
            }
            // use defaults in memory if the file is missing
            // hook up saving after load so opening a map doesn't write settings
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                foreach (string kind in new[] { "skin", "cosmetic", "set" })
                {
                    string field = kind + "Settings";
                    var handler = (PropertyChangedEventHandler)Delegate.CreateDelegate(typeof(PropertyChangedEventHandler), __instance,
                        __instance.GetType().GetMethod("Save" + Char.ToUpperInvariant(kind[0]) + kind.Substring(1) + "Settings", BindingFlags.Instance | BindingFlags.NonPublic));
                    var previous = Engine.Get(__instance, field) as INotifyPropertyChanged;
                    if (previous != null) previous.PropertyChanged -= handler;
                    Engine.Set(__instance, field, kind == name ? settings : null);
                    if (kind == name) ((INotifyPropertyChanged)settings).PropertyChanged += handler;
                }
            }));
            return false;
        }
    }
}
