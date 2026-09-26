using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace JKRuntime.Gameplay
{
    // A bounded forward data-flow verifier and observer emitter, not an IL
    // interpreter. Original instructions/calls execute once, in their order.
    internal static class MotionIl
    {
        private struct Value
        {
            internal bool Float, Depends;
            internal Value(bool number, bool depends) { Float=number; Depends=depends; }
        }
        private sealed class State
        {
            internal List<Value> Stack = new List<Value>();
            internal bool[] Locals;
            internal State Copy() { return new State { Stack=new List<Value>(Stack), Locals=(bool[])Locals.Clone() }; }
            internal Value Pop() { if(Stack.Count==0) throw Refuse("stack underflow"); var v=Stack[Stack.Count-1]; Stack.RemoveAt(Stack.Count-1); return v; }
        }
        private sealed class Code
        {
            internal object Raw, Operand;
            internal OpCode Op;
        }
        private static readonly Dictionary<Assembly,MethodInfo> bridges=new Dictionary<Assembly,MethodInfo>();
        internal static MethodInfo Bridge(Assembly engine)
        {
            MethodInfo found; if(bridges.TryGetValue(engine,out found)) return found;
            var instruction=engine.GetType("HarmonyLib.CodeInstruction",true);
            var enumerable=typeof(IEnumerable<>).MakeGenericType(instruction);
            var module=AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("JKRuntime.MotionBridge."+bridges.Count),AssemblyBuilderAccess.Run).DefineDynamicModule("Bridge");
            var type=module.DefineType("MotionTranspiler",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed);
            var callback=typeof(Func<IEnumerable,ILGenerator,MethodBase,IEnumerable>);
            var field=type.DefineField("Callback",callback,FieldAttributes.Public|FieldAttributes.Static);
            var method=type.DefineMethod("Rewrite",MethodAttributes.Public|MethodAttributes.Static,enumerable,new[]{enumerable,typeof(ILGenerator),typeof(MethodBase)});
            method.DefineParameter(1,ParameterAttributes.None,"instructions"); method.DefineParameter(2,ParameterAttributes.None,"generator"); method.DefineParameter(3,ParameterAttributes.None,"__originalMethod");
            var il=method.GetILGenerator(); il.Emit(OpCodes.Ldsfld,field); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Callvirt,callback.GetMethod("Invoke")); il.Emit(OpCodes.Castclass,enumerable); il.Emit(OpCodes.Ret);
            var created=type.CreateType(); created.GetField("Callback").SetValue(null,new Func<IEnumerable,ILGenerator,MethodBase,IEnumerable>(Rewrite));
            found=created.GetMethod("Rewrite"); bridges.Add(engine,found); return found;
        }
        private static NotSupportedException Refuse(string reason) { return new NotSupportedException("Motion arithmetic: "+reason); }
        private static int Index(Code c)
        {
            string n=c.Op.Name; int dot=n.LastIndexOf('.'); int small;
            if(dot>=0 && int.TryParse(n.Substring(dot+1),out small)) return small;
            var local=c.Operand as LocalVariableInfo; if(local!=null) return local.LocalIndex;
            var parameter=c.Operand as ParameterInfo; if(parameter!=null) return parameter.Position+1;
            return Convert.ToInt32(c.Operand);
        }
        private static Value Typed(Type type, bool dependent) { return new Value(type==typeof(float),dependent); }
        private static void Independent(Value value) { if(value.Depends) throw Refuse("speed escapes arithmetic or selects control flow"); }
        private static bool Arithmetic(OpCode op) { return op==OpCodes.Add || op==OpCodes.Sub || op==OpCodes.Mul || op==OpCodes.Div; }
        private static State Step(Code c,State input,Type[] locals)
        {
            var s=input.Copy(); string n=c.Op.Name;
            if(n=="nop" || n=="br" || n=="br.s") return s;
            if(n.StartsWith("ldarg") && !n.StartsWith("ldarga")) { int i=Index(c); s.Stack.Add(new Value(i==1,i==1)); return s; }
            if(n.StartsWith("ldloc") && !n.StartsWith("ldloca")) { int i=Index(c); s.Stack.Add(Typed(locals[i],s.Locals[i])); return s; }
            if(n.StartsWith("stloc")) { var v=s.Pop(); int i=Index(c); if(v.Depends && locals[i]!=typeof(float)) throw Refuse("non-float speed local"); s.Locals[i]=v.Depends; return s; }
            if(n=="ldc.r4") { s.Stack.Add(new Value(true,false)); return s; }
            if(n.StartsWith("ldc.i") || n=="ldc.r8" || n=="ldnull" || n=="ldstr" || n=="ldtoken") { s.Stack.Add(new Value(false,false)); return s; }
            if(n=="dup") { var v=s.Pop(); s.Stack.Add(v); s.Stack.Add(v); return s; }
            if(n=="pop") { s.Pop(); return s; }
            if(n=="ldfld" || n=="ldsfld") { if(n=="ldfld") Independent(s.Pop()); s.Stack.Add(Typed(((FieldInfo)c.Operand).FieldType,false)); return s; }
            if(n=="stfld" || n=="stsfld") { Independent(s.Pop()); if(n=="stfld") Independent(s.Pop()); return s; }
            if(n=="call" || n=="callvirt" || n=="newobj")
            {
                var target=(MethodBase)c.Operand;
                if(target.GetParameters().Any(p=>p.ParameterType.IsByRef)) throw Refuse("by-ref call");
                foreach(var p in target.GetParameters()) Independent(s.Pop());
                if(!target.IsStatic && n!="newobj") Independent(s.Pop());
                Type result=n=="newobj"?target.DeclaringType:((MethodInfo)target).ReturnType;
                if(result!=typeof(void)) s.Stack.Add(Typed(result,false)); return s;
            }
            if(Arithmetic(c.Op))
            {
                var right=s.Pop(); var left=s.Pop(); bool dep=left.Depends||right.Depends;
                if(dep && (!left.Float || !right.Float)) throw Refuse("non-float arithmetic");
                if(c.Op==OpCodes.Div && right.Depends) throw Refuse("speed-dependent divisor");
                if(c.Op==OpCodes.Mul && left.Depends && right.Depends) throw Refuse("nonlinear speed product");
                s.Stack.Add(new Value(left.Float&&right.Float,dep)); return s;
            }
            if(n=="neg") { var v=s.Pop(); s.Stack.Add(v); return s; }
            if(n.StartsWith("conv.")) { var v=s.Pop(); if(v.Depends && c.Op!=OpCodes.Conv_R4) throw Refuse("speed conversion"); s.Stack.Add(new Value(c.Op==OpCodes.Conv_R4,v.Depends)); return s; }
            if(n=="castclass" || n=="isinst" || n=="box" || n=="unbox.any") { Independent(s.Pop()); s.Stack.Add(Typed((Type)c.Operand,false)); return s; }
            if(n=="ceq" || n=="cgt" || n=="cgt.un" || n=="clt" || n=="clt.un") { Independent(s.Pop()); Independent(s.Pop()); s.Stack.Add(new Value(false,false)); return s; }
            if(c.Op.FlowControl==FlowControl.Cond_Branch)
            {
                Independent(s.Pop()); if(c.Op.StackBehaviourPop==StackBehaviour.Pop1_pop1) Independent(s.Pop()); return s;
            }
            if(n=="ret") { var v=s.Pop(); if(!v.Float || s.Stack.Count!=0) throw Refuse("non-float return"); return s; }
            if(n=="throw") { Independent(s.Pop()); return s; }
            throw Refuse("unsupported opcode "+n);
        }
        private static IEnumerable Rewrite(IEnumerable source,ILGenerator generator,MethodBase method)
        {
            var raw=source.Cast<object>().ToArray(); if(raw.Length==0) return source;
            var type=raw[0].GetType(); var opcode=type.GetField("opcode"); var operand=type.GetField("operand"); var labels=type.GetField("labels"); var blocks=type.GetField("blocks");
            var code=raw.Select(r=>new Code{Raw=r,Op=(OpCode)opcode.GetValue(r),Operand=operand.GetValue(r)}).ToArray();
            var plan=MotionObservation.FindPlan(method);
            if(plan==null || plan.Stale) return source;
            try
            {
                var body=method.GetMethodBody();
                if(body==null || body.ExceptionHandlingClauses.Count!=0 || code.Length>512 || body.LocalVariables.Count>64) throw Refuse("body exceeds supported limits");
                if(code.Any(c=>((IList)blocks.GetValue(c.Raw)).Count!=0)) throw Refuse("exception regions");
                var localTypes=body.LocalVariables.Select(v=>v.LocalType).ToArray();
                var targets=new Dictionary<Label,int>();
                for(int i=0;i<code.Length;i++) foreach(Label l in (IList)labels.GetValue(code[i].Raw)) targets[l]=i;
                var states=new State[code.Length]; states[0]=new State{Locals=new bool[localTypes.Length]}; int maximum=0;
                for(int i=0;i<code.Length;i++)
                {
                    if(states[i]==null) continue;
                    var next=Step(code[i],states[i],localTypes); maximum=Math.Max(maximum,Math.Max(next.Stack.Count,states[i].Stack.Count));
                    if(maximum>64) throw Refuse("stack limit");
                    var destinations=new List<int>(); var flow=code[i].Op.FlowControl;
                    if(flow==FlowControl.Branch || flow==FlowControl.Cond_Branch)
                    {
                        if(code[i].Operand is Label[]) destinations.AddRange(((Label[])code[i].Operand).Select(l=>targets[l]));
                        else destinations.Add(targets[(Label)code[i].Operand]);
                    }
                    if(flow!=FlowControl.Branch && flow!=FlowControl.Return && flow!=FlowControl.Throw && i+1<code.Length) destinations.Add(i+1);
                    foreach(int d in destinations)
                    {
                        if(d<=i) throw Refuse("backward branch");
                        if(states[d]==null) { states[d]=next.Copy(); continue; }
                        var merge=states[d]; if(merge.Stack.Count!=next.Stack.Count) throw Refuse("inconsistent stack join");
                        for(int j=0;j<merge.Stack.Count;j++) {
                            var a=merge.Stack[j]; var b=next.Stack[j]; if(a.Float!=b.Float) throw Refuse("inconsistent numeric join");
                            merge.Stack[j]=new Value(a.Float,a.Depends||b.Depends);
                        }
                        for(int j=0;j<merge.Locals.Length;j++) merge.Locals[j]|=next.Locals[j];
                    }
                }
                var stackSlots=Enumerable.Range(0,maximum+1).Select(i=>generator.DeclareLocal(typeof(float))).ToArray();
                var localSlots=localTypes.Select(t=>t==typeof(float)?generator.DeclareLocal(typeof(float)):null).ToArray();
                var output=new List<object>();
                Action<OpCode,object> emit=(op,arg)=>output.Add(Activator.CreateInstance(type,new object[]{op,arg}));
                Action<int> load=i=>emit(OpCodes.Ldloc,stackSlots[i]); Action<int> save=i=>emit(OpCodes.Stloc,stackSlots[i]);
                for(int i=0;i<code.Length;i++)
                {
                    var c=code[i]; var s=states[i]; if(s==null) { output.Add(c.Raw); continue; }
                    int depth=s.Stack.Count; string n=c.Op.Name; var next=Step(c,s,localTypes);
                    if(n=="ret")
                    {
                        int start=output.Count; emit(OpCodes.Dup,null); load(depth-1); emit(OpCodes.Ldc_I4,plan.Id);
                        emit(OpCodes.Call,typeof(MotionObservation).GetMethod("Record",BindingFlags.NonPublic|BindingFlags.Static));
                        var destination=(IList)labels.GetValue(output[start]); foreach(var l in (IList)labels.GetValue(c.Raw)) destination.Add(l);
                        ((IList)labels.GetValue(c.Raw)).Clear(); output.Add(c.Raw); continue;
                    }
                    output.Add(c.Raw);
                    if(n.StartsWith("stloc")) { int local=Index(c); if(localSlots[local]!=null) { load(depth-1); emit(OpCodes.Stloc,localSlots[local]); } continue; }
                    if(n=="dup") { if(s.Stack[depth-1].Float) { load(depth-1); save(depth); } continue; }
                    if(Arithmetic(c.Op) && next.Stack[next.Stack.Count-1].Float)
                    { load(depth-2); load(depth-1); emit(c.Op,null); save(depth-2); continue; }
                    if(n=="neg" && s.Stack[depth-1].Float) { load(depth-1); emit(OpCodes.Neg,null); save(depth-1); continue; }
                    if(n.StartsWith("ldloc") && !n.StartsWith("ldloca") && localSlots[Index(c)]!=null)
                    { emit(OpCodes.Ldloc,localSlots[Index(c)]); save(depth); continue; }
                    if(n.StartsWith("ldarg") && Index(c)==1)
                    { emit(OpCodes.Call,typeof(MotionObservation).GetMethod("NeutralArgument",BindingFlags.NonPublic|BindingFlags.Static)); save(depth); continue; }
                    if(n=="conv.r4" && s.Stack[depth-1].Depends) continue;
                    // A number produced by an independent load/call is copied
                    // from the real evaluation stack. Never call its getter twice.
                    if(next.Stack.Count>0 && next.Stack[next.Stack.Count-1].Float && Produces(c.Op))
                    { emit(OpCodes.Dup,null); save(next.Stack.Count-1); }
                }
                plan.Reason=null;
                var array=Array.CreateInstance(type,output.Count); for(int i=0;i<output.Count;i++) array.SetValue(output[i],i); return array;
            }
            catch(NotSupportedException e) { plan.Reason=e.Message; return source; }
        }
        private static bool Produces(OpCode op)
        { return op.StackBehaviourPush!=StackBehaviour.Push0 || op.FlowControl==FlowControl.Call; }
    }
}
