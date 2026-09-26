using System;
using System.Collections.Generic;
using System.Reflection;

namespace MegaGameplayExpansion
{
    // Generic property interception also holds values when a provider updates its
    // backing field later in the tick. Raw field reads cannot be intercepted here.
    internal static class GimmickGetters
    {
        private static readonly Dictionary<MethodBase, List<Lease>> values = new Dictionary<MethodBase, List<Lease>>();
        private static readonly HashSet<MethodInfo> prepared = new HashSet<MethodInfo>();
        private sealed class Lease : IDisposable
        {
            internal MethodInfo Method;
            internal object Target, Value;
            public void Dispose()
            {
                lock (values)
                {
                    List<Lease> list;
                    if (values.TryGetValue(Method, out list)) { list.Remove(this); if (list.Count == 0) values.Remove(Method); }
                }
            }
        }
        internal static void Prepare(MethodInfo method)
        {
            if (prepared.Contains(method)) return;
            if (method.ContainsGenericParameters || method.IsAbstract || method.GetParameters().Length != 0)
                throw new InvalidOperationException("This property has no interceptable getter");
            MethodInfo hook = typeof(GimmickGetters).GetMethod(method.IsStatic ? "StaticRead" : "InstanceRead", Gimmicks.Members).MakeGenericMethod(method.ReturnType);
            GimmickBlocks.HookGetter(method, hook);
            prepared.Add(method);
        }
        internal static IDisposable Hold(MethodInfo method, object target, object value)
        {
            Prepare(method);
            var lease = new Lease { Method = method, Target = target, Value = value };
            lock (values)
            {
                List<Lease> list;
                if (!values.TryGetValue(method, out list)) values.Add(method, list = new List<Lease>());
                if (list.Exists(item => ReferenceEquals(item.Target, target))) throw new InvalidOperationException("This property already has an active override");
                list.Add(lease);
            }
            return lease;
        }
        private static void StaticRead<T>(MethodBase __originalMethod, ref T __result) { Read(__originalMethod, null, ref __result); }
        private static void InstanceRead<T>(MethodBase __originalMethod, object __instance, ref T __result) { Read(__originalMethod, __instance, ref __result); }
        private static void Read<T>(MethodBase method, object instance, ref T result)
        {
            lock (values)
            {
                List<Lease> list;
                if (!values.TryGetValue(method, out list)) return;
                foreach (var item in list) if (ReferenceEquals(item.Target, instance)) { result = (T)item.Value; return; }
            }
        }
    }
}
