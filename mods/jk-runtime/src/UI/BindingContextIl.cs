using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JumpKing.Player;
using JKRuntime.Inspection;

namespace JKRuntime.UI
{
    internal static class BindingContextIl
    {
        internal static string LastRefusal;
        private sealed class State
        {
            internal int At;
            internal List<int?> Stack = new List<int?>();
            internal Dictionary<int, int?> Locals = new Dictionary<int, int?>();
            internal State Copy(int at) { return new State { At = at, Stack = new List<int?>(Stack), Locals = new Dictionary<int, int?>(Locals) }; }
            internal int? Pop() { if (Stack.Count == 0) throw new InvalidOperationException(); var v = Stack[Stack.Count - 1]; Stack.RemoveAt(Stack.Count - 1); return v; }
        }
        // prove every covered path falls through when its block predicates are
        // false. Active paths are never interpreted or replayed
        internal static bool TryGuard(MethodInfo method, out Type[] blocks)
        {
            blocks = new Type[0];
            LastRefusal = "No covered input path";
            try
            {
                var body = method.GetMethodBody();
                if (body == null) return false;
                var code = GimmickIl.Read(method); if (code.Length > 512) return false;
                var dependencies = new HashSet<Type>();
                var work = new Queue<State>(); work.Enqueue(new State()); int budget = 4096, returns = 0;
                while (work.Count != 0)
                {
                    var s = work.Dequeue();
                    while (s.At < code.Length)
                    {
                        if (--budget < 0 || s.Stack.Count > 64 || work.Count > 128) return false;
                        var i = code[s.At++]; var op = i.Code;
                        LastRefusal = "IL_" + i.Offset.ToString("x4") + " " + op.Name + " " + i.Operand;
                        // inactive branches may skip a foreach/finally completely
                        foreach (var clause in body.ExceptionHandlingClauses)
                            if (i.Offset >= clause.TryOffset && i.Offset < clause.TryOffset + clause.TryLength
                                || i.Offset >= clause.HandlerOffset && i.Offset < clause.HandlerOffset + clause.HandlerLength) return false;
                        if (op == OpCodes.Nop) continue;
                        if (op == OpCodes.Ret) {
                            if (method.ReturnType == typeof(bool) && s.Pop() != 1) return false;
                            if (method.ReturnType != typeof(bool) && method.ReturnType != typeof(void)) return false;
                            returns++; break;
                        }
                        if (op == OpCodes.Ldc_I4_0) { s.Stack.Add(0); continue; }
                        if (op == OpCodes.Ldc_I4_1) { s.Stack.Add(1); continue; }
                        if (op == OpCodes.Ldnull || op.Name.StartsWith("ldarg") || op == OpCodes.Ldsfld || op.Name.StartsWith("ldc.")) { s.Stack.Add(null); continue; }
                        if (op == OpCodes.Ldfld) { s.Pop(); s.Stack.Add(null); continue; }
                        if (op == OpCodes.Dup) { var v = s.Pop(); s.Stack.Add(v); s.Stack.Add(v); continue; }
                        if (op == OpCodes.Pop) { s.Pop(); continue; }
                        if (op.Name.StartsWith("ldloc")) { int? v; s.Locals.TryGetValue(Local(i), out v); s.Stack.Add(v); continue; }
                        if (op.Name.StartsWith("stloc")) { s.Locals[Local(i)] = s.Pop(); continue; }
                        if (op == OpCodes.Call || op == OpCodes.Callvirt)
                        {
                            var call = i.Operand as MethodInfo; if (call == null) return false;
                            foreach (var p in call.GetParameters()) s.Pop(); if (!call.IsStatic) s.Pop();
                            if (IsBlockQuery(call)) { dependencies.Add(call.GetGenericArguments()[0]); s.Stack.Add(0); continue; }
                            if (!Trivial(call, method.DeclaringType.Assembly)) return false;
                            if (call.ReturnType != typeof(void)) s.Stack.Add(null);
                            continue;
                        }
                        if (op.FlowControl == FlowControl.Branch || op.FlowControl == FlowControl.Cond_Branch)
                        {
                            if (!(i.Operand is int) || (int)i.Operand <= i.Offset) return false;
                            int target = Array.FindIndex(code, v => v.Offset == (int)i.Operand); if (target < 0) return false;
                            if (op.FlowControl == FlowControl.Branch) { s.At = target; continue; }
                            if (op != OpCodes.Brtrue && op != OpCodes.Brtrue_S && op != OpCodes.Brfalse && op != OpCodes.Brfalse_S) return false;
                            int? value = s.Pop(); bool truth = op == OpCodes.Brtrue || op == OpCodes.Brtrue_S;
                            if (!value.HasValue) work.Enqueue(s.Copy(target));
                            else if ((value.Value != 0) == truth) s.At = target;
                            continue;
                        }
                        return false;
                    }
                }
                blocks = dependencies.ToArray(); bool covered = returns > 0 && blocks.Length > 0;
                if (covered) LastRefusal = null; return covered;
            }
            catch (Exception e) { LastRefusal += ": " + e.Message; return false; }
        }
        private static int Local(GimmickInstruction i)
        { int index; return int.TryParse(i.Code.Name.Substring(6), out index) ? index : Convert.ToInt32(i.Operand); }
        private static bool IsBlockQuery(MethodInfo m)
        { return m != null && m.DeclaringType == typeof(BodyComp) && m.Name == "IsOnBlock" && m.IsGenericMethod && m.GetGenericArguments().Length == 1; }
        private static bool Trivial(MethodInfo method, Assembly owner)
        {
            if (method.GetMethodBody() == null) return false;
            var code = GimmickIl.Read(method).Where(i => i.Code != OpCodes.Nop).ToArray();
            if (code.Length == 2 && code[0].Code == OpCodes.Ldsfld && code[1].Code == OpCodes.Ret) return true;
            if (code.Length != 3 || code[0].Code != OpCodes.Ldarg_0 || code[2].Code != OpCodes.Ret) return false;
            if (code[1].Code == OpCodes.Ldfld) return true;
            var field = code[1].Operand as FieldInfo;
            // private numeric bookkeeping is permitted only on the inactive path
            return code[1].Code == OpCodes.Stsfld && field != null && field.IsPrivate && field.DeclaringType.Assembly == owner
                && (field.FieldType == typeof(float) || field.FieldType == typeof(int));
        }
    }
}
