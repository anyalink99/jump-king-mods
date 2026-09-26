using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace MegaMappingExpansion
{
    // Objects are source-level assemblies. Only ordinary validated components reach runtime.
    internal static class SceneObjects
    {
        private static readonly Regex Token = new Regex(@"\$\{([a-zA-Z0-9_.-]+)\}");
        private static readonly HashSet<string> Sections = new HashSet<string>(new[] {
            "Props", "Nodes", "Lights", "Fogs", "Rains", "Emitters", "Texts", "Anchors", "Regions", "Effects", "Rules", "Flags", "BehaviorTrees", "Bushes", "Waters", "Puddles", "ShadowSurfaces", "Planets", "Surfs" });
        private static readonly HashSet<string> References = new HashSet<string>(new[] {
            "target", "attach", "anchor", "enter", "exit", "effect", "requiresFlag", "setFlag", "increment", "counter", "text", "flag", "tree" });
        internal static void Expand(XmlDocument document, Dictionary<string, string> origins)
        {
            XmlElement definitions = document.DocumentElement["ObjectDefinitions"], instances = document.DocumentElement["Objects"];
            if (definitions == null && instances == null) return;
            var objects = new Dictionary<string, XmlElement>(StringComparer.Ordinal);
            if (definitions != null) foreach (XmlElement item in Elements(definitions))
            {
                Attributes(item, "id"); string id = Required(item, "id");
                if (item.Name != "Object" || objects.ContainsKey(id)) throw new InvalidDataException("Duplicate/invalid object definition: " + id);
                var sectionNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (XmlElement section in Elements(item))
                    if ((!Sections.Contains(section.Name) && section.Name != "Parameters") || !sectionNames.Add(section.Name)) throw new InvalidDataException(id + ": unknown/duplicate object section " + section.Name);
                objects.Add(id, item);
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (instances != null) foreach (XmlElement instance in Elements(instances))
            {
                Attributes(instance, "id", "object", "screen", "x", "y");
                string id = Required(instance, "id"); XmlElement definition;
                if (instance.Name != "Instance" || !names.Add(id) || !objects.TryGetValue(Required(instance, "object"), out definition))
                    throw new InvalidDataException("Invalid object instance: " + id);
                if (names.Count > 512) throw new InvalidDataException("Object budget is 512 instances");
                int screen = instance.HasAttribute("screen") ? XmlConvert.ToInt32(instance.GetAttribute("screen")) : 1;
                float x = Number(instance, "x", 0), y = Number(instance, "y", 0);
                var values = new Dictionary<string, string>(StringComparer.Ordinal) { { "id", id } };
                var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (XmlElement argument in Elements(instance))
                {
                    Attributes(argument, "name", "value"); string name = Required(argument, "name");
                    if (argument.Name != "Arg" || arguments.ContainsKey(name)) throw new InvalidDataException(id + ": invalid/duplicate Arg " + name);
                    arguments.Add(name, Required(argument, "value"));
                }
                XmlElement parameters = definition["Parameters"];
                if (parameters != null) foreach (XmlElement parameter in Elements(parameters))
                {
                    Attributes(parameter, "name", "type", "default", "min", "max");
                    string name = Required(parameter, "name"), value;
                    if (parameter.Name != "Param" || values.ContainsKey(name)) throw new InvalidDataException(id + ": invalid/duplicate Param " + name);
                    if (!arguments.TryGetValue(name, out value)) value = Required(parameter, "default");
                    string type = Required(parameter, "type");
                    if (type == "number")
                    {
                        float number = Parse(value);
                        if (number < Number(parameter, "min", -1000000) || number > Number(parameter, "max", 1000000)) throw new InvalidDataException(id + ": parameter out of range: " + name);
                    }
                    else if (type == "boolean") XmlConvert.ToBoolean(value);
                    else if (type != "string") throw new InvalidDataException(id + ": unknown parameter type " + type);
                    values.Add(name, value); arguments.Remove(name);
                }
                if (arguments.Count != 0) throw new InvalidDataException(id + ": unknown argument " + arguments.Keys.First());
                foreach (XmlElement section in Elements(definition))
                {
                    if (section.Name == "Parameters") continue;
                    if (!Sections.Contains(section.Name)) throw new InvalidDataException(id + ": unsupported object section " + section.Name);
                    XmlElement destination = document.DocumentElement[section.Name];
                    if (destination == null) { destination = document.CreateElement(section.Name); document.DocumentElement.AppendChild(destination); }
                    foreach (XmlElement original in Elements(section))
                    {
                        var node = (XmlElement)original.CloneNode(true);
                        foreach (XmlElement element in node.SelectNodes("descendant-or-self::*"))
                            foreach (XmlAttribute attribute in element.Attributes)
                            {
                                attribute.Value = Token.Replace(attribute.Value, match => {
                                    string value; if (!values.TryGetValue(match.Groups[1].Value, out value)) throw new InvalidDataException(id + ": unknown parameter " + match.Value); return value;
                                });
                                if (References.Contains(attribute.Name) && attribute.Value.StartsWith("@", StringComparison.Ordinal)) attribute.Value = id + "." + attribute.Value.Substring(1);
                                if (attribute.Name == "event" || attribute.Name == "stopEvent") attribute.Value = attribute.Value.Replace(":@", ":" + id + ".");
                                if (attribute.Name == "target") attribute.Value = attribute.Value.Replace("anchor:@", "anchor:" + id + ".");
                                if (attribute.Name == "reflectionObjects") attribute.Value = string.Join(";", attribute.Value.Split(';').Select(v => v.Trim().StartsWith("@", StringComparison.Ordinal) ? id + "." + v.Trim().Substring(1) : v.Trim()));
                            }
                        string local = Required(node, "id"); node.SetAttribute("id", id + "." + local);
                        if (section.Name == "Effects" && node.HasAttribute("group")) node.SetAttribute("group", id + "." + node.GetAttribute("group"));
                        if (node.HasAttribute("owner")) node.SetAttribute("owner", id + "." + node.GetAttribute("owner"));
                        bool spatial = section.Name != "Effects" && section.Name != "Rules" && section.Name != "Flags" && section.Name != "BehaviorTrees";
                        if (spatial && !node.HasAttribute("anchor"))
                        {
                            node.SetAttribute("screen", XmlConvert.ToString(screen + (node.HasAttribute("screen") ? XmlConvert.ToInt32(node.GetAttribute("screen")) - 1 : 0)));
                            if (!node.HasAttribute("attach")) Translate(node, section.Name, x, y);
                        }
                        destination.AppendChild(node);
                        string origin; if (origins.TryGetValue(id, out origin)) origins[node.GetAttribute("id")] = origin + " object " + definition.GetAttribute("id") + "/" + local;
                    }
                }
            }
            if (definitions != null) document.DocumentElement.RemoveChild(definitions);
            if (instances != null) document.DocumentElement.RemoveChild(instances);
        }
        private static IEnumerable<XmlElement> Elements(XmlElement element) { return element.ChildNodes.OfType<XmlElement>(); }
        private static string Required(XmlElement element, string name)
        { if (!element.HasAttribute(name) || string.IsNullOrWhiteSpace(element.GetAttribute(name))) throw new InvalidDataException(element.Name + ": missing " + name); return element.GetAttribute(name); }
        private static void Attributes(XmlElement element, params string[] names)
        { foreach (XmlAttribute attribute in element.Attributes) if (!names.Contains(attribute.Name)) throw new InvalidDataException(element.Name + ": unknown attribute " + attribute.Name); }
        private static float Parse(string value)
        { float result = float.Parse(value, CultureInfo.InvariantCulture); if (float.IsNaN(result) || float.IsInfinity(result)) throw new InvalidDataException("Non-finite object coordinate/parameter"); return result; }
        private static float Number(XmlElement element, string name, float omitted) { return element.HasAttribute(name) ? Parse(element.GetAttribute(name)) : omitted; }
        private static void Shift(XmlElement element, string name, float offset) { element.SetAttribute(name, XmlConvert.ToString(Number(element, name, 0) + offset)); }
        private static void Translate(XmlElement node, string section, float x, float y)
        {
            var array = typeof(SceneFile).GetProperties().Single(p => { var a = (XmlArrayAttribute)Attribute.GetCustomAttribute(p, typeof(XmlArrayAttribute)); return a != null && a.ElementName == section; });
            Type type = array.PropertyType.GetElementType(); object baseline = Activator.CreateInstance(type);
            foreach (string member in new[] { "X", "Y" })
            {
                var property = type.GetProperty(member); if (property == null) continue;
                string name = member.ToLowerInvariant(); float offset = member == "X" ? x : y;
                node.SetAttribute(name, XmlConvert.ToString(Number(node, name, Convert.ToSingle(property.GetValue(baseline, null), CultureInfo.InvariantCulture)) + offset));
            }
            if (node.HasAttribute("planeY")) Shift(node, "planeY", y);
            if (node.HasAttribute("glintX") && Number(node, "glintX", -1) >= 0) Shift(node, "glintX", x);
            if (node.HasAttribute("outline")) node.SetAttribute("outline", string.Join(";", SceneValidation.ParsePath(node.GetAttribute("outline"), "object outline").Select(p => XmlConvert.ToString(p.X + x) + "," + XmlConvert.ToString(p.Y + y))));
            foreach (XmlElement impact in node.SelectNodes("Impact")) { Shift(impact, "x", x); Shift(impact, "y", y); }
        }
    }
}
