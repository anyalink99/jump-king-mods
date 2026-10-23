using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class RecipeCompiler
    {
        private static readonly Regex Token = new Regex(@"\$\{([a-zA-Z0-9_.-]+)\}");
        private static readonly string[] Sections = { "Assets", "Components", "Graph", "Bindings", "Variables", "Effects", "Rules", "Exports", "Trees" };
        private readonly ComponentRegistry registry;
        private readonly Dictionary<string, XElement> recipes = new Dictionary<string, XElement>(StringComparer.Ordinal);
        private int expanded;
        internal RecipeCompiler(ComponentRegistry descriptors) { registry = descriptors; }
        internal SceneDocument Read(string root)
        {
            var source=Source(root);return source==null?null:Compile(source);
        }
        internal static XDocument Source(string root)
        {
            root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            string path = Path.Combine(root, "mapping_settings.xml"); if (!File.Exists(path)) return null;
            return ReadModules(path, root, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
        }
        internal SceneDocument Compile(XDocument source)
        {
            recipes.Clear(); expanded = 0;
            ValidateDepth(source.Root);
            if (source.Root == null || source.Root.Name != "MappingSettings" || (string)source.Root.Attribute("version") != "1") throw new InvalidDataException("Expected MappingSettings version 1");
            foreach (var resource in typeof(RecipeCompiler).Assembly.GetManifestResourceNames().Where(n => n.StartsWith("MegaMapping.Recipes.", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal))
                using (var stream = typeof(RecipeCompiler).Assembly.GetManifestResourceStream(resource)) using (var reader = XmlReader.Create(stream, ReaderSettings())) LoadRecipes(XDocument.Load(reader).Root);
            var document = new XDocument(source); var definitions = document.Root.Element("Recipes");
            if (definitions != null) { LoadRecipes(definitions); definitions.Remove(); }
            var instances = document.Root.Element("Instances");
            if (instances != null)
            {
                Attributes(instances); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var instance in instances.Elements())
                {
                    string id = Required(instance, "id"); if (!ids.Add(id)) throw new InvalidDataException("Duplicate recipe instance: " + id);
                    Expand(instance, document.Root, "", new HashSet<string>(StringComparer.Ordinal), 0);
                }
                instances.Remove();
            }
            foreach (var section in document.Root.Elements()) if (!Sections.Contains(section.Name.LocalName) && section.Name != "MapLayout") throw new InvalidDataException("Unknown scene section: " + section.Name);
            foreach (var group in document.Root.Elements().GroupBy(e => e.Name)) if (group.Count() != 1) throw new InvalidDataException("Duplicate scene section: " + group.Key);
            ResolvePorts(document.Root);
            return Deserialize(document);
        }
        internal static SceneDocument Deserialize(XDocument document)
        {
            ValidateDepth(document.Root);
            var serializer = new XmlSerializer(typeof(SceneDocument));
            serializer.UnknownAttribute += (sender, args) => { throw new InvalidDataException("Unknown scene attribute: " + args.Attr.Name); };
            serializer.UnknownElement += (sender, args) => { throw new InvalidDataException("Unknown scene element: " + args.Element.Name); };
            serializer.UnknownNode += (sender, args) => { if ((args.NodeType == XmlNodeType.Text || args.NodeType == XmlNodeType.CDATA) && !string.IsNullOrWhiteSpace(args.Text)) throw new InvalidDataException("Unexpected scene text"); };
            using (var reader = document.CreateReader()) return (SceneDocument)serializer.Deserialize(reader);
        }
        private void LoadRecipes(XElement definitions)
        {
            if (definitions == null || definitions.Name != "Recipes") throw new InvalidDataException("Recipe library needs a Recipes root");
            Attributes(definitions);
            foreach (var recipe in definitions.Elements())
            {
                if (recipe.Name != "Recipe") throw new InvalidDataException("Recipe library contains an unknown element");
                Attributes(recipe, "id"); string id = Required(recipe, "id");
                if (recipes.ContainsKey(id)) throw new InvalidDataException("Duplicate recipe: " + id);
                foreach (var section in recipe.Elements()) if (!Sections.Contains(section.Name.LocalName) && section.Name != "Parameters" && section.Name != "Instances") throw new InvalidDataException(id + ": unknown recipe section " + section.Name);
                recipes.Add(id, new XElement(recipe));
            }
        }
        private void Expand(XElement instance, XElement output, string parent, HashSet<string> active, int depth)
        {
            Attributes(instance, "id", "recipe");
            if (instance.Name != "Instance" || depth > 16 || ++expanded > 1024) throw new InvalidDataException("Recipe expansion exceeds its nesting or instance budget");
            string id = parent + Required(instance, "id"), name = Required(instance, "recipe"); XElement recipe;
            if (!recipes.TryGetValue(name, out recipe) || !active.Add(name)) throw new InvalidDataException(id + ": unknown or cyclic recipe " + name);
            var values = new Dictionary<string, string>(StringComparer.Ordinal) { { "id", id } };
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var arg in instance.Elements())
            {
                if (arg.Name != "Arg") throw new InvalidDataException(id + ": Instance accepts only Arg children");
                Attributes(arg, "name", "value"); string key = Required(arg, "name"); if (arguments.ContainsKey(key)) throw new InvalidDataException(id + ": duplicate argument " + key);
                arguments.Add(key, Required(arg, "value", true));
            }
            var parameters = recipe.Element("Parameters");
            if (parameters != null) foreach (var parameter in parameters.Elements())
            {
                Attributes(parameter, "name", "kind", "default", "min", "max");
                if (parameter.Name != "Param") throw new InvalidDataException("Parameters accepts Param children");
                string key = Required(parameter, "name"), text;
                if (!arguments.TryGetValue(key, out text)) text = Required(parameter, "default", true);
                SceneValueKind kind; if (!Enum.TryParse<SceneValueKind>(Required(parameter, "kind"), false, out kind) || !Enum.IsDefined(typeof(SceneValueKind), kind)) throw new InvalidDataException(id + ": unknown parameter kind");
                SceneValue value = SceneValue.Parse(kind, text);
                double min = parameter.Attribute("min") == null ? -1000000 : SceneValue.Parse(SceneValueKind.Number, (string)parameter.Attribute("min")).X;
                double max = parameter.Attribute("max") == null ? 1000000 : SceneValue.Parse(SceneValueKind.Number, (string)parameter.Attribute("max")).X;
                new FieldDescriptor(key, ValueGraph.Zero(kind), min, max).Validate(value);
                if (values.ContainsKey(key)) throw new InvalidDataException(id + ": duplicate parameter " + key);
                values.Add(key, value.ToString()); arguments.Remove(key);
            }
            if (arguments.Count != 0) throw new InvalidDataException(id + ": unknown argument " + arguments.Keys.First());
            var body = new XElement(recipe);
            foreach (var attribute in body.Descendants().Attributes()) attribute.Value = Token.Replace(attribute.Value, match => {
                string value; if (!values.TryGetValue(match.Groups[1].Value, out value)) throw new InvalidDataException(id + ": unknown recipe parameter " + match.Value); return value;
            });
            foreach (var section in body.Elements().Where(e => Sections.Contains(e.Name.LocalName)))
            {
                var destination = output.Element(section.Name); if (destination == null) { destination = new XElement(section.Name); output.Add(destination); }
                foreach (var child in section.Elements())
                {
                    var item = new XElement(child); item.SetAttributeValue("id", id + "." + Required(item, "id"));
                    Qualify(item, id); destination.Add(item);
                }
            }
            var nested = body.Element("Instances"); if (nested != null) foreach (var item in nested.Elements())
            {
                var child=new XElement(item);foreach(var argument in child.Elements("Arg")){var value=argument.Attribute("value");if(value!=null)value.Value=Local(value.Value,id);}Expand(child, output, id + ".", active, depth + 1);
            }
            active.Remove(name);
        }
        private void Qualify(XElement item, string instance)
        {
            Func<string, string> local = value => Local(value,instance);
            foreach (var attribute in item.DescendantsAndSelf().Attributes())
            {
                if (attribute.Name == "target" || attribute.Name == "source" || attribute.Name == "condition" || attribute.Name == "event" || attribute.Name == "stopEvent") attribute.Value = local(attribute.Value);
                if (attribute.Name == "inputs") attribute.Value = string.Join(";", attribute.Value.Split(';').Select(local));
            }
            if (item.Name == "Component")
            {
                var descriptor = registry.Require(Required(item, "kind"));
                foreach (var value in item.Elements("Value"))
                {
                    var field = descriptor.Fields[descriptor.Index(Required(value, "name"))];
                    if (field.Reference == ReferenceKind.Component || field.Reference == ReferenceKind.Asset || field.Reference == ReferenceKind.Signal) value.SetAttributeValue("value", local((string)value.Attribute("value")));
                    if (field.Reference == ReferenceKind.Selection) value.SetAttributeValue("value", string.Join(";", ((string)value.Attribute("value") ?? "").Split(';').Select(local)));
                }
            }
        }
        private static XmlReaderSettings ReaderSettings() { return new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2000000 }; }
        private static XDocument ReadModules(string path, string root, HashSet<string> active, int depth)
        {
            path = Path.GetFullPath(path);
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || depth > 16 || !active.Add(path)) throw new InvalidDataException("Unsafe or cyclic scene include: " + path);
            XDocument document; using (var reader = XmlReader.Create(path, ReaderSettings())) document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            ValidateDepth(document.Root);
            if (document.Root == null || (document.Root.Name != "MappingSettings" && document.Root.Name != "MappingFragment") || (string)document.Root.Attribute("version") != "1") throw new InvalidDataException(path + ": expected MappingSettings or MappingFragment version 1");
            if(depth==0&&document.Root.Name!="MappingSettings"||depth>0&&document.Root.Name!="MappingFragment")throw new InvalidDataException(path+": the entry must be MappingSettings and includes must be MappingFragment");
            Attributes(document.Root,depth==0?new[]{"version","saveId"}:new[]{"version"});
            foreach (var include in document.Root.Elements("Include").ToArray())
            {
                Attributes(include, "src"); var module = ReadModules(Path.Combine(Path.GetDirectoryName(path), Required(include, "src")), root, active, depth + 1);
                foreach (var section in module.Root.Elements())
                {
                    if (section.Name == "MapLayout") throw new InvalidDataException("MapLayout belongs to the entry file");
                    var destination = document.Root.Element(section.Name);
                    if (destination == null) document.Root.Add(new XElement(section)); else destination.Add(section.Elements().Select(e => new XElement(e)));
                }
                include.Remove();
            }
            active.Remove(path); return document;
        }
        private static void Attributes(XElement element, params string[] allowed)
        { foreach (var attribute in element.Attributes()) if (!allowed.Contains(attribute.Name.LocalName) || attribute.Name.NamespaceName != "") throw new InvalidDataException(element.Name + ": unknown attribute " + attribute.Name); }
        private static string Required(XElement element, string name, bool empty = false)
        { var attribute = element.Attribute(name); if (attribute == null || (!empty && string.IsNullOrWhiteSpace(attribute.Value))) throw new InvalidDataException(element.Name + ": missing " + name); return attribute.Value; }
    }
}
