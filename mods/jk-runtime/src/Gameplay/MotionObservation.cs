using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;

namespace JKRuntime.Gameplay
{
    /// <summary>Evidence about this horizontal modifier pass, not a complete physics forecast</summary>
    public enum MotionKind { Unknown, ControlledOnly, ExternalMotion }

    /// <summary>A detached, allocation-free result of one native X modifier pass</summary>
    public struct MotionSample
    {
        public MotionKind Kind { get; internal set; }
        public float Step { get; internal set; }
        public float NeutralStep { get; internal set; }
        public int Calls { get; internal set; }
        public long Sequence { get; internal set; }
        public Type Source { get; internal set; }
        public string Reason { get; internal set; }
        public bool IncludesDeclaredReport { get; internal set; }
    }

    /// <summary>on-demand preparation diagnostics, no handler is executed to get this record</summary>
    public sealed class MotionHandlerStatus
    {
        public Type HandlerType { get; internal set; }
        public bool Automatic { get; internal set; }
        public string Reason { get; internal set; }
    }

    /// <summary>prepared arithmetic observers. Own in a world scope, prepare outside gameplay updates</summary>
    public sealed class MotionObservationScope : IDisposable
    {
        private MotionObservation.Hub hub;
        internal MotionObservationScope(MotionObservation.Hub value) { hub=value; }
        /// <summary>bind cheaply at player activation. unknown/unprepared handlers keep their original behavior</summary>
        public MotionObserver Observe(BodyComp body)
        {
            RuntimeApi.Kernel.CheckThread(); if(hub==null) throw new ObjectDisposedException("MotionObservationScope");
            if(body==null) throw new ArgumentNullException("body"); return new MotionObserver(hub,body);
        }
        /// <summary>allocate a detached inventory of prepared types and refusal reasons. Intended for diagnostics, not Update</summary>
        public MotionHandlerStatus[] Inspect()
        {
            RuntimeApi.Kernel.CheckThread(); if(hub==null) throw new ObjectDisposedException("MotionObservationScope");
            return hub.Types.Select(p=>new MotionHandlerStatus {HandlerType=p.Key,Automatic=p.Value.Installed && p.Value.Reason==null,
                Reason=p.Value.Reason ?? "Observed arithmetic"}).ToArray();
        }
        public void Dispose() { RuntimeApi.Kernel.CheckThread(); if(hub==null) return; MotionObservation.Release(hub); hub=null; }
    }

    /// <summary>one consumer of a shared body observation. Disable when unused, disposal never removes foreign handlers</summary>
    public sealed class MotionObserver : IDisposable
    {
        private MotionObservation.Trace trace;
        private MotionObservation.Hub hub;
        private bool enabled;
        internal MotionObserver(MotionObservation.Hub value,BodyComp body)
        {
            hub=value; trace=hub.Traces.GetValue(body,b=>new MotionObservation.Trace(b)); hub.References++; Enabled=true;
        }
        /// <summary>with no enabled consumers on a body, native callbacks bypass all arithmetic recording</summary>
        public bool Enabled
        {
            get { return enabled; }
            set { RuntimeApi.Kernel.CheckThread(); if(trace==null) throw new ObjectDisposedException("MotionObserver"); if(enabled==value) return; enabled=value; trace.Consumers+=value?1:-1; }
        }
        /// <summary>read the last completed native pass, compare Sequence to reject stale observations</summary>
        public MotionSample Read() { RuntimeApi.Kernel.CheckThread(); if(trace==null) throw new ObjectDisposedException("MotionObserver"); return trace.Sample; }
        public void Dispose()
        {
            RuntimeApi.Kernel.CheckThread(); if(trace==null) return;
            Enabled=false; MotionObservation.Release(hub); trace=null; hub=null;
        }
    }

    /// <summary>single-call horizontal arithmetic observation with conservative refusal and optional provider reports</summary>
    public static class MotionObservation
    {
        internal sealed class Plan
        {
            internal int Id;
            internal MethodInfo Method;
            internal bool Installed, Sum;
            internal string Reason;
        }
        internal sealed class Trace
        {
            internal readonly BodyComp Body;
            internal int Consumers, Calls, ExpectedId, ExpectedCalls, Entries, Returns, Sums, Records, VelocityReads;
            internal bool Unknown, External, Recorded, InProgress, Declared, OpaqueCall, Reported, Scalar;
            internal float Neutral, Real, ReturnedNeutral, ReturnedReal, StartX, Input, StartVelocity, Destination;
            internal IBlockBehaviour Handler;
            internal Type Source;
            internal string Reason;
            internal MotionSample Sample;
            internal Trace(BodyComp body) { Body=body; }
        }
        internal sealed class Hub
        {
            internal int References;
            internal readonly OwnedPatches Patches=new OwnedPatches("jkruntime.motion-observation");
            internal readonly Dictionary<Type,Plan> Types=new Dictionary<Type,Plan>();
            internal readonly Dictionary<MethodBase,Plan> Methods=new Dictionary<MethodBase,Plan>();
            internal readonly ConditionalWeakTable<BodyComp,Trace> Traces=new ConditionalWeakTable<BodyComp,Trace>();
            internal readonly Assembly Engine=OwnedPatches.SharedEngine();
        }
        private struct NativeState { internal Trace Previous, Current; }
        private static volatile Hub shared;
        private static int nextId;
        [ThreadStatic] private static Trace active;
        private static readonly MethodInfo Native=typeof(UpdateXPositionFromVelocityBehaviour).GetMethod("ExecuteBehaviour");
        private static readonly FieldInfo Handlers=typeof(UpdateXPositionFromVelocityBehaviour).GetField("m_blockBehaviours",OwnedPatches.Members);
        private static MethodInfo Hook(string name) { return typeof(MotionObservation).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic); }

