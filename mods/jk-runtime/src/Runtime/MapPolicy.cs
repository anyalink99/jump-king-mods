using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace JKRuntime
{
    /// <summary>Map-scoped requirements and cooperative restrictions. Never unloads foreign assemblies or changes user settings.</summary>
    public static class MapPolicy
    {
        private sealed class Rule { internal string Id, Mode, Reason; internal Version Minimum; }
        private static Rule[] modules = new Rule[0];
        private static readonly Dictionary<string, string> mechanics = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> foreign = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>Whether the active map allows a declared mechanic. Check before acquiring its player resources.</summary>
        public static bool AllowsMechanic(string id) { RuntimeApi.Kernel.CheckThread(); return !mechanics.ContainsKey(ModuleDefinition.ValidId(id)); }
        /// <summary>Author-provided reason for a denied mechanic; null means unrestricted.</summary>
        public static string MechanicReason(string id) { RuntimeApi.Kernel.CheckThread(); string reason; return mechanics.TryGetValue(ModuleDefinition.ValidId(id), out reason) ? reason : null; }
        /// <summary>Detached human-readable policy declarations for diagnostics.</summary>
        public static string[] Describe()
        { RuntimeApi.Kernel.CheckThread(); return modules.Select(m => m.Mode + " " + m.Id + ": " + m.Reason).Concat(mechanics.Select(m => "deny mechanic " + m.Key + ": " + m.Value)).Concat(foreign.Select(m => "reject assembly " + m.Key + ": " + m.Value)).ToArray(); }
        internal static void Clear() { modules = new Rule[0]; mechanics.Clear(); foreign.Clear(); }
        internal static void Load(string root)
        {
            Clear(); string path = Path.Combine(Path.GetFullPath(root), "jk-runtime", "policy.xml");
            if (!File.Exists(path)) return;
            try
            {
                using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 })) Parse(XElement.Load(reader));
            }
            catch (Exception error) { Clear(); throw new InvalidDataException(path + ": " + error.Message, error); }
        }
        internal static void Parse(XElement root)
        {
            Clear();
            if (root.Name != "MapPolicy" || (string)root.Attribute("version") != "1" || root.Attributes().Count() != 1) throw new InvalidDataException("Expected MapPolicy version=1");
            var next = new List<Rule>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (XElement node in root.Elements())
            {
                if (next.Count + mechanics.Count + foreign.Count >= 256 || node.HasElements || !string.IsNullOrWhiteSpace(node.Value)) throw new InvalidDataException("Invalid policy entry/budget");
                string id = (string)node.Attribute("id"), reason = (string)node.Attribute("reason");
                if (string.IsNullOrWhiteSpace(reason) || reason.Length > 256) throw new InvalidDataException("Every restriction requires a reason (1..256 characters)");
                foreach (var attribute in node.Attributes()) if (!new[] { "id", "reason", "mode", "minimum" }.Contains(attribute.Name.ToString())) throw new InvalidDataException("Unknown policy attribute " + attribute.Name);
                if (node.Name == "Module")
                {
                    ModuleDefinition.ValidId(id); string mode = (string)node.Attribute("mode");
                    if (!new[] { "require", "reject", "suspend" }.Contains(mode) || !ids.Add(id)) throw new InvalidDataException("Invalid/duplicate module rule " + id);
                    Version version = node.Attribute("minimum") == null ? null : Version.Parse((string)node.Attribute("minimum"));
                    if (version != null && mode != "require") throw new InvalidDataException("minimum is only valid for require");
                    next.Add(new Rule { Id = id, Mode = mode, Minimum = version, Reason = reason });
                }
                else if (node.Name == "Mechanic")
                {
                    ModuleDefinition.ValidId(id);
                    if (node.Attribute("mode") != null || node.Attribute("minimum") != null || mechanics.ContainsKey(id)) throw new InvalidDataException("Invalid/duplicate mechanic restriction " + id);
                    mechanics.Add(id, reason);
                }
                else if (node.Name == "Assembly")
                {
                    if (string.IsNullOrWhiteSpace(id) || id.Length > 160 || id.IndexOfAny(new[] { '/', '\\', ',', ':' }) >= 0 || node.Attribute("mode") != null || node.Attribute("minimum") != null || foreign.ContainsKey(id)) throw new InvalidDataException("Expected a unique assembly simple name");
                    foreign.Add(id, reason);
                }
                else throw new InvalidDataException("Unknown policy entry " + node.Name);
            }
            modules = next.ToArray();
        }
        internal static bool Suspends(string id) { return modules.Any(m => m.Id == id && m.Mode == "suspend"); }
        internal static void RequireAvailable(IEnumerable<string> available)
        {
            if (!modules.Any(m => m.Mode == "require")) return;
            var ids = new HashSet<string>(available, StringComparer.Ordinal);
            foreach (Rule rule in modules) if (rule.Mode == "require" && !ids.Contains(rule.Id)) throw new InvalidOperationException("Required map module is unavailable: " + rule.Id + ": " + rule.Reason);
        }
        internal static bool Empty { get { return modules.Length + mechanics.Count + foreign.Count == 0; } }
        internal static bool HasForeignRules { get { return foreign.Count != 0; } }
        internal static void Validate(IEnumerable<ModuleDefinition> definitions, IEnumerable<string> assemblies)
        {
            var found = definitions.ToDictionary(m => m.Id, StringComparer.Ordinal);
            foreach (Rule rule in modules)
            {
                ModuleDefinition module; bool present = found.TryGetValue(rule.Id, out module);
                if (rule.Mode == "require" && (!present || (rule.Minimum != null && module.Version < rule.Minimum))) throw new InvalidOperationException("Map requires " + rule.Id + " " + rule.Minimum + ": " + rule.Reason);
                if (rule.Mode == "reject" && present) throw new InvalidOperationException("Disable module " + rule.Id + " before loading this map: " + rule.Reason);
                if (rule.Mode == "suspend" && present && !module.MapSuspendable) throw new InvalidOperationException(rule.Id + " does not support map-scoped suspension; disable it explicitly: " + rule.Reason);
            }
            foreach (string assembly in assemblies) if (foreign.ContainsKey(assembly)) throw new InvalidOperationException("Disable foreign assembly " + assembly + " and restart: " + foreign[assembly]);
        }
    }
}
