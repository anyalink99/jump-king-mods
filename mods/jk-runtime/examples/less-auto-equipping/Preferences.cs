using System;
using System.IO;
using System.Xml.Linq;
using JKRuntime.Settings;

namespace LessAutoEquipping
{
    public sealed class Preferences
    {
        public bool ShouldPreventAutoEquip { get; set; }
    }

    internal sealed class PreferencesStore
    {
        internal const string FileName = "Zebra.LessAutoEquipping.Settings.xml";
        private readonly string path;
        internal Preferences Current { get; private set; }

        internal PreferencesStore(string directory)
        {
            path = Path.Combine(directory, FileName);
            var next = new Preferences(); // Preserve the upstream default: false.
            if (File.Exists(path))
            {
                var root = XDocument.Load(path).Root;
                if (root == null || root.Name != "Preferences") throw new InvalidDataException("Expected LessAutoEquipping Preferences XML");
                var value = root.Element("ShouldPreventAutoEquip");
                // Upstream writes True/False; XmlSerializer expects lowercase XML booleans.
                if (value != null) next.ShouldPreventAutoEquip = bool.Parse(value.Value);
            }
            Current = next;
        }

        internal void Set(bool value)
        {
            var next = new Preferences { ShouldPreventAutoEquip = value };
            AtomicXmlFile.Save(path, next); // Failed writes leave the in-memory setting unchanged.
            Current = next;
        }
    }
}
