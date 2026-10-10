using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JKRuntime.Inspection;

namespace JKRuntime.UI
{
    // accept comparison-only rewrites without treating an installed gravity mod
    // as an input controller. all input reads and branch destinations stay put
    internal static class BindingComparisonRewrite
    {
        private sealed class Code
        {
            internal OpCode Op;
            internal object Operand;
            internal Label[] Labels;
        }
        internal static bool TryCover(Assembly engine, MethodInfo target, out MethodInfo[] helpers)
        {
            helpers = new MethodInfo[0];
            try {
                var processor = engine.GetType("HarmonyLib.PatchProcessor", true);
                var original = processor.GetMethod("GetOriginalInstructions", new[] { typeof(MethodBase), typeof(ILGenerator) });
                var current = processor.GetMethod("GetCurrentInstructions", new[] { typeof(MethodBase), typeof(int), typeof(ILGenerator) });
                // this compiles the transpiler chain during preparation; no emitted
                // gameplay method or predicate is invoked by the verifier
                return Compare(Read(original.Invoke(null, new object[] { target, null })),
                    Read(current.Invoke(null, new object[] { target, int.MaxValue, null })), out helpers);
            } catch { return false; }
        }
        private static Code[] Read(object instructions)
        {
            return ((IEnumerable)instructions).Cast<object>().Select(i => {
                var t = i.GetType();
                if (((IEnumerable)t.GetField("blocks").GetValue(i)).Cast<object>().Any()) throw new NotSupportedException();
                return new Code { Op = (OpCode)t.GetField("opcode").GetValue(i), Operand = t.GetField("operand").GetValue(i),
                    Labels = ((IEnumerable)t.GetField("labels").GetValue(i)).Cast<Label>().ToArray() };
            }).ToArray();
        }
        private static bool Compare(Code[] before, Code[] after, out MethodInfo[] helpers)
        {
            helpers = new MethodInfo[0];
            var checkedMethods = new HashSet<MethodInfo>();
            var map = new Dictionary<int, int>(); var branches = new List<int[]>();
            int i = 0, j = 0;
            while (i < before.Length && j < after.Length) {
                map.Add(i, j);
                var a = before[i]; var b = after[j];
                if (a.Op == b.Op && a.Operand is Label && b.Operand is Label) branches.Add(new[] { i, j });
                else if (a.Op == b.Op && Same(a.Operand, b.Operand)) { }
                else {
                    var helper = b.Operand as MethodInfo;
                    if (!Comparison(a.Op) || b.Op != OpCodes.Call || helper == null || !helper.IsStatic
                        || helper.ReturnType != typeof(bool) || !helper.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(float), typeof(float) })
                        || j + 1 >= after.Length || (after[j + 1].Op != OpCodes.Brtrue && after[j + 1].Op != OpCodes.Brtrue_S)
                        || after[j + 1].Labels.Length != 0 || !Pure(helper, checkedMethods, new HashSet<MethodInfo>())) return false;
                    branches.Add(new[] { i, ++j });
                }
                i++; j++;
            }
            if (i != before.Length || j != after.Length) return false;
            foreach (var branch in branches) {
                int from = Destination(before, before[branch[0]].Operand), to = Destination(after, after[branch[1]].Operand);
                int mapped; if (from < 0 || !map.TryGetValue(from, out mapped) || mapped != to) return false;
            }
            helpers = checkedMethods.ToArray(); return true;
        }
        private static bool Same(object a, object b)
        {
            var x = a as LocalVariableInfo; var y = b as LocalVariableInfo;
            return x != null && y != null ? x.LocalIndex == y.LocalIndex && x.LocalType == y.LocalType : Equals(a, b);
        }
        private static int Destination(Code[] code, object label)
        { return label is Label ? Array.FindIndex(code, i => i.Labels.Contains((Label)label)) : -1; }
        private static bool Comparison(OpCode op)
        { return new[] { OpCodes.Bge, OpCodes.Bge_S, OpCodes.Bge_Un, OpCodes.Bge_Un_S, OpCodes.Bgt, OpCodes.Bgt_S, OpCodes.Bgt_Un, OpCodes.Bgt_Un_S,
            OpCodes.Ble, OpCodes.Ble_S, OpCodes.Ble_Un, OpCodes.Ble_Un_S, OpCodes.Blt, OpCodes.Blt_S, OpCodes.Blt_Un, OpCodes.Blt_Un_S }.Contains(op); }
        private static bool Scalar(Type type)
        { return type == typeof(bool) || type == typeof(float) || type == typeof(int) || type == typeof(string); }
        internal static bool Pure(MethodInfo method, HashSet<MethodInfo> complete, HashSet<MethodInfo> active)
        {
            if (complete.Contains(method)) return true;
            if (active.Count >= 8 || !active.Add(method) || !method.IsStatic || !Scalar(method.ReturnType)
                || method.GetParameters().Any(p => !Scalar(p.ParameterType))) return false;
            var body = method.GetMethodBody(); if (body == null || body.ExceptionHandlingClauses.Count != 0) return false;
            var code = GimmickIl.Read(method); if (code.Length > 256) return false;
            foreach (var i in code) {
                var op = i.Code; string name = op.Name;
                if (op == OpCodes.Call) {
                    var call = i.Operand as MethodInfo;
                    if (call == null) return false;
                    if (call.DeclaringType == typeof(string) && call.Name == "op_Equality") continue;
                    if (!Pure(call, complete, active)) return false;
                }
                else if (op == OpCodes.Ldsfld) { if (!Scalar(((FieldInfo)i.Operand).FieldType)) return false; }
                else if (op == OpCodes.Newobj) {
                    var ctor = (ConstructorInfo)i.Operand;
                    if (ctor.DeclaringType != typeof(Exception) || !ctor.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string) })) return false;
                }
                else if (op == OpCodes.Throw || op == OpCodes.Ret || op == OpCodes.Nop || op == OpCodes.Dup || op == OpCodes.Pop
                    || name.StartsWith("ldarg") && !name.StartsWith("ldarga") || name.StartsWith("ldloc") && !name.StartsWith("ldloca")
                    || name.StartsWith("stloc") || name.StartsWith("starg") || name.StartsWith("ldc.") || op == OpCodes.Ldstr
                    || op == OpCodes.Ceq || op == OpCodes.Cgt || op == OpCodes.Cgt_Un || op == OpCodes.Clt || op == OpCodes.Clt_Un
                    || op == OpCodes.Add || op == OpCodes.Sub || op == OpCodes.Mul || op == OpCodes.Div || op == OpCodes.Neg) { }
                else if (op.FlowControl == FlowControl.Branch || op.FlowControl == FlowControl.Cond_Branch) {
                    if (!(i.Operand is int) || (int)i.Operand <= i.Offset) return false;
                }
                else return false;
            }
            active.Remove(method); complete.Add(method); return true;
        }
    }
}
