using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using JKRuntime.Gameplay;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static partial class MotionObservationTests
    {
        private static int nativeMode,handlerMode,sumMode,prefixCalls,postfixCalls,helperCalls;
        private static bool ConditionalNativePrefix(BehaviourContext __0,ref bool __result)
        {
            prefixCalls++;
            if(nativeMode==1)__0.BodyComp.Velocity.X+=.5f;
            if(nativeMode==2) { __0.BodyComp.Position.X+=__0.BodyComp.Velocity.X;__result=true;return false; }
            return true;
        }
        private static void ConditionalNativePostfix(BehaviourContext __0)
        { postfixCalls++;if(nativeMode==3)__0.BodyComp.Position.X+=.5f; }
        private static bool ConditionalHandlerPrefix(ref float __0,ref float __result)
        {
            if(handlerMode==1)__0+=.25f;
            if(handlerMode==2) { __result=__0;return false; }
            return true;
        }
        private static void ConditionalHandlerPostfix(ref float __result) { if(handlerMode==3)__result+=.25f; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float OpaquePosition(float position,float speed) { helperCalls++;return position+speed; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float ConditionalPosition(float position,float speed)
        {
            helperCalls++;
            if(sumMode==1)return position+1.5f;
            if(sumMode==2)return OpaquePosition(position,speed);
            if(sumMode==3)return position+speed+.5f;
            return position+speed;
        }
        private static MethodInfo PatchMethod(string name) { return typeof(MotionObservationTests).GetMethod(name,OwnedPatches.Members); }
        private static MethodInfo PositionTranspiler()
        {
            var instruction=OwnedPatches.SharedEngine().GetType("HarmonyLib.CodeInstruction",true);
            var enumerable=typeof(IEnumerable<>).MakeGenericType(instruction);
            var module=AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("UnknownPositionFixture"),AssemblyBuilderAccess.Run).DefineDynamicModule("fixture");
            var type=module.DefineType("Transform",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed);
            var field=type.DefineField("Callback",typeof(Func<IEnumerable,IEnumerable>),FieldAttributes.Public|FieldAttributes.Static);
            var method=type.DefineMethod("Rewrite",MethodAttributes.Public|MethodAttributes.Static,enumerable,new[]{enumerable});
            method.DefineParameter(1,ParameterAttributes.None,"instructions");
            var il=method.GetILGenerator();il.Emit(OpCodes.Ldsfld,field);il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt,typeof(Func<IEnumerable,IEnumerable>).GetMethod("Invoke"));il.Emit(OpCodes.Castclass,enumerable);il.Emit(OpCodes.Ret);
            var created=type.CreateType();
            created.GetField("Callback").SetValue(null,new Func<IEnumerable,IEnumerable>(source=> {
                var values=source.Cast<object>().ToArray();var opcode=instruction.GetField("opcode");var operand=instruction.GetField("operand");
                foreach(var value in values)if((OpCode)opcode.GetValue(value)==OpCodes.Add) { opcode.SetValue(value,OpCodes.Call);operand.SetValue(value,PatchMethod("ConditionalPosition")); }
                var result=Array.CreateInstance(instruction,values.Length);Array.Copy(values,result,values.Length);return result;
            }));
            return created.GetMethod("Rewrite");
        }
        private static void PatchStepChecks()
        {
            var native=typeof(UpdateXPositionFromVelocityBehaviour).GetMethod("ExecuteBehaviour");
            var body=new BodyComp(Vector2.Zero,18,26);var context=new BehaviourContext(body);
            var scale=new Scale {Factor=1};var handlers=new LinkedList<IBlockBehaviour>();handlers.AddLast(scale);
            var stage=new UpdateXPositionFromVelocityBehaviour(handlers);
            using(var scope=MotionObservation.Prepare(new[]{typeof(Scale)}))
            using(var observer=scope.Observe(body))
            {
                using(var foreign=new OwnedPatches(Guid.NewGuid().ToString("N")))
                {
                    foreign.Add(native,prefix:PatchMethod("ConditionalNativePrefix"),postfix:PatchMethod("ConditionalNativePostfix"));
                    foreach(int mode in new[]{0,1,0,2,0,3,0}) {
                        nativeMode=mode;int calls=scale.Calls,before=prefixCalls,after=postfixCalls;
                        Tick(stage,context,1.5f);
                        Require(observer.Read().Kind==(mode==0?MotionKind.ControlledOnly:MotionKind.Unknown),"Native patch classified by presence instead of current effect: "+mode+" "+observer.Read().Reason);
                        Require(prefixCalls==before+1&&postfixCalls==after+1&&scale.Calls==calls+(mode==2?0:1),"Native patch or callback was replayed");
                        Require(body.Position.X==(mode==1||mode==3?2f:1.5f),"Observation changed foreign movement");
                    }
                    foreign.Add(typeof(Scale).GetMethod("ModifyXVelocity"),prefix:PatchMethod("ConditionalHandlerPrefix"),postfix:PatchMethod("ConditionalHandlerPostfix"));
                    foreach(int mode in new[]{0,1,0,2,0,3,0}) {
                        handlerMode=mode;int calls=scale.Calls;Tick(stage,context,1.5f);
                        Require(observer.Read().Kind==(mode==0?MotionKind.ControlledOnly:MotionKind.Unknown),"Handler patch did not recover per step: "+mode+" "+observer.Read().Reason);
                        Require(scale.Calls==calls+(mode==2?0:1),"Patched handler was replayed");
                    }
                }
                Tick(stage,context,1.5f);Require(observer.Read().Kind==MotionKind.ControlledOnly,"Unpatch retained a stale global refusal");
            }
            using(var foreign=new OwnedPatches(Guid.NewGuid().ToString("N")))
            {
                foreign.Add(native,transpiler:PositionTranspiler());
                using(var scope=MotionObservation.Prepare(new[]{typeof(Scale)}))
                using(var observer=scope.Observe(body))
                {
                    foreach(int mode in new[]{0,1,0,2,0,3,0}) {
                        sumMode=mode;int calls=helperCalls;Tick(stage,context,1.5f);
                        Require(mode==0?observer.Read().Kind==MotionKind.ControlledOnly:observer.Read().Kind!=MotionKind.ControlledOnly,
                            "Executed position-helper path was misclassified: "+mode+" "+observer.Read().Reason);
                        Require(helperCalls==calls+(mode==2?2:1),"Position helper was replayed");
                        Require(body.Position.X==(mode==3?2f:1.5f),"Position helper behavior changed");
                    }
                }
            }
            nativeMode=handlerMode=sumMode=0;
            Console.WriteLine("[OK] Unknown patch owners: inert/active prefixes and postfixes, skipped bodies with matching displacement, conditional position helpers, same-result external motion, single calls and immediate recovery");
        }
    }
}