        /// <summary>Prepare concrete handlers in loaded assemblies referencing JumpKing. use during preparation or an explicit paused action, never from Update</summary>
        public static MotionObservationScope PrepareLoaded()
        {
            RuntimeApi.Kernel.CheckThread(); var types=new List<Type>(); var game=typeof(BodyComp).Assembly;
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if(assembly.IsDynamic || (assembly!=game && !assembly.GetReferencedAssemblies().Any(a=>a.Name==game.GetName().Name))) continue;
                Type[] found;
                try { found=assembly.GetTypes(); } catch(ReflectionTypeLoadException e) { found=e.Types; }
                foreach(var type in found) if(type!=null && !type.IsAbstract && !type.ContainsGenericParameters && typeof(IBlockBehaviour).IsAssignableFrom(type)) types.Add(type);
            }
            return Prepare(types);
        }

        /// <summary>Analyze supplied concrete IBlockBehaviour types and install shared observers. Doesn't construct handlers or call their code</summary>
        public static MotionObservationScope Prepare(IEnumerable<Type> handlerTypes)
        {
            RuntimeApi.Kernel.CheckThread(); if(handlerTypes==null) throw new ArgumentNullException("handlerTypes");
            var types=handlerTypes.Distinct().ToArray(); if(types.Length>1024) throw new ArgumentException("Too many motion handler types");
            bool fresh=shared==null; var hub=shared ?? new Hub(); shared=hub;
            try
            {
                if(fresh)
                {
                    hub.Patches.Add(Native,prefix:Hook("Begin"),priority:int.MaxValue);
                    hub.Patches.Add(Native,transpiler:MotionNativeIl.Bridge(hub.Engine),finalizer:Hook("End"),priority:int.MinValue);
                }
                foreach(var type in types)
                {
                    if(type==null || type.IsAbstract || type.ContainsGenericParameters || !typeof(IBlockBehaviour).IsAssignableFrom(type)) continue;
                    Plan existing; if(hub.Types.TryGetValue(type,out existing)) continue;
                    var map=type.GetInterfaceMap(typeof(IBlockBehaviour));
                    int index=Array.FindIndex(map.InterfaceMethods,m=>m.Name=="ModifyXVelocity");
                    var method=map.TargetMethods[index];
                    Plan plan;
                    if(!hub.Methods.TryGetValue(method,out plan))
                    {
                        plan=new Plan {Id=++nextId,Method=method}; hub.Methods.Add(method,plan);
                        InstallPlan(hub,plan);
                    }
                    hub.Types.Add(type,plan);
                }
                PrepareSums(hub);
                MovementObservation.RefreshMotionHooks();
                hub.References++; return new MotionObservationScope(hub);
            }
            catch
            {
                if(fresh) Release(hub);
                throw;
            }
        }
        internal static Plan FindPlan(MethodBase method) { Plan plan; return shared!=null && shared.Methods.TryGetValue(method,out plan)?plan:null; }
        internal static object Diagnostics()
        {
            var hub=shared;
            return new {active=hub!=null,nativeInstrumented=hub!=null,
                handlers=hub==null?new object[0]:hub.Types.Select(p=>(object)new {type=p.Key.AssemblyQualifiedName,
                    automatic=p.Value.Installed && p.Value.Reason==null,reason=p.Value.Reason ?? "Observed arithmetic"}).ToArray()};
        }
        internal static void Release(Hub hub)
        {
            if(hub.References>1) { hub.References--; return; }
            hub.Patches.Dispose();
            hub.References=0; if(ReferenceEquals(shared,hub)) shared=null;
            MovementObservation.RefreshMotionHooks();
        }
        private static void InstallPlan(Hub hub,Plan plan)
        {
            if(plan.Method.GetMethodBody()==null) { plan.Reason="No managed movement body"; return; }
            hub.Patches.Add(plan.Method,transpiler:MotionIl.Bridge(hub.Engine),priority:int.MinValue);
            plan.Installed=true;
        }
        internal static void DiscoverSum(MethodInfo method)
        {
            var hub=shared;
            if(hub!=null && !hub.Methods.ContainsKey(method) && hub.Methods.Count<2048)
                hub.Methods.Add(method,new Plan {Id=++nextId,Method=method,Sum=true});
        }
        private static void PrepareSums(Hub hub)
        {
            // discovery happens in the native transpiler; patch helpers only at preparation boundaries
            foreach(var plan in hub.Methods.Values.Where(p=>p.Sum && !p.Installed).ToArray()) InstallPlan(hub,plan);
        }
        /// <summary>Prepare newly discovered position helpers outside gameplay updates. Each invocation supplies its own motion evidence.</summary>
        public static void RefreshPhysicsAdapters()
        {
            RuntimeApi.Kernel.CheckThread(); if(shared!=null) PrepareSums(shared);
        }
        internal static void RefreshNativeValidity()
        {
            // retained for stage-observer lifecycle calls; no owner list controls motion evidence
        }
        private static void Begin(UpdateXPositionFromVelocityBehaviour __instance,BehaviourContext __0,out NativeState __state)
        {
            __state=new NativeState {Previous=active}; active=null;
            var hub=shared; Trace trace;
            if(hub==null || !hub.Traces.TryGetValue(__0.BodyComp,out trace) || trace.Consumers==0) return;
            if(trace.InProgress) { trace.Unknown=true; trace.Reason="Reentrant body movement"; return; }
            __state.Current=trace; active=trace; trace.InProgress=true;
            trace.Calls=trace.Entries=trace.Returns=trace.Sums=trace.VelocityReads=0; trace.Unknown=false; trace.External=false; trace.Declared=false; trace.Neutral=0;
            trace.ExpectedCalls=((LinkedList<IBlockBehaviour>)Handlers.GetValue(__instance)).Count;
            trace.Scalar=false; trace.StartVelocity=__0.BodyComp.Velocity.X;
            trace.Real=__0.BodyComp.Velocity.X; trace.StartX=__0.BodyComp.Position.X; trace.Source=null;
            trace.Reason=null;
        }
        private static void End(Exception __exception,NativeState __state)
        {
            var trace=__state.Current;
            try
            {
                if(trace==null) return;
                if(trace.Entries!=1 || trace.Returns!=1 || trace.Sums!=1 || trace.VelocityReads!=1 || trace.Calls!=trace.ExpectedCalls)
                    Refuse(trace,"Incomplete movement path in this step");
                if(trace.Destination!=trace.StartX+trace.Real) Refuse(trace,"Position arithmetic changed the observed displacement");
                float fraction=PlayerPhysicsStep.Fraction(trace.Body);
                // SFC scales the native position delta, after the float addition
                // keep that rounding before scaling too, without accepting a tolerance
                if(fraction!=1) {
                    float nativeX=trace.StartX+trace.Real;
                    trace.Real=nativeX-trace.StartX;
                }
                trace.Real*=fraction; trace.Neutral*=fraction;
                if(__exception!=null || trace.Body.Position.X!=trace.StartX+trace.Real) { trace.Unknown=true; trace.Reason="Movement did not complete its observed displacement"; }
                trace.Sample=new MotionSample {Kind=trace.Unknown?MotionKind.Unknown:trace.External?MotionKind.ExternalMotion:MotionKind.ControlledOnly,
                    Step=trace.Real,NeutralStep=trace.Neutral,Calls=trace.Calls,Sequence=trace.Sample.Sequence+1,Source=trace.Source,Reason=trace.Reason,IncludesDeclaredReport=trace.Declared};
                trace.Handler=null; trace.Scalar=false; trace.InProgress=false;
            }
            finally { active=__state.Previous; }
        }
        private static float Invoke(IBlockBehaviour handler,float input,BehaviourContext context)
        {
            var trace=active;
            if(trace==null || !ReferenceEquals(trace.Body,context.BodyComp)) return handler.ModifyXVelocity(input,context);
            Plan plan; shared.Types.TryGetValue(handler.GetType(),out plan);
            if(input!=trace.Real) Refuse(trace,"Movement input changed outside observed arithmetic");
            trace.Handler=handler; trace.Recorded=false; trace.Records=0; trace.OpaqueCall=trace.Reported=false; trace.ExpectedId=plan==null?-1:plan.Id; trace.Input=input;
            float result;
            try { result=handler.ModifyXVelocity(input,context); }
            finally { trace.Handler=null; }
            trace.Calls++; trace.Real=result;
            if(trace.OpaqueCall || !trace.Recorded || trace.ReturnedReal!=result || !Finite(result) || !Finite(trace.ReturnedNeutral))
            {
                trace.Unknown=true; trace.Source=handler.GetType();
                trace.Reason=trace.OpaqueCall?"Executed unobserved movement arithmetic":plan==null?"Unprepared handler without an explicit report":plan.Reason ?? "Missing or inconsistent arithmetic report";
            }
            else
            {
                trace.Neutral=trace.ReturnedNeutral;
                if(trace.Neutral!=0) { trace.External=true; trace.Source=handler.GetType(); }
                if(input!=0 && result!=0 && Math.Sign(input)!=Math.Sign(result)) { trace.Unknown=true; trace.Reason="Movement reversed direction"; }
            }
            return result;
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static void Refuse(Trace trace,string reason) { trace.Unknown=true; trace.Reason=reason; }
        private static void EnterBody(BehaviourContext context)
        {
            var trace=active;if(trace==null)return;trace.Entries++;
            if(!ReferenceEquals(trace.Body,context.BodyComp) || trace.Body.Position.X!=trace.StartX || trace.Body.Velocity.X!=trace.StartVelocity)
                Refuse(trace,"Movement state changed before the native body");
        }
        private static void ReturnBody(bool result) { if(active!=null) { active.Returns++; if(!result) Refuse(active,"Movement body did not complete"); } }
        private static float ReadVelocity(BodyComp body)
        {
            var trace=active;float value=body.Velocity.X;
            if(trace!=null) { trace.VelocityReads++;if(!ReferenceEquals(body,trace.Body) || value!=trace.StartVelocity)Refuse(trace,"Native velocity source changed"); }
            return value;
        }
        private static void EnterArithmetic(float input,int id)
        {
            var trace=active;
            if(trace!=null && (trace.Handler!=null || trace.Scalar) && trace.ExpectedId==id && input!=trace.Input)
                Refuse(trace,"Movement argument changed before arithmetic");
        }
        private static float AddPosition(float position,float step)
        {
            float result=position+step;var trace=active;if(trace==null)return result;
            CheckSum(trace,position,step);trace.Destination=result;return result;
        }
        private static void CheckSum(Trace trace,float position,float step)
        {
            trace.Sums++;
            if(position!=trace.StartX || step!=trace.Real || trace.Entries!=1) Refuse(trace,"Unobserved position or displacement before integration");
        }
        internal static float Sum(MethodInfo method,Func<float,float,float> call,float position,float step)
        {
            var trace=active;if(trace==null)return call(position,step);
            CheckSum(trace,position,step);
            Plan plan;shared.Methods.TryGetValue(method,out plan);
            trace.Scalar=true;trace.Input=step;trace.ExpectedId=plan==null?-1:plan.Id;trace.Recorded=false;trace.Records=0;trace.OpaqueCall=trace.Reported=false;
            float result;
            try { result=call(position,step); } finally { trace.Scalar=false; }
            if(!trace.Recorded || trace.OpaqueCall || trace.ReturnedReal!=result || !Finite(result) || !Finite(trace.ReturnedNeutral))
                Refuse(trace,"Unobserved position arithmetic executed in this step");
            else {
                if(trace.ReturnedNeutral!=position)trace.External=true;
                trace.Real=result-position;trace.Neutral=trace.ReturnedNeutral-position;
            }
            trace.Destination=result;return result;
        }
        private static float NeutralArgument() { return active==null?0:active.Neutral; }
        private static void UnobservedCall() { if(active!=null && (active.Handler!=null || active.Scalar)) active.OpaqueCall=true; }
        private static void Record(float actual,float neutral,int id)
        {
            var trace=active; if(trace==null || (trace.Handler==null && !trace.Scalar) || trace.ExpectedId!=id || trace.Reported) return;
            if(++trace.Records!=1)Refuse(trace,"Reentrant movement arithmetic");
            trace.Recorded=true; trace.ReturnedReal=actual; trace.ReturnedNeutral=neutral;
        }
        /// <summary>Neutral input for an explicit single-call provider report. Zero outside this handler's observed invocation</summary>
        public static float GetNeutralInput(IBlockBehaviour source) { return active!=null && ReferenceEquals(active.Handler,source)?active.Neutral:0; }
        /// <summary>Declare the result of applying the provider's arithmetic to GetNeutralInput. Doesn't change gameplay, false reports remain the provider's responsibility</summary>
        public static void Report(IBlockBehaviour source,float actualResult,float neutralResult)
        {
            if(active!=null && ReferenceEquals(active.Handler,source))
            {
                active.Declared=true; active.OpaqueCall=false;
                // automatic return instrumentation mustn't replace the provider's neutral result
                active.Recorded=active.Reported=true; active.ReturnedReal=actualResult; active.ReturnedNeutral=neutralResult;
            }
        }
    }
}
