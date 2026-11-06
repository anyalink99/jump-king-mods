using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JumpKing.Controller;
using JKRuntime.Inspection;

namespace JKRuntime.UI
{
    // recognize a held-state producer and every ordinary consumer of its private
    // bit. Names like Sprint/Save/Toggle aren't evidence of input semantics
    internal static class BindingModeDiscovery
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private static readonly Dictionary<Assembly, Dictionary<MethodInfo, GimmickInstruction[]>> assemblies = new Dictionary<Assembly, Dictionary<MethodInfo, GimmickInstruction[]>>();
        internal static UiBindingModeOption Discover(string id, Assembly assembly, PropertyInfo bindings, object key)
        {
            try
            {
                var member = key == null || !key.GetType().IsEnum ? null : key.GetType().GetField(key.ToString());
                var declared = member == null ? null : Attribute.GetCustomAttribute(member, typeof(UiBindingModeAttribute)) as UiBindingModeAttribute;
                if (declared != null) return new UiBindingModeOption(declared.Mode, "Provider-declared input mode");
                var engine = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "0Harmony");
                if (engine == null) return null;
                var info = engine.GetType("HarmonyLib.Harmony").GetMethod("GetPatchInfo").Invoke(null, new object[] { typeof(ControllerManager).GetMethod("Update") });
                var patches = info == null ? new object[0] : info.GetType().GetField("Postfixes").GetValue(info) as IEnumerable;
                var writers = new List<MethodInfo>();
                foreach (object patch in patches) {
                    var method = patch.GetType().GetProperty("PatchMethod").GetValue(patch, null) as MethodInfo;
                    if (method != null && method.DeclaringType.Assembly == assembly) writers.Add(method);
                }
                FieldInfo field;
                using (StartupTrace.Measure("input-modes.inspect")) field = Analyze(assembly, bindings, key, writers);
                if (field != null) {
                    if (!BindingActivation.Install()) return new UiBindingModeOption(UiBindingMode.Hold, "Input conversion is unavailable");
                    var query = engine.GetType("HarmonyLib.Harmony").GetMethod("GetPatchInfo");
                    var methods = assemblies[assembly];
                    var accessors = methods.Where(p => p.Value.Any(i => Equals(i.Operand, field))).Select(p => p.Key).ToArray();
                    var proof = methods.Where(p => accessors.Contains(p.Key) || p.Value.Any(i => accessors.Contains(i.Operand as MethodInfo))).Select(p => p.Key).ToArray();
                    if (proof.Any(m => query.Invoke(null, new object[] { m }) != null)) return null;
                    string graph = PatchGraph(query);
                    return BindingActivation.RegisterForeign(id, field, () => PatchGraph(query) == graph && proof.All(m => query.Invoke(null, new object[] { m }) == null));
                }
                return ReadsOnlyEdges(assembly, bindings, key) ? new UiBindingModeOption(UiBindingMode.Press,
                    "Edge-triggered action; no reversible state contract") : null;
            }
            catch (Exception error) {
                Console.WriteLine("[JK Runtime UI] Mode not inferred for " + id + ": " + error.GetBaseException().Message);
                return null;
            }
        }
        private static string PatchGraph(MethodInfo query)
        {
            var info = query.Invoke(null, new object[] { typeof(ControllerManager).GetMethod("Update") });
            if (info == null) return "";
            var parts = new List<string>();
            foreach (string kind in new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers" })
                foreach (object patch in (IEnumerable)info.GetType().GetField(kind).GetValue(info)) {
                    var type = patch.GetType(); var method = (MethodInfo)type.GetProperty("PatchMethod").GetValue(patch, null);
                    parts.Add(kind + ":" + method.Module.ModuleVersionId + ":" + method.MetadataToken + ":" + type.GetField("priority").GetValue(patch)
                        + ":" + string.Join(",", ((string[])type.GetField("before").GetValue(patch)) ?? new string[0]) + ":" + string.Join(",", ((string[])type.GetField("after").GetValue(patch)) ?? new string[0]));
                }
            return string.Join("|", parts);
        }
        internal static FieldInfo Analyze(Assembly assembly, PropertyInfo bindings, object key, IEnumerable<MethodInfo> writers)
        {
            Dictionary<MethodInfo, GimmickInstruction[]> methods;
            if (!assemblies.TryGetValue(assembly, out methods)) {
                methods = new Dictionary<MethodInfo, GimmickInstruction[]>();
                foreach (var type in assembly.GetTypes()) foreach (var method in type.GetMethods(Flags)) {
                    if (method.GetMethodBody() == null) continue;
                    methods.Add(method, GimmickIl.Read(method).Where(i => i.Code != OpCodes.Nop && !(i.Code == OpCodes.Castclass && Equals(i.Operand, typeof(IEnumerable<int>)))).ToArray());
                }
                if (!assembly.IsDynamic) assemblies.Add(assembly, methods);
            }
            var matches = new List<FieldInfo>();
            foreach (var writer in writers.Distinct())
            {
                GimmickInstruction[] code; if (!methods.TryGetValue(writer, out code)) continue;
                for (int i = 2; i + 6 < code.Length; i++)
                {
                    if (!Equals(code[i].Operand, bindings.GetGetMethod(true)) || !Key(code[i + 1], key)) continue;
                    var item = code[i + 2].Operand as MethodInfo;
                    if (item == null || item.Name != "get_Item" || item.DeclaringType != bindings.PropertyType) continue;
                    if (!Equals(code[i - 2].Operand, typeof(IPad).GetMethod("GetPressedButtons"))) continue;
                    if (code[i + 3].Code != OpCodes.Ldftn || !Linq(code[i + 3], "Contains") || code[i + 4].Code != OpCodes.Newobj
                        || !Linq(code[i + 5], "Any")) continue;
                    var ctor = code[i + 4].Operand as ConstructorInfo;
                    if (ctor == null || ctor.DeclaringType != typeof(Func<int, bool>)) continue;
                    var setter = code[i + 6].Operand as MethodInfo;
                    GimmickInstruction[] set;
                    if (setter == null || !setter.IsStatic || !methods.TryGetValue(setter, out set) || set.Length != 3
                        || set[0].Code != OpCodes.Ldarg_0 || set[1].Code != OpCodes.Stsfld || set[2].Code != OpCodes.Ret) continue;
                    var field = set[1].Operand as FieldInfo;
                    if (field == null || !field.IsPrivate || field.FieldType != typeof(bool)) continue;
                    var getter = methods.FirstOrDefault(p => p.Value.Length == 2 && p.Value[0].Code == OpCodes.Ldsfld
                        && Equals(p.Value[0].Operand, field) && p.Value[1].Code == OpCodes.Ret).Key;
                    if (getter == null) continue;
                    bool safe = true, consumed = false;
                    foreach (var pair in methods)
                    {
                        for (int at = 0; at < pair.Value.Length; at++) {
                            var instruction = pair.Value[at];
                            if (Equals(instruction.Operand, field) && pair.Key != setter && pair.Key != getter) safe = false;
                            if (Equals(instruction.Operand, setter) && (pair.Key != writer || at != i + 6 && (at == 0 || pair.Value[at - 1].Code != OpCodes.Ldc_I4_0))) safe = false;
                            if (Equals(instruction.Operand, getter) && pair.Key != writer) {
                                bool arithmetic = ArithmeticConsumer(pair.Key, pair.Value, methods);
                                safe &= arithmetic; consumed |= arithmetic;
                            }
                        }
                    }
                    if (safe && consumed) matches.Add(field);
                }
            }
            return matches.Distinct().Count() == 1 ? matches[0] : null;
        }
        internal static bool ReadsOnlyEdges(Assembly assembly, PropertyInfo bindings, object key)
        {
            Dictionary<MethodInfo, GimmickInstruction[]> methods;
            if (!assemblies.TryGetValue(assembly, out methods)) { Analyze(assembly, bindings, key, new MethodInfo[0]); methods = assemblies[assembly]; }
            foreach (var input in methods)
            for (int at = 0; at + 4 < input.Value.Length; at++)
            {
                var code = input.Value;
                if (!Equals(code[at].Operand, bindings.GetGetMethod(true)) || !Key(code[at + 1], key)) continue;
                var item = code[at + 2].Operand as MethodInfo; var match = code[at + 3].Operand as MethodInfo;
                var field = code[at + 4].Operand as FieldInfo;
                if (item == null || item.Name != "get_Item" || item.DeclaringType != bindings.PropertyType || match == null
                    || match.ReturnType != typeof(bool) || !match.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(int[]), typeof(int[]) })
                    || code[at + 4].Code != OpCodes.Stfld || field == null || field.FieldType != typeof(bool) || !field.DeclaringType.IsValueType) continue;
                foreach (var edge in methods.Where(p => p.Key.ReturnType == field.DeclaringType))
                {
                    var reads = edge.Value.Where(i => i.Code == OpCodes.Ldfld && Equals(i.Operand, field)).ToArray();
                    if (reads.Length != 2 || edge.Value.Count(i => i.Code == OpCodes.Stfld && Equals(i.Operand, field)) != 1) continue;
                    // !previous && current: the first true branch skips current and writes false
                    int first = Array.IndexOf(edge.Value, reads[0]), second = Array.IndexOf(edge.Value, reads[1]);
                    if (first + 1 >= second || (edge.Value[first + 1].Code != OpCodes.Brtrue && edge.Value[first + 1].Code != OpCodes.Brtrue_S)) continue;
                    var zero = edge.Value.FirstOrDefault(i => i.Offset == (int)edge.Value[first + 1].Operand);
                    if (zero == null || zero.Code != OpCodes.Ldc_I4_0) continue;
                    if (edge.Value.Any(i => i.Code.FlowControl == FlowControl.Call || i.Code == OpCodes.Stsfld || i.Code == OpCodes.Starg || i.Code == OpCodes.Starg_S)) continue;
                    bool consumed = false, safe = true;
                    foreach (var reader in methods.Where(p => p.Key != input.Key && p.Key != edge.Key))
                        if (reader.Value.Any(i => Equals(i.Operand, field))) {
                            bool usesEdges = reader.Value.Any(i => Equals(i.Operand, edge.Key));
                            safe &= usesEdges && !reader.Value.Any(i => i.Operand is MethodInfo && ((MethodInfo)i.Operand).ReturnType == field.DeclaringType && !Equals(i.Operand, edge.Key));
                            consumed |= usesEdges;
                        }
                    if (safe && consumed) return true;
                }
            }
            return false;
        }
        private static bool Linq(GimmickInstruction instruction, string name)
        {
            var method = instruction.Operand as MethodInfo;
            return method != null && method.DeclaringType == typeof(Enumerable) && method.Name == name && method.IsGenericMethod
                && method.GetGenericArguments().SequenceEqual(new[] { typeof(int) });
        }
        private static bool Key(GimmickInstruction instruction, object key)
        {
            if (key is string) return instruction.Code == OpCodes.Ldstr && Equals(instruction.Operand, key);
            if (!key.GetType().IsEnum && !(key is int)) return false;
            int value;
            if (instruction.Code == OpCodes.Ldc_I4 || instruction.Code == OpCodes.Ldc_I4_S) value = (int)instruction.Operand;
            else if (instruction.Code == OpCodes.Ldc_I4_M1) value = -1;
            else if (!instruction.Code.Name.StartsWith("ldc.i4.") || !int.TryParse(instruction.Code.Name.Substring(7), out value)) return false;
            return value == Convert.ToInt32(key);
        }
        private static bool ArithmeticConsumer(MethodInfo method, GimmickInstruction[] code, Dictionary<MethodInfo, GimmickInstruction[]> methods)
        {
            var args = method.GetParameters();
            if (!method.IsStatic || method.ReturnType != typeof(void) || args.Length != 1 || args[0].ParameterType != typeof(float).MakeByRefType()) return false;
            if (!code.Any(i => i.Code == OpCodes.Stind_R4)) return false;
            foreach (var i in code)
            {
                var op = i.Code;
                if (op == OpCodes.Ret || op == OpCodes.Dup || op == OpCodes.Ldarg_0 || op == OpCodes.Ldind_R4 || op == OpCodes.Stind_R4
                    || op == OpCodes.Ldc_R4 || op == OpCodes.Add || op == OpCodes.Sub || op == OpCodes.Mul || op == OpCodes.Div) continue;
                if ((op == OpCodes.Brfalse || op == OpCodes.Brfalse_S || op == OpCodes.Brtrue || op == OpCodes.Brtrue_S) && (int)i.Operand > i.Offset) continue;
                var read = i.Operand as MethodInfo; GimmickInstruction[] body;
                if (op == OpCodes.Call && read != null && read.IsStatic && read.ReturnType == typeof(bool) && read.GetParameters().Length == 0
                    && methods.TryGetValue(read, out body) && body.Length == 2 && body[0].Code == OpCodes.Ldsfld && body[1].Code == OpCodes.Ret) continue;
                return false;
            }
            return true;
        }
    }
}
