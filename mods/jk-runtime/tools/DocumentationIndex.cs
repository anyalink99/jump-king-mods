using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;

// Build-time metadata only: never instantiate a provider or read a static field.
internal static class DocumentationIndex
{
    private const BindingFlags Flags = BindingFlags.DeclaredOnly | BindingFlags.Public
        | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static bool Visible(MethodBase m)
    { return m != null && (m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly); }
    private static string Name(Type t)
    {
        if (t.IsByRef) return Name(t.GetElementType());
        if (t.IsArray) return Name(t.GetElementType()) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
        if (t.IsGenericParameter) return t.Name;
        if (!t.IsGenericType) return (t.FullName ?? t.Name).Replace('+', '.');
        string name = t.GetGenericTypeDefinition().FullName.Replace('+', '.');
        // Reflection names carry arity on each generic declaring type.
        name = System.Text.RegularExpressions.Regex.Replace(name, @"`\d+", "");
        return name + "<" + string.Join(", ", t.GetGenericArguments().Select(Name)) + ">";
    }
    private static string Literal(object value)
    {
        if (value == null) return "null";
        if (value is string || value is char) return new JavaScriptSerializer().Serialize(value);
        if (value is bool) return (bool)value ? "true" : "false";
        if (value == Missing.Value || value == DBNull.Value) return "[optional]";
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }
    private static string Parameter(ParameterInfo p)
    {
        string prefix = p.IsOut ? "out " : p.ParameterType.IsByRef ? "ref " :
            p.IsDefined(typeof(ParamArrayAttribute), false) ? "params " : "";
        return prefix + Name(p.ParameterType) + " " + p.Name +
            (p.IsOptional ? " = " + Literal(p.DefaultValue) : "");
    }
    private static string Access(MethodBase m)
    { return m.IsPublic ? "public " : m.IsFamilyOrAssembly ? "protected internal " : "protected "; }
    private static string Constraints(IEnumerable<Type> args)
    {
        var clauses = new List<string>();
        foreach (Type t in args.Where(a => a.IsGenericParameter))
        {
            var parts = new List<string>();
            GenericParameterAttributes a = t.GenericParameterAttributes;
            if ((a & GenericParameterAttributes.ReferenceTypeConstraint) != 0) parts.Add("class");
            bool value = (a & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0;
            if (value) parts.Add("struct");
            parts.AddRange(t.GetGenericParameterConstraints().Where(c => c != typeof(ValueType)).Select(Name));
            if (!value && (a & GenericParameterAttributes.DefaultConstructorConstraint) != 0) parts.Add("new()");
            if (parts.Count != 0) clauses.Add("where " + t.Name + " : " + string.Join(", ", parts));
        }
        return clauses.Count == 0 ? "" : " " + string.Join(" ", clauses);
    }
    public static int Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs request) {
                string name = new AssemblyName(request.Name).Name;
                foreach (string extension in new[] { ".dll", ".exe" }) {
                    string path = Path.Combine(args[1], name + extension);
                    if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            var assembly = Assembly.LoadFrom(args[0]);
            var summaries = XDocument.Load(args[2]).Descendants("member")
                .Where(x => x.Element("summary") != null)
                .ToDictionary(x => (string)x.Attribute("name"), x =>
                    System.Text.RegularExpressions.Regex.Replace(x.Element("summary").Value.Trim(), @"\s+", " "));
            var types = new List<object>();
            foreach (Type t in assembly.GetExportedTypes().OrderBy(Name, StringComparer.Ordinal))
            {
                var members = new List<string>();
                foreach (MethodBase m in t.GetConstructors(Flags).Cast<MethodBase>().Concat(t.GetMethods(Flags)))
                {
                    if (!Visible(m) || (m.IsSpecialName && !m.IsConstructor && !m.Name.StartsWith("op_"))) continue;
                    var method = m as MethodInfo;
                    members.Add(Access(m) + (m.IsStatic ? "static " : "") + (m.IsAbstract ? "abstract " : m.IsVirtual ? "virtual " : "") +
                        (method == null ? t.Name.Split('`')[0] : Name(method.ReturnType) + " " + m.Name) +
                        (m.IsGenericMethod ? "<" + string.Join(", ", m.GetGenericArguments().Select(Name)) + ">" : "") +
                        "(" + string.Join(", ", m.GetParameters().Select(Parameter)) + ")" +
                        (m.IsGenericMethod ? Constraints(m.GetGenericArguments()) : "") + ";");
                }
                foreach (PropertyInfo p in t.GetProperties(Flags))
                {
                    var get = p.GetGetMethod(true); var set = p.GetSetMethod(true);
                    if (!Visible(get) && !Visible(set)) continue;
                    var accessor = Visible(get) ? get : set;
                    var parameters = p.GetIndexParameters();
                    // Accessor visibility is explicit, including a non-public setter.
                    members.Add((accessor.IsStatic ? "static " : "") + Name(p.PropertyType) + " " +
                        (parameters.Length == 0 ? p.Name : "this[" + string.Join(", ", parameters.Select(Parameter)) + "]") +
                        " { " + (Visible(get) ? Access(get) + "get; " : "") + (Visible(set) ? Access(set) + "set; " : "") + "}");
                }
                foreach (EventInfo e in t.GetEvents(Flags))
                {
                    var add = e.GetAddMethod(true);
                    if (Visible(add)) members.Add(Access(add) + (add.IsStatic ? "static " : "") + "event " + Name(e.EventHandlerType) + " " + e.Name + ";");
                }
                foreach (FieldInfo f in t.GetFields(Flags))
                {
                    if (!(f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly) || f.Name == "value__") continue;
                    members.Add((f.IsPublic ? "public " : f.IsFamilyOrAssembly ? "protected internal " : "protected ") +
                        (f.IsLiteral ? "const " : (f.IsStatic ? "static " : "") + (f.IsInitOnly ? "readonly " : "")) +
                        Name(f.FieldType) + " " + f.Name + (f.IsLiteral ? " = " + Literal(f.GetRawConstantValue()) : "") + ";");
                }
                string summary;
                summaries.TryGetValue("T:" + t.FullName.Replace('+', '.'), out summary);
                types.Add(new { name = Name(t), ns = t.Namespace, summary = summary,
                    kind = t.IsEnum ? "enum" : t.IsInterface ? "interface" : t.IsValueType ? "struct" : "class",
                    constraints = Constraints(t.GetGenericArguments()),
                    baseType = t.BaseType == null ? null : Name(t.BaseType),
                    interfaces = t.GetInterfaces().Select(Name).OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                    members = members.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                    staticMembers = t.GetMembers(BindingFlags.Public | BindingFlags.Static).Select(m => m.Name).Distinct().ToArray() });
            }
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            string runtimeVersion = (string)assembly.GetType("JKRuntime.RuntimeApi").GetField("Version").GetRawConstantValue();
            File.WriteAllText(args[3], serializer.Serialize(new { assembly = assembly.FullName, runtimeVersion = runtimeVersion, types = types }), new UTF8Encoding(false));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
