using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JKRuntime
{
    // A reversible group using the already loaded engine; never adds a Harmony dependency.
    internal sealed class DiagnosticHooks : IDisposable
    {
        private readonly object owner;
        private readonly Type metadata;
        private readonly MethodInfo patch, unpatch;
        private readonly List<Tuple<MethodInfo, MethodInfo>> applied = new List<Tuple<MethodInfo, MethodInfo>>();
        internal const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal DiagnosticHooks(string id)
        {
            var engines = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "0Harmony").ToArray();
            if (engines.Length != 1) throw new InvalidOperationException("Expected one loaded Harmony engine; found " + engines.Length);
            Type harmony = engines[0].GetType("HarmonyLib.Harmony", true);
            metadata = engines[0].GetType("HarmonyLib.HarmonyMethod", true);
            owner = Activator.CreateInstance(harmony, new object[] { id });
            patch = harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
            unpatch = harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) });
        }
        internal void Add(Type targetType, string method, Type handlerType, string before, string after)
        {
            MethodInfo target = targetType.GetMethod(method, Flags);
            if (target == null) throw new MissingMethodException(targetType.FullName, method);
            Add(target,handlerType,before,after);
        }
        internal void Add(MethodInfo target, Type handlerType, string before, string after)
        {
            if(target==null || target.ContainsGenericParameters) throw new ArgumentException("A concrete method is required", "target");
            object prefix = Entry(target, handlerType, before, 800);
            object postfix = Entry(target, handlerType, after, 0);
            patch.Invoke(owner, new[] { (object)target, prefix, postfix, null, null });
        }
        private object Entry(MethodInfo target, Type type, string name, int priority)
        {
            if (name == null) return null;
            MethodInfo callback = type.GetMethod(name, Flags);
            if (callback == null) throw new MissingMethodException(type.FullName, name);
            var entry = Activator.CreateInstance(metadata, new object[] { callback });
            metadata.GetField("priority").SetValue(entry, priority);
            applied.Add(Tuple.Create(target, callback));
            return entry;
        }
        public void Dispose()
        {
            // Retain failed removals so callers can retry, rather than lose ownership.
            for (int i = applied.Count - 1; i >= 0; i--)
            { unpatch.Invoke(owner, new object[] { applied[i].Item1, applied[i].Item2 }); applied.RemoveAt(i); }
        }
    }
}
