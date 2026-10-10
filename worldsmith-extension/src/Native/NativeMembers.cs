using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace WorldsmithExtension
{
    internal static class NativeMembers
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        static readonly ConcurrentDictionary<string, MemberInfo> Cache = new ConcurrentDictionary<string, MemberInfo>();
        static string Key(Type type, string name) { return type.AssemblyQualifiedName + ":" + name; }

        internal static MemberInfo Value(Type type, string name)
        {
            return Cache.GetOrAdd(Key(type, name), key =>
            {
                MemberInfo value = type.GetProperty(name, Flags) ?? (MemberInfo)type.GetField(name, Flags);
                if (value == null) throw new MissingMemberException(type.FullName, name);
                return value;
            });
        }

        internal static MethodInfo Method(Type type, string name, params Type[] parameters)
        {
            string signature = name + "(" + String.Join(",", parameters.Select(p => p == null ? "null" : p.AssemblyQualifiedName)) + ")";
            return (MethodInfo)Cache.GetOrAdd(Key(type, signature), key =>
            {
                if (parameters.All(p => p != null))
                {
                    var exact = type.GetMethod(name, Flags, null, parameters, null);
                    if (exact != null) return exact;
                }
                var matches = type.GetMethods(Flags).Where(method => method.Name == name && !method.ContainsGenericParameters &&
                    method.GetParameters().Length == parameters.Length && method.GetParameters().Select((p, i) =>
                        parameters[i] == null ? !p.ParameterType.IsValueType || Nullable.GetUnderlyingType(p.ParameterType) != null : p.ParameterType.IsAssignableFrom(parameters[i])).All(v => v)).ToArray();
                if (matches.Length == 1) return matches[0];
                throw new MissingMethodException("Unsupported editor member: " + type.FullName + "." + signature + "; compatible overloads: " + matches.Length);
            });
        }
    }
}
