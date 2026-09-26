using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// Separate process: never load two JKRuntime versions into one binding context.
internal static class ApiSurface
{
    private static string Name(Type type)
    {
        if (type.IsByRef) return Name(type.GetElementType()) + "&";
        if (type.IsArray) return Name(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsGenericParameter) return "!" + type.GenericParameterPosition;
        if (!type.IsGenericType) return type.FullName;
        return type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(Name)) + ">";
    }
    private static string Constraints(Type[] args)
    {
        return string.Join(";", args.Where(t => t.IsGenericParameter).Select(t => Name(t) + ":" + t.GenericParameterAttributes + ":" +
            string.Join(",", t.GetGenericParameterConstraints().Select(Name).OrderBy(n => n, StringComparer.Ordinal))));
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
            var lines = new SortedSet<string>(StringComparer.Ordinal);
            var assembly = Assembly.LoadFrom(args[0]);
            lines.Add("assembly " + assembly.FullName);
            foreach (Type type in assembly.GetExportedTypes())
            {
                string prefix = Name(type);
                lines.Add("type " + prefix + " " + (type.Attributes & (TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Sealed)) + " " + Constraints(type.GetGenericArguments()));
                if (type.BaseType != null) lines.Add("base " + prefix + " " + Name(type.BaseType));
                foreach (Type iface in type.GetInterfaces()) lines.Add("implements " + prefix + " " + Name(iface));
                const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                foreach (MethodBase method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)))
                {
                    if (!(method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly)) continue;
                    var info = method as MethodInfo;
                    lines.Add("method " + prefix + "." + method.Name + " " + method.Attributes + " " + (info == null ? "ctor" : Name(info.ReturnType)) +
                        "(" + string.Join(",", method.GetParameters().Select(p => Name(p.ParameterType) + (p.IsOut ? " out" : ""))) + ") " +
                        (method.IsGenericMethod ? Constraints(method.GetGenericArguments()) : ""));
                }
                foreach (FieldInfo field in type.GetFields(flags))
                    if (field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly)
                        lines.Add("field " + prefix + "." + field.Name + " " + field.Attributes + " " + Name(field.FieldType) +
                            (type.IsEnum && field.IsLiteral ? "=" + Convert.ToInt64(field.GetRawConstantValue()) : ""));
            }
            lines = new SortedSet<string>(lines.Select(line => line.TrimEnd()), StringComparer.Ordinal);
            if (args.Length == 2) { foreach (string line in lines) Console.WriteLine(line); return 0; }
            string[] missing = File.ReadAllLines(args[2]).Select(line => line.TrimEnd()).Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#") && !lines.Contains(line)).ToArray();
            if (missing.Length != 0) throw new Exception("Public ABI changed:\n" + string.Join("\n", missing));
            Console.WriteLine("[OK] Frozen public type/member ABI retained: " + Path.GetFileName(args[2]));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
