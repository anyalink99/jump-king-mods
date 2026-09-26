using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JKRuntime.UI;

namespace JKRuntime.Settings
{
    // One-time data conversion, not a legacy runtime/API execution path.
    public static class DataMigration
    {
        internal static string UpgradeUiXml(string xml)
        {
            var root = XElement.Parse(xml);
            if (root.Name != "UIApiSettings") throw new InvalidDataException("Unknown UI settings schema");
            foreach (var leaf in root.Descendants().Where(e => !e.HasElements))
                if (leaf.Value.StartsWith("ui-api-plus.", StringComparison.Ordinal))
                    leaf.Value = "jk.runtime." + leaf.Value.Substring("ui-api-plus.".Length);
            var legacyGrid = root.Element("UseCompactModGrid");
            if (legacyGrid != null)
            {
                if (root.Element("UseCompactWorkshopGrids") == null)
                    root.Add(new XElement("UseCompactWorkshopGrids", legacyGrid.Value));
                legacyGrid.Remove();
            }
            var buttons = root.Element("InteractBindings");
            var chords = root.Element("InteractChords");
            if (buttons != null && (chords == null || (string)chords.Attribute(XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance")) == "true"))
            {
                if (chords != null) chords.Remove();
                root.Add(new XElement("InteractChords", buttons.Elements().Select(b => new XElement("ArrayOfInt", new XElement("int", b.Value)))));
            }
            if (buttons != null) buttons.Remove();
            return root.ToString();
        }
        public static bool MigrateUi(string directory)
        {
            string current = Path.Combine(directory, "JKRuntime.Settings.xml");
            string old = Path.Combine(directory, "UIApiPlus.Settings.xml");
            if (File.Exists(current) || !File.Exists(old)) return false;
            string xml = UpgradeUiXml(File.ReadAllText(old));
            UIApiSettings settings;
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(UIApiSettings));
            using (var reader = new StringReader(xml)) settings = (UIApiSettings)serializer.Deserialize(reader);
            File.Copy(old, old + ".migration-" + Guid.NewGuid().ToString("N") + ".bak");
            AtomicXmlFile.Save(current, settings);
            return true;
        }
    }
}
