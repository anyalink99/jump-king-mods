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
    /// <summary>Evidence about this horizontal modifier pass, not a complete physics forecast.</summary>
    public enum MotionKind { Unknown, ControlledOnly, ExternalMotion }

    /// <summary>A detached, allocation-free result of one native X modifier pass.</summary>
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

    /// <summary>On-demand preparation diagnostics; no handler is executed to obtain this record.</summary>
    public sealed class MotionHandlerStatus
    {
        public Type HandlerType { get; internal set; }
        public bool Automatic { get; internal set; }
        public string Reason { get; internal set; }
    }

    /// <summary>Prepared arithmetic observers. Own in a world scope; prepare outside gameplay updates.</summary>
    public sealed class MotionObservationScope : IDisposable
    {
        private MotionObservation.Hub hub;
        internal MotionObservationScope(MotionObservation.Hub value) { hub=value; }
        /// <summary>Bind cheaply at player activation. Unknown/unprepared handlers retain their original behavior.</summary>
        public MotionObserver Observe(BodyComp body)
        {
            RuntimeApi.Kernel.CheckThread(); if(hub==null) throw new ObjectDisposedException("MotionObservationScope");
            if(body==null) throw new ArgumentNullException("body"); return new MotionObserver(hub,body);
        }
        /// <summary>Allocate a detached inventory of prepared types and refusal reasons. Intended for diagnostics, not Update.</summary>
        public MotionHandlerStatus[] Inspect()
        {
            RuntimeApi.Kernel.CheckThread(); if(hub==null) throw new ObjectDisposedException("MotionObservationScope");
            return hub.Types.Select(p=>new MotionHandlerStatus {HandlerType=p.Key,Automatic=!p.Value.Stale && p.Value.Reason==null,
                Reason=p.Value.Reason ?? (p.Value.Stale?"Movement patch graph changed":p.Value.Identity?"Managed identity":"Observed arithmetic")}).ToArray();
        }
        public void Dispose() { RuntimeApi.Kernel.CheckThread(); if(hub==null) return; MotionObservation.Release(hub); hub=null; }
    }

    /// <summary>One consumer of a shared body observation. Disable when unused; disposal never removes foreign handlers.</summary>
    public sealed class MotionObserver : IDisposable
    {
        private MotionObservation.Trace trace;
        private MotionObservation.Hub hub;
        private bool enabled;
        internal MotionObserver(MotionObservation.Hub value,BodyComp body)
        {
            hub=value; trace=hub.Traces.GetValue(body,b=>new MotionObservation.Trace(b)); hub.References++; Enabled=true;
        }
        /// <summary>With no enabled consumers on a body, native callbacks bypass all arithmetic recording.</summary>
        public bool Enabled
        {
            get { return enabled; }
            set { RuntimeApi.Kernel.CheckThread(); if(trace==null) throw new ObjectDisposedException("MotionObserver"); if(enabled==value) return; enabled=value; trace.Consumers+=value?1:-1; }
        }
        /// <summary>Read the last completed native pass; compare Sequence to reject stale observations.</summary>
        public MotionSample Read() { RuntimeApi.Kernel.CheckThread(); if(trace==null) throw new ObjectDisposedException("MotionObserver"); return trace.Sample; }
        public void Dispose()
        {
            RuntimeApi.Kernel.CheckThread(); if(trace==null) return;
            Enabled=false; MotionObservation.Release(hub); trace=null; hub=null;
        }
    }

    /// <summary>Single-call horizontal arithmetic observation with conservative refusal and optional provider reports.</summary>
    public static class MotionObservation
    {
        internal sealed class Plan
        {
            internal int Id;
            internal MethodInfo Method;
            internal bool Rejected;
            internal MethodValidityLease Validity;
            internal bool Stale { get { return Rejected || (Validity!=null && !Validity.IsValid); } set { Rejected=value; } }
            internal bool Identity;
            internal string Reason;
        }
        internal sealed class Trace
        {
            internal readonly BodyComp Body;
            internal int Consumers, Calls, ExpectedId;
            internal bool Unknown, External, Recorded, InProgress, Declared;
            internal float Neutral, Real, ReturnedNeutral, ReturnedReal, StartX;
            internal IBlockBehaviour Handler;
            internal Type Source;
            internal string Reason;
            internal MotionSample Sample;
            internal Trace(BodyComp body) { Body=body; }
        }
        internal sealed class Hub
        {
            internal int References;
            internal MethodValidityLease NativeValidity;
            internal bool NativeStale { get { return NativeValidity==null || !NativeValidity.IsValid; } }
            internal readonly OwnedPatches Patches=new OwnedPatches("jkruntime.motion-observation");
            internal readonly Dictionary<Type,Plan> Types=new Dictionary<Type,Plan>();
            internal readonly Dictionary<MethodBase,Plan> Methods=new Dictionary<MethodBase,Plan>();
            internal readonly ConditionalWeakTable<BodyComp,Trace> Traces=new ConditionalWeakTable<BodyComp,Trace>();
            internal readonly Assembly Engine=OwnedPatches.SharedEngine();
            internal MethodInfo PatchInfo;
        }
        private struct NativeState { internal Trace Previous, Current; }
        private static volatile Hub shared;
        private static int nextId;
        [ThreadStatic] private static Trace active;
        private static readonly MethodInfo Native=typeof(UpdateXPositionFromVelocityBehaviour).GetMethod("ExecuteBehaviour");
        private static MethodInfo Hook(string name) { return typeof(MotionObservation).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic); }

        /// <summary>Prepare concrete handlers in loaded assemblies referencing JumpKing. Use during preparation or an explicit paused action, never from Update.</summary>
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

        /// <summary>Analyze supplied concrete IBlockBehaviour types and install shared observers. Does not construct handlers or invoke their code.</summary>
        public static MotionObservationScope Prepare(IEnumerable<Type> handlerTypes)
        {
            RuntimeApi.Kernel.CheckThread(); if(handlerTypes==null) throw new ArgumentNullException("handlerTypes");
            var types=handlerTypes.Distinct().ToArray(); if(types.Length>1024) throw new ArgumentException("Too many motion handler types");
            bool fresh=shared==null; var hub=shared ?? new Hub(); shared=hub;
            try
            {
                if(fresh)
                {
                    hub.PatchInfo=hub.Engine.GetType("HarmonyLib.Harmony",true).GetMethod("GetPatchInfo",new[]{typeof(MethodBase)});
                    hub.Patches.ReplaceCalls(Native,typeof(IBlockBehaviour).GetMethod("ModifyXVelocity"),Hook("Invoke"),1);
                    hub.Patches.Add(Native,prefix:Hook("Begin"),finalizer:Hook("End"));
                    RefreshNativeValidity();
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
                        if(ForeignPatches(hub,method)) { plan.Stale=true; plan.Reason="Foreign patches on movement handler"; }
                        else if(method.GetMethodBody()==null) plan.Reason="No managed movement body";
                        else
                        {
                            var bytes=method.GetMethodBody().GetILAsByteArray();
                            // Exact managed identity body: ldarg.1; ret. No calls,
                            // locals or writes to instrument, and no foreign patch.
                            plan.Identity=bytes.Length==2 && bytes[0]==3 && bytes[1]==42;
                            if(!plan.Identity) hub.Patches.Add(method,transpiler:MotionIl.Bridge(hub.Engine));
                        }
                    }
                    if(plan.Validity==null)plan.Validity=MethodValidity.Watch(method,"jkruntime.motion-observation");
                    hub.Types.Add(type,plan);
                }
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
            return new {active=hub!=null,nativeCovered=hub!=null && !hub.NativeStale,
                handlers=hub==null?new object[0]:hub.Types.Select(p=>(object)new {type=p.Key.AssemblyQualifiedName,
                    automatic=!p.Value.Stale && p.Value.Reason==null,reason=p.Value.Reason ?? (p.Value.Stale?"Patch graph changed":p.Value.Identity?"Managed identity":"Observed arithmetic")}).ToArray()};
        }
        internal static void Release(Hub hub)
        {
            if(hub.References>1) { hub.References--; return; }
            hub.Patches.Dispose();
            foreach(var plan in hub.Methods.Values)if(plan.Validity!=null)plan.Validity.Dispose();
            if(hub.NativeValidity!=null)hub.NativeValidity.Dispose();
            hub.References=0; if(ReferenceEquals(shared,hub)) shared=null;
            MovementObservation.RefreshMotionHooks();
        }
        private static bool ForeignPatches(Hub hub,MethodBase method)
        {
            object info=hub.PatchInfo.Invoke(null,new object[]{method}); if(info==null) return false;
            foreach(string kind in new[]{"Prefixes","Postfixes","Transpilers","Finalizers"})
                foreach(var patch in (IEnumerable)info.GetType().GetField(kind).GetValue(info))
                    if((string)patch.GetType().GetField("owner").GetValue(patch)!="jkruntime.motion-observation") return true;
            return false;
        }
        internal static void RefreshNativeValidity()
        {
            var hub=shared; if(hub==null)return;
            var value=MethodValidity.Watch(Native,"jkruntime.motion-observation","jkruntime.movement-observation");
            if(hub.NativeValidity!=null)hub.NativeValidity.Dispose(); hub.NativeValidity=value;
        }
        private static void Begin(BehaviourContext __0,out NativeState __state)
        {
            __state=new NativeState {Previous=active}; active=null;
            var hub=shared; Trace trace;
            if(hub==null || !hub.Traces.TryGetValue(__0.BodyComp,out trace) || trace.Consumers==0) return;
            if(trace.InProgress) { trace.Unknown=true; trace.Reason="Reentrant body movement"; return; }
            __state.Current=trace; active=trace; trace.InProgress=true;
            trace.Calls=0; trace.Unknown=hub.NativeStale; trace.External=false; trace.Declared=false; trace.Neutral=0;
            trace.Real=__0.BodyComp.Velocity.X; trace.StartX=__0.BodyComp.Position.X; trace.Source=null;
            trace.Reason=hub.NativeStale?"Native movement patch graph is not covered":null;
        }
        private static void End(Exception __exception,NativeState __state)
        {
            var trace=__state.Current;
            try
            {
                if(trace==null) return;
                if(__exception!=null || trace.Body.Position.X!=trace.StartX+trace.Real) { trace.Unknown=true; trace.Reason="Movement did not complete its observed displacement"; }
                trace.Sample=new MotionSample {Kind=trace.Unknown?MotionKind.Unknown:trace.External?MotionKind.ExternalMotion:MotionKind.ControlledOnly,
                    Step=trace.Real,NeutralStep=trace.Neutral,Calls=trace.Calls,Sequence=trace.Sample.Sequence+1,Source=trace.Source,Reason=trace.Reason,IncludesDeclaredReport=trace.Declared};
                trace.Handler=null; trace.InProgress=false;
            }
            finally { active=__state.Previous; }
        }
        private static float Invoke(IBlockBehaviour handler,float input,BehaviourContext context)
        {
            var trace=active;
            if(trace==null || !ReferenceEquals(trace.Body,context.BodyComp)) return handler.ModifyXVelocity(input,context);
            Plan plan; shared.Types.TryGetValue(handler.GetType(),out plan);
            trace.Handler=handler; trace.Recorded=false; trace.ExpectedId=plan==null?-1:plan.Id;
            float result;
            try { result=handler.ModifyXVelocity(input,context); }
            finally { trace.Handler=null; }
            if(plan!=null && plan.Identity && result==input) { trace.Recorded=true; trace.ReturnedReal=result; trace.ReturnedNeutral=trace.Neutral; }
            trace.Calls++; trace.Real=result;
            if(!trace.Recorded || trace.ReturnedReal!=result || !Finite(result) || !Finite(trace.ReturnedNeutral) || (plan!=null && plan.Stale))
            {
                trace.Unknown=true; trace.Source=handler.GetType();
                trace.Reason=plan==null?"Unprepared handler without an explicit report":plan.Reason ?? "Missing or inconsistent arithmetic report";
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
        private static float NeutralArgument() { return active==null?0:active.Neutral; }
        private static void Record(float actual,float neutral,int id)
        {
            var trace=active; if(trace==null || trace.Handler==null || trace.ExpectedId!=id) return;
            trace.Recorded=true; trace.ReturnedReal=actual; trace.ReturnedNeutral=neutral;
        }
        /// <summary>Neutral input for an explicit single-call provider report. Zero outside this handler's observed invocation.</summary>
        public static float GetNeutralInput(IBlockBehaviour source) { return active!=null && ReferenceEquals(active.Handler,source)?active.Neutral:0; }
        /// <summary>Declare the result of applying the provider's arithmetic to GetNeutralInput. Does not change gameplay; false reports remain the provider's responsibility.</summary>
        public static void Report(IBlockBehaviour source,float actualResult,float neutralResult)
        {
            if(active!=null && ReferenceEquals(active.Handler,source)) { active.Declared=true; Record(actualResult,neutralResult,active.ExpectedId); }
        }
    }
}
