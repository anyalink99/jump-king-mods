using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JumpKing.API;
using JumpKing.Player;
using JKRuntime.Gameplay;
using JKRuntime.Inspection;

namespace JKRuntime.UI
{
    // prepare dependencies once, then read only covered backing fields. No
    // foreign predicate or gameplay callback gets an extra invocation
    internal static class BindingConversionContext
    {
        private static readonly FieldInfo Lookup = typeof(BodyComp).GetField("m_blockBehaviourLookup", OwnedPatches.Members);
        private sealed class Gate
        {
            internal string[] Actions;
            internal Type[] Blocks;
            internal string Refusal;
            internal MethodInfo Target;
            internal string Fingerprint;
            internal MethodValidityLease TargetValidity;
            internal readonly List<MethodValidityLease> Validity = new List<MethodValidityLease>();
        }
        private sealed class Flag { internal FieldInfo Field; internal MethodValidityLease Validity; }
        private static readonly List<Gate> gates = new List<Gate>();
        private static readonly Dictionary<Type, Flag> flags = new Dictionary<Type, Flag>();
        private static RuntimeScope scope;
        private static string failure;
        internal static string Reason(string id)
        {
            if (failure != null) return failure;
            var player = JumpKing.GameManager.GameLoop.m_player;
            if (player == null) return null;
            if (JumpSlot.ChargePolicySuspended || GameFeatures.IsMorphed || GameFeatures.Movement != MovementMode.Vanilla
                || !PlayerControl.Available(player.m_body, "jk-runtime.binding-modes"))
                return "Custom player controller: native mode retained";
            var blocks = Lookup.GetValue(player.m_body) as Dictionary<Type, IBlockBehaviour>;
            foreach (var gate in gates)
            {
                if (Array.IndexOf(gate.Actions, id) < 0) continue;
                if (gate.Refusal != null) return gate.Refusal;
                if (!gate.TargetValidity.IsValid) return "Input patch changed: " + gate.TargetValidity.Reason;
                foreach (var validity in gate.Validity) if (!validity.IsValid) return "Input helper changed: " + validity.Reason;
                foreach (var type in gate.Blocks)
                {
                    IBlockBehaviour handler;
                    if (blocks == null) return "Input context unavailable";
                    if (!blocks.TryGetValue(type, out handler)) continue;
                    Flag flag;
                    if (!flags.TryGetValue(handler.GetType(), out flag) || flag.Field == null || !flag.Validity.IsValid)
                        return "Uncovered mechanic predicate: native mode retained";
                    if ((bool)flag.Field.GetValue(handler)) return "Active input mechanic: native mode retained";
                }
            }
            return null;
        }
        internal static void Prepare()
        {
            Clear(); scope = new RuntimeScope();
            try
            {
                var engine = OwnedPatches.SharedEngine();
                var query = engine.GetType("HarmonyLib.Harmony").GetMethod("GetPatchInfo", new[] { typeof(MethodBase) });
                gates.Add(PrepareTarget(query, typeof(JumpState).GetMethod("MyRun", OwnedPatches.Members), new[] { "jump-king.jump" }));
                gates.Add(PrepareTarget(query, typeof(Walk).GetMethod("MyRun", OwnedPatches.Members), new[] { "jump-king.left", "jump-king.right" }));
                var skin = typeof(BodyComp).Assembly.GetType("JumpKing.Player.Skins.SkinManager");
                gates.Add(PrepareTarget(query, skin.GetMethod("SetSkinEnabled", OwnedPatches.Members), new[] { "jump-king.boots", "jump-king.snake" }));
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic) continue;
                    Type[] types;
                    try { types = assembly.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types; }
                    foreach (var type in types)
                    {
                        if (type == null || type.IsAbstract || type.ContainsGenericParameters || !typeof(IBlockBehaviour).IsAssignableFrom(type)) continue;
                        var map = type.GetInterfaceMap(typeof(IBlockBehaviour));
                        int index = Array.FindIndex(map.InterfaceMethods, m => m.Name == "get_IsPlayerOnBlock");
                        var getter = index < 0 ? null : map.TargetMethods[index];
                        var flag = new Flag(); flags[type] = flag;
                        if (getter == null || getter.GetMethodBody() == null) continue;
                        var code = GimmickIl.Read(getter).Where(i => i.Code != OpCodes.Nop).ToArray();
                        if (code.Length == 3 && code[0].Code == OpCodes.Ldarg_0 && code[1].Code == OpCodes.Ldfld
                            && code[2].Code == OpCodes.Ret && ((FieldInfo)code[1].Operand).FieldType == typeof(bool)) {
                            flag.Field = (FieldInfo)code[1].Operand; flag.Validity = scope.Own(MethodValidity.Watch(getter));
                        }
                    }
                }
            }
            catch (Exception e) { failure = "Input analysis unavailable"; Console.WriteLine("[JK Runtime UI] Binding context: " + e.GetBaseException().Message); }
        }
        private static Gate PrepareTarget(MethodInfo query, MethodInfo target, string[] actions)
        {
            var gate = new Gate { Actions = actions, Blocks = new Type[0], Target = target };
            var info = query.Invoke(null, new object[] { target });
            var owners = new List<string>(); var blocks = new HashSet<Type>();
            bool rewriteChecked = false;
            if (info != null) foreach (string kind in new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers" })
                foreach (var patch in (IEnumerable)info.GetType().GetField(kind).GetValue(info)) {
                    var type = patch.GetType(); var method = (MethodInfo)type.GetProperty("PatchMethod").GetValue(patch, null);
                    owners.Add((string)type.GetField("owner").GetValue(patch));
                    if (method.DeclaringType.Assembly == typeof(BindingConversionContext).Assembly) continue;
                    if (kind == "Transpilers") {
                        if (rewriteChecked) continue; rewriteChecked = true;
                        MethodInfo[] helpers;
                        if (!BindingComparisonRewrite.TryCover(query.DeclaringType.Assembly, target, out helpers))
                            gate.Refusal = "Uncovered input rewrite: native mode retained";
                        else foreach (var helper in helpers) gate.Validity.Add(scope.Own(MethodValidity.Watch(helper)));
                        continue;
                    }
                    // postfixes retain authority over the native result. A prefix can
                    // replace the input's meaning before native control ever runs
                    if (kind != "Prefixes") continue;
                    Type[] dependencies;
                    if (!BindingContextIl.TryGuard(method, out dependencies)) gate.Refusal = "Uncovered input controller: native mode retained";
                    else foreach (var block in dependencies) blocks.Add(block);
                    gate.Validity.Add(scope.Own(MethodValidity.Watch(method)));
                    foreach (var call in GimmickIl.Read(method).Select(i => i.Operand as MethodInfo).Where(m => m != null && !m.ContainsGenericParameters
                        && m.DeclaringType.Assembly == method.DeclaringType.Assembly).Distinct()) gate.Validity.Add(scope.Own(MethodValidity.Watch(call)));
                }
            gate.Blocks = blocks.ToArray();
            gate.TargetValidity = scope.Own(MethodValidity.Watch(target, owners.ToArray()));
            gate.Fingerprint = ForeignGraph(info);
            return gate;
        }
        private static string ForeignGraph(object info)
        {
            if (info == null) return "";
            var parts = new List<string>();
            foreach (string kind in new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers" })
                foreach (var patch in (IEnumerable)info.GetType().GetField(kind).GetValue(info)) {
                    var type = patch.GetType(); var method = (MethodInfo)type.GetProperty("PatchMethod").GetValue(patch, null);
                    if (method.DeclaringType.Assembly == typeof(BindingConversionContext).Assembly) continue;
                    parts.Add(kind + ":" + method.Module.ModuleVersionId + ":" + method.MetadataToken + ":" + type.GetField("priority").GetValue(patch)
                        + ":" + string.Join(",", (string[])type.GetField("before").GetValue(patch) ?? new string[0]) + ":" + string.Join(",", (string[])type.GetField("after").GetValue(patch) ?? new string[0]));
                }
            return string.Join("|", parts);
        }
        internal static void FinalizeOwnHooks()
        {
            if (scope == null) return;
            var query = OwnedPatches.SharedEngine().GetType("HarmonyLib.Harmony").GetMethod("GetPatchInfo", new[] { typeof(MethodBase) });
            for (int index = 0; index < gates.Count; index++) {
                var gate = gates[index];
                var info = query.Invoke(null, new object[] { gate.Target });
                if (ForeignGraph(info) != gate.Fingerprint) {
                    // native OnLevelStart callbacks can install patches after our
                    // preparation. certify that final graph once at the barrier
                    gates[index] = PrepareTarget(query, gate.Target, gate.Actions);
                    gate.TargetValidity.Dispose(); foreach (var lease in gate.Validity) lease.Dispose();
                    continue;
                }
                var owners = new List<string>();
                if (info != null) foreach (string kind in new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers" })
                    foreach (var patch in (IEnumerable)info.GetType().GetField(kind).GetValue(info)) owners.Add((string)patch.GetType().GetField("owner").GetValue(patch));
                gate.TargetValidity.Dispose(); gate.TargetValidity = scope.Own(MethodValidity.Watch(gate.Target, owners.ToArray()));
            }
            WriteDiagnosticReport();
        }
        internal static void WriteDiagnosticReport()
        {
            string path = Environment.GetEnvironmentVariable("JKRUNTIME_BINDING_REPORT");
            if (string.IsNullOrEmpty(path)) return;
            try {
                var lines = new List<string> { DateTime.UtcNow.ToString("o"), "Binding modes " + RuntimeApi.Version };
                foreach (var gate in gates) {
                    lines.Add(string.Join(", ", gate.Actions) + ": " + (Reason(gate.Actions[0]) ?? "mode conversion available"));
                    lines.Add("  prepared: " + (gate.Refusal ?? "covered") + "; target: " + (gate.TargetValidity.Reason ?? "valid"));
                    foreach (var type in gate.Blocks) lines.Add("  predicate: " + type.FullName);
                }
                System.IO.File.WriteAllLines(path, lines);
            } catch (Exception error) { Console.WriteLine("[JK Runtime UI] Cannot export binding report: " + error.Message); }
        }
        internal static void Clear()
        {
            if (scope != null) scope.Dispose(); scope = null;
            gates.Clear(); flags.Clear(); failure = null;
        }
    }
}
