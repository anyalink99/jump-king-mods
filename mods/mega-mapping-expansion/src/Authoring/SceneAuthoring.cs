using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace MegaMappingExpansion
{
    // Authoring conveniences disappear before validation and runtime preparation.
    internal static class SceneAuthoring
    {
        internal static XmlDocument Expand(string path)
        { return Expand(path, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)); }

        private static XmlDocument Expand(string path, Dictionary<string, string> origins)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            XmlDocument document = ReadModules(path, directory, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0, origins);
            SceneObjects.Expand(document, origins);
            Dictionary<string, XmlElement> templates = Collect(document, "Templates", "Template");
            Dictionary<string, XmlElement> materials = Collect(document, "Materials", "Material");
            Dictionary<string, XmlElement> layers = Collect(document, "LayerGroups", "LayerGroup");
            foreach (XmlNode section in document.DocumentElement.ChildNodes)
            {
                // Behavior groups are effect exclusivity keys, not visual LayerGroup defaults.
                if (section.Name == "Effects" || section.Name == "Regions" || section.Name == "Rules" || section.Name == "Flags" || section.Name == "BehaviorTrees") continue;
                foreach (XmlNode node in section.ChildNodes)
                {
                    XmlElement element = node as XmlElement;
                    if (element == null) continue;
                    Apply(element, "template", templates);
                    Apply(element, "material", materials);
                    Apply(element, "group", layers);
                }
            }
            return document;
        }

        private static Dictionary<string, XmlElement> Collect(XmlDocument document, string section, string type)
        {
            Dictionary<string, XmlElement> result = new Dictionary<string, XmlElement>(StringComparer.OrdinalIgnoreCase);
            XmlElement container = document.DocumentElement[section];
            if (container == null) return result;
            foreach (XmlNode node in container.ChildNodes)
            {
                XmlElement item = node as XmlElement;
                if (item == null) continue;
                string id = item.GetAttribute("id");
                if (item.Name != type || string.IsNullOrWhiteSpace(id) || result.ContainsKey(id))
                    throw new InvalidDataException("Invalid or duplicate " + type + " id='" + id + "'");
                if (item.HasAttribute("template") || item.HasAttribute("material") || item.HasAttribute("group"))
                    throw new InvalidDataException(type + " '" + id + "': defaults cannot inherit other defaults");
                result.Add(id, item);
            }
            document.DocumentElement.RemoveChild(container);
            return result;
        }

        private static void Apply(XmlElement target, string attribute, Dictionary<string, XmlElement> definitions)
        {
            if (!target.HasAttribute(attribute)) return;
            string id = target.GetAttribute(attribute);
            XmlElement defaults;
            if (!definitions.TryGetValue(id, out defaults))
                throw new InvalidDataException(target.Name + " id='" + target.GetAttribute("id") + "' @" + attribute + ": unknown '" + id + "'");
            foreach (XmlAttribute value in defaults.Attributes)
                if (value.Name != "id" && !target.HasAttribute(value.Name)) target.SetAttribute(value.Name, value.Value);
            HashSet<string> overriddenCollections = new HashSet<string>(StringComparer.Ordinal);
            foreach (XmlNode child in target.ChildNodes) if (child is XmlElement) overriddenCollections.Add(child.Name);
            foreach (XmlNode child in defaults.ChildNodes)
                if (child is XmlElement && !overriddenCollections.Contains(child.Name)) target.AppendChild(target.OwnerDocument.ImportNode(child, true));
            target.RemoveAttribute(attribute);
        }

        private static XmlDocument ReadModules(string path, string root, HashSet<string> active, int depth, Dictionary<string, string> origins)
        {
            path = Path.GetFullPath(path);
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || depth > 16 || !active.Add(path))
                throw new InvalidDataException("Unsafe, cyclic or excessively nested scene include: " + path);
            XmlDocument document = new XmlDocument { XmlResolver = null };
            using (XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                document.Load(reader);
            if (document.DocumentElement == null || document.DocumentElement.Name != "MegaMapping")
                throw new InvalidDataException(path + ": expected MegaMapping root");
            foreach (XmlElement item in document.SelectNodes("/MegaMapping/*/*[@id]"))
                origins[item.GetAttribute("id")] = path;
            if (document.DocumentElement["Options"] != null) origins["Options"] = path;
            using (XmlReader source = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                while (source.Read())
                    if (source.NodeType == XmlNodeType.Element && (source.Depth == 2 || source.Name == "Options"))
                    {
                        string id = source.Name == "Options" ? "Options" : source.GetAttribute("id");
                        if (!string.IsNullOrEmpty(id)) origins[id] = path + "(" + ((IXmlLineInfo)source).LineNumber + ")";
                    }
            XmlNodeList includes = document.SelectNodes("/MegaMapping/Include");
            foreach (XmlElement include in includes)
            {
                if (include.Attributes.Count != 1 || !include.HasAttribute("src"))
                    throw new InvalidDataException(path + ": Include requires only @src");
                XmlDocument module = ReadModules(Path.Combine(Path.GetDirectoryName(path), include.GetAttribute("src")), root, active, depth + 1, origins);
                foreach (XmlNode child in module.DocumentElement.ChildNodes)
                {
                    XmlElement section = child as XmlElement;
                    if (section == null) continue;
                    XmlElement destination = document.DocumentElement[section.Name];
                    if (destination == null) document.DocumentElement.InsertBefore(document.ImportNode(section, true), include);
                    else
                    {
                        if (section.Name == "Options") throw new InvalidDataException(path + ": Options must be declared once, in the entry scene");
                        foreach (XmlNode entry in section.ChildNodes) destination.AppendChild(document.ImportNode(entry, true));
                    }
                }
                document.DocumentElement.RemoveChild(include);
            }
            active.Remove(path);
            return document;
        }

        internal static SceneFile Read(string path)
        {
            Dictionary<string, string> origins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            XmlDocument document = Expand(path, origins);
            XmlSerializer serializer = new XmlSerializer(typeof(SceneFile));
            serializer.UnknownNode += delegate(object sender, XmlNodeEventArgs args) {
                if (args.NodeType == XmlNodeType.Text || args.NodeType == XmlNodeType.CDATA)
                    throw new InvalidDataException(path + ": unexpected text in " + args.ObjectBeingDeserialized.GetType().Name + "; use the documented XML attributes");
            };
            serializer.UnknownAttribute += delegate(object sender, XmlAttributeEventArgs args) {
                var property = args.ObjectBeingDeserialized.GetType().GetProperty("Id");
                string id = property == null ? "Options" : (string)property.GetValue(args.ObjectBeingDeserialized, null);
                string origin;
                if (id == null || !origins.TryGetValue(id, out origin)) origin = path;
                throw new InvalidDataException(origin + ": " + args.ObjectBeingDeserialized.GetType().Name
                    + " id='" + id + "' @" + args.Attr.Name + ": unknown attribute");
            };
            serializer.UnknownElement += delegate(object sender, XmlElementEventArgs args) {
                throw new InvalidDataException(path + ": unknown element " + args.Element.Name + " (expanded line " + args.LineNumber + ")");
            };
            using (StringReader text = new StringReader(document.OuterXml))
            using (XmlReader reader = XmlReader.Create(text))
            { var result = (SceneFile)serializer.Deserialize(reader); result.SourceOrigins = origins; return result; }
        }
    }
}
