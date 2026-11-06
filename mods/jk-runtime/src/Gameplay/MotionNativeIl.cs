using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JumpKing.API;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime.Gameplay
{
    // witnesses live inside the executed body, so skipped bodies and alternate paths can't borrow an old sample
    internal static class MotionNativeIl
    {
        private static readonly Dictionary<Assembly,MethodInfo> bridges=new Dictionary<Assembly,MethodInfo>();
        private static readonly Dictionary<MethodInfo,MethodInfo> sums=new Dictionary<MethodInfo,MethodInfo>();
        private static ModuleBuilder module;
        private static int next;
        private static ModuleBuilder Module {
            get { return module ?? (module=AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("JKRuntime.MotionNativeBridge"),AssemblyBuilderAccess.Run).DefineDynamicModule("Bridge")); }
        }
        private static MethodInfo Hook(string name) { return typeof(MotionObservation).GetMethod(name,OwnedPatches.Members); }
        internal static MethodInfo Bridge(Assembly engine)
        {
            MethodInfo found;if(bridges.TryGetValue(engine,out found))return found;
            var instruction=engine.GetType("HarmonyLib.CodeInstruction",true);
            var enumerable=typeof(IEnumerable<>).MakeGenericType(instruction);
            var type=Module.DefineType("Native"+(++next),TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed);
            var callback=typeof(Func<IEnumerable,IEnumerable>);
            var field=type.DefineField("Callback",callback,FieldAttributes.Public|FieldAttributes.Static);
            var method=type.DefineMethod("Rewrite",MethodAttributes.Public|MethodAttributes.Static,enumerable,new[]{enumerable});
            method.DefineParameter(1,ParameterAttributes.None,"instructions");
            var il=method.GetILGenerator();il.Emit(OpCodes.Ldsfld,field);il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt,callback.GetMethod("Invoke"));il.Emit(OpCodes.Castclass,enumerable);il.Emit(OpCodes.Ret);
            var created=type.CreateType();created.GetField("Callback").SetValue(null,new Func<IEnumerable,IEnumerable>(Rewrite));
            found=created.GetMethod("Rewrite");bridges.Add(engine,found);return found;
        }
        private static MethodInfo SumBridge(MethodInfo target)
        {
            MotionObservation.DiscoverSum(target);
            MethodInfo found;if(sums.TryGetValue(target,out found))return found;
            // the delegate calls the actual helper once, including its installed patches
            var original=(Func<float,float,float>)Delegate.CreateDelegate(typeof(Func<float,float,float>),target);
            Func<float,float,float> observe=(position,step)=>MotionObservation.Sum(target,original,position,step);
            var type=Module.DefineType("Sum"+(++next),TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed);
            var field=type.DefineField("Callback",typeof(Func<float,float,float>),FieldAttributes.Public|FieldAttributes.Static);
            var method=type.DefineMethod("Invoke",MethodAttributes.Public|MethodAttributes.Static,typeof(float),new[]{typeof(float),typeof(float)});
            var il=method.GetILGenerator();il.Emit(OpCodes.Ldsfld,field);il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt,typeof(Func<float,float,float>).GetMethod("Invoke"));il.Emit(OpCodes.Ret);
            var created=type.CreateType();created.GetField("Callback").SetValue(null,observe);
            found=created.GetMethod("Invoke");sums.Add(target,found);return found;
        }
        private static IEnumerable Rewrite(IEnumerable source)
        {
            var raw=source.Cast<object>().ToArray();if(raw.Length==0)return source;
            var type=raw[0].GetType();var opcode=type.GetField("opcode");var operand=type.GetField("operand");var labels=type.GetField("labels");
            var output=new List<object>();
            Action<OpCode,object> emit=(op,arg)=>output.Add(Activator.CreateInstance(type,new object[]{op,arg}));
            emit(OpCodes.Ldarg_1,null);emit(OpCodes.Call,Hook("EnterBody"));
            var modifier=typeof(IBlockBehaviour).GetMethod("ModifyXVelocity");
            var x=typeof(Vector2).GetField("X");
            var velocity=typeof(BodyComp).GetField("Velocity");
            for(int i=0;i<raw.Length;i++) {
                var instruction=raw[i];var op=(OpCode)opcode.GetValue(instruction);var arg=operand.GetValue(instruction);
                if(op==OpCodes.Ldflda && Equals(arg,velocity) && i+1<raw.Length
                    && (OpCode)opcode.GetValue(raw[i+1])==OpCodes.Ldfld && Equals(operand.GetValue(raw[i+1]),x)) {
                    opcode.SetValue(instruction,OpCodes.Call);operand.SetValue(instruction,Hook("ReadVelocity"));
                    opcode.SetValue(raw[i+1],OpCodes.Nop);operand.SetValue(raw[i+1],null);
                }
                if((op==OpCodes.Call || op==OpCodes.Callvirt) && Equals(arg,modifier)) {
                    opcode.SetValue(instruction,OpCodes.Call);operand.SetValue(instruction,Hook("Invoke"));
                }
                int following=i+1;while(following<raw.Length && (OpCode)opcode.GetValue(raw[following])==OpCodes.Nop)following++;
                bool store=following<raw.Length && ((OpCode)opcode.GetValue(raw[following])==OpCodes.Stind_R4
                    || ((OpCode)opcode.GetValue(raw[following])==OpCodes.Stfld && Equals(operand.GetValue(raw[following]),x)));
                if(store && op==OpCodes.Add) {
                    opcode.SetValue(instruction,OpCodes.Call);operand.SetValue(instruction,Hook("AddPosition"));
                } else if(store && op==OpCodes.Call) {
                    var helper=arg as MethodInfo;
                    if(helper!=null && helper.IsStatic && !helper.ContainsGenericParameters && helper.ReturnType==typeof(float)
                        && helper.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(float),typeof(float)}))
                        operand.SetValue(instruction,SumBridge(helper));
                }
                if(op==OpCodes.Ret) {
                    int start=output.Count;emit(OpCodes.Dup,null);emit(OpCodes.Call,Hook("ReturnBody"));
                    var moved=(IList)labels.GetValue(output[start]);foreach(var label in (IList)labels.GetValue(instruction))moved.Add(label);
                    ((IList)labels.GetValue(instruction)).Clear();
                }
                output.Add(instruction);
            }
            var result=Array.CreateInstance(type,output.Count);for(int i=0;i<output.Count;i++)result.SetValue(output[i],i);return result;
        }
    }
}
