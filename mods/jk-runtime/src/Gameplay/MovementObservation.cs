using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime.Gameplay
{
    public enum MovementStage { Wind, XMovement, XCollision, YMovement, YCollision, Gravity, Materials }
    public enum ContactPath { None, Native, Additional }
    /// <summary>Last accepted actual collision query in a resolver invocation. It identifies the accepting path, not a unique supporting block.</summary>
    public struct ContactEvidence
    {
        public ContactPath Path { get; internal set; }
        public Type HandlerType { get; internal set; }
        public Rectangle Probe { get; internal set; }
        public int Direction { get; internal set; }
        public int AcceptedQueries { get; internal set; }
    }
    /// <summary>Detached before/after evidence for one actual native stage. Sequence is per body, not a global frame number.</summary>
    public struct MovementSample
    {
        public long Sequence { get; internal set; }
        public MovementStage Stage { get; internal set; }
        public Vector2 PositionBefore { get; internal set; }
        public Vector2 PositionAfter { get; internal set; }
        public Vector2 VelocityBefore { get; internal set; }
        public Vector2 VelocityAfter { get; internal set; }
        public bool Completed { get; internal set; }
        public bool Covered { get; internal set; }
        public ContactEvidence Contact { get; internal set; }
    }
    /// <summary>World-owned dormant stage/contact hooks. Bind observers only at player activation.</summary>
    public sealed class MovementObservationScope : IDisposable
    {
        internal MovementObservation.Hub Hub;
        internal MovementObservationScope(MovementObservation.Hub hub) { Hub=hub; }
        public MovementObserver Observe(BodyComp body)
        { RuntimeApi.Kernel.CheckThread(); if(Hub==null || Hub.Closing)throw new ObjectDisposedException("MovementObservationScope"); if(body==null)throw new ArgumentNullException("body"); return new MovementObserver(Hub,body); }
        public void Dispose() { RuntimeApi.Kernel.CheckThread(); if(Hub==null)return; MovementObservation.Release(Hub); Hub=null; }
    }
    /// <summary>Shared per-body evidence; recording is off by default. Reads/capture/export are game-thread operations.</summary>
    public sealed class MovementObserver : IDisposable
    {
        private MovementObservation.Hub hub;
        private MovementObservation.Track track;
        private bool enabled,recording;
        internal MovementObserver(MovementObservation.Hub value,BodyComp body)
        {
            hub=value;
            if(!hub.Bodies.TryGetValue(body,out track)) { track=new MovementObservation.Track(body); hub.Bodies.Add(body,track); }
            track.References++; hub.References++; Enabled=true;
        }
        public bool Enabled
        {
            get { return enabled; }
            set { Check(); if(enabled==value)return; if(recording)track.Recorders+=value?1:-1; enabled=value; track.Consumers+=value?1:-1; }
        }
        /// <summary>Opt in to a shared 256-event ring. Other readers can also record; switching this off does not erase their history.</summary>
        public bool Recording
        {
            get { return recording; }
            set { Check(); if(recording==value)return; if(value && track.Ring==null)track.Ring=new MovementSample[256]; recording=value; if(enabled)track.Recorders+=value?1:-1; }
        }
        /// <summary>Latest completed publication for the chosen stage. Sequence zero means unobserved; compare it to reject stale data.</summary>
        public MovementSample Read(MovementStage stage)
        { Check(); int index=(int)stage; if(index<0 || index>=7)throw new ArgumentOutOfRangeException("stage"); return track.Latest[index]; }
        /// <summary>Allocate a detached oldest-first trace, including overwritten-record count. Does not enable recording.</summary>
        public MovementTrace Capture()
        {
            Check(); var samples=new MovementSample[track.Count];
            for(int i=0;i<samples.Length;i++)samples[i]=track.Ring[(track.Next-track.Count+i+256)%256];
            return new MovementTrace(samples,track.Dropped,MotionObservation.Diagnostics());
        }
        public void Dispose()
        {
            RuntimeApi.Kernel.CheckThread(); if(track==null)return;
            Enabled=false; MovementObservation.Release(hub);
            if(--track.References==0)hub.Bodies.Remove(track.Body);
            track=null; hub=null;
        }
        private void Check() { RuntimeApi.Kernel.CheckThread(); if(track==null)throw new ObjectDisposedException("MovementObserver"); }
    }
    /// <summary>Detached bounded diagnostic export; contains no body references. Not sufficient for deterministic replay.</summary>
    public sealed class MovementTrace
    {
        private readonly MovementSample[] samples;
        private readonly object coverage;
        public long Dropped { get; private set; }
        internal MovementTrace(MovementSample[] values,long dropped,object motionCoverage) { samples=values; Dropped=dropped; coverage=motionCoverage; }
        public MovementSample[] Read() { return (MovementSample[])samples.Clone(); }
        /// <summary>Format only on explicit request. Type identities are text; arbitrary foreign objects are never serialized.</summary>
        public string ToJson()
        {
            return new JavaScriptSerializer().Serialize(new {schema="jkruntime.movement-trace.v1",runtime=RuntimeApi.Version,dropped=Dropped,motionCoverageAtCapture=coverage,
                samples=samples.Select(s=>new {sequence=s.Sequence,stage=s.Stage.ToString(),completed=s.Completed,covered=s.Covered,
                    positionBefore=new[]{Scalar(s.PositionBefore.X),Scalar(s.PositionBefore.Y)},positionAfter=new[]{Scalar(s.PositionAfter.X),Scalar(s.PositionAfter.Y)},
                    velocityBefore=new[]{Scalar(s.VelocityBefore.X),Scalar(s.VelocityBefore.Y)},velocityAfter=new[]{Scalar(s.VelocityAfter.X),Scalar(s.VelocityAfter.Y)},
                    contact=new {path=s.Contact.Path.ToString(),handler=s.Contact.HandlerType==null?null:s.Contact.HandlerType.AssemblyQualifiedName,
                        probe=new[]{s.Contact.Probe.X,s.Contact.Probe.Y,s.Contact.Probe.Width,s.Contact.Probe.Height},direction=s.Contact.Direction,acceptedQueries=s.Contact.AcceptedQueries}}).ToArray()});
        }
        private static float? Scalar(float value) { return float.IsNaN(value) || float.IsInfinity(value)?(float?)null:value; }
        /// <summary>Write this detached capture to an explicitly chosen new file. Existing files are preserved.</summary>
        public void Export(string path)
        { using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))using(var writer=new StreamWriter(stream))writer.Write(ToJson()); }
    }
    /// <summary>Observe actual native stages and accepted collision queries without replaying handlers or changing their results.</summary>
    public static class MovementObservation
    {
        internal sealed class Track
        {
            internal readonly BodyComp Body;
            internal readonly MovementSample[] Latest=new MovementSample[7];
            internal MovementSample[] Ring;
            internal int Consumers,Recorders,References,Next,Count;
            internal long Sequence,Dropped;
            internal Track(BodyComp body) { Body=body; }
            internal void Publish(MovementSample value)
            {
                value.Sequence=++Sequence; Latest[(int)value.Stage]=value;
                if(Recorders==0)return;
                Ring[Next]=value; Next=(Next+1)%256; if(Count==256)Dropped++; else Count++;
            }
        }
        internal sealed class Plan
        {
            internal MovementStage Stage;
            internal MethodInfo Method,Additional;
            internal MethodValidityLease Validity,AdditionalValidity;
            internal bool Covered { get { return Validity!=null && Validity.IsValid && (AdditionalValidity==null || AdditionalValidity.IsValid); } }
        }
        internal sealed class Hub
        {
            internal int References;
            internal bool Ready,Closing;
            internal readonly OwnedPatches Patches=new OwnedPatches("jkruntime.movement-observation");
            internal readonly Dictionary<MethodBase,Plan> Plans=new Dictionary<MethodBase,Plan>();
            internal readonly Plan[] Stages=new Plan[7];
            internal readonly Dictionary<BodyComp,Track> Bodies=new Dictionary<BodyComp,Track>();
        }
        private struct Invocation
        {
            internal Track Track;
            internal Plan Plan;
            internal MovementSample Sample;
            internal bool Reentrant;
            internal Rectangle Probe;
        }
        private struct State { internal Invocation Previous; internal bool Capturing; }
        [ThreadStatic] private static Invocation active;
        private static Hub shared;
        private static MethodInfo Hook(string name) { return typeof(MovementObservation).GetMethod(name,OwnedPatches.Members); }
        /// <summary>Install shared dormant hooks once during preparation. No assembly scan, handler construction or player lookup.</summary>
        public static MovementObservationScope Prepare()
        {
            RuntimeApi.Kernel.CheckThread();
            if(shared!=null && !shared.Ready)Release(shared);
            if(shared!=null && shared.Closing)throw new InvalidOperationException("Movement observation cleanup is incomplete");
            if(shared!=null) { shared.References++; return new MovementObservationScope(shared); }
            var hub=new Hub(); shared=hub;
            try
            {
                var names=new[]{"WindVelocityUpdateBehaviour","UpdateXPositionFromVelocityBehaviour","ResolveXCollisionBehaviour","UpdateYPositionFromVelocityBehaviour","ResolveYCollisionBehaviour","ApplyGravityBehaviour","ExecuteBlockBehaviours"};
                for(int i=0;i<names.Length;i++)
                {
                    var type=typeof(BodyComp).Assembly.GetType("JumpKing.BodyCompBehaviours."+names[i],true);
                    var plan=new Plan {Stage=(MovementStage)i,Method=type.GetMethod("ExecuteBehaviour")}; hub.Plans.Add(plan.Method,plan); hub.Stages[i]=plan;
                    hub.Patches.Add(plan.Method,prefix:Hook("Begin"+i),finalizer:Hook("End"));
                    if(i==2 || i==4)
                    {
                        plan.Additional=type.GetMethod("AnyAdditionalCollisionChecks",OwnedPatches.Members);
                        hub.Patches.ReplaceCalls(plan.Method,typeof(ICollisionQuery).GetMethod("CheckCollision",new[]{typeof(Rectangle),typeof(Rectangle).MakeByRefType(),typeof(AdvCollisionInfo).MakeByRefType()}),Hook("Query"),2);
                        hub.Patches.ReplaceCalls(plan.Additional,typeof(IBlockBehaviour).GetMethod(i==2?"AdditionalXCollisionCheck":"AdditionalYCollisionCheck"),Hook(i==2?"AdditionalX":"AdditionalY"),1);
                        plan.AdditionalValidity=MethodValidity.Watch(plan.Additional,"jkruntime.movement-observation");
                    }
                    // Motion arithmetic hooks are reviewed alongside these observations.
                    plan.Validity=MethodValidity.Watch(plan.Method,"jkruntime.movement-observation","jkruntime.motion-observation");
                }
                MotionObservation.RefreshNativeValidity();
                hub.Ready=true; hub.References=1; return new MovementObservationScope(hub);
            }
            catch { Release(hub); throw; }
        }
        internal static void Release(Hub hub)
        {
            if(hub.References>1) { hub.References--; return; }
            hub.Closing=true; hub.Patches.Dispose();
            foreach(var plan in hub.Plans.Values) { if(plan.Validity!=null)plan.Validity.Dispose(); if(plan.AdditionalValidity!=null)plan.AdditionalValidity.Dispose(); }
            hub.References=0; if(ReferenceEquals(shared,hub))shared=null;
            MotionObservation.RefreshNativeValidity();
        }
        internal static void RefreshMotionHooks()
        {
            var hub=shared; if(hub==null)return;
            foreach(var plan in hub.Plans.Values)
            {
                if(plan.Stage!=MovementStage.XMovement)continue;
                var value=MethodValidity.Watch(plan.Method,"jkruntime.movement-observation","jkruntime.motion-observation");
                if(plan.Validity!=null)plan.Validity.Dispose(); plan.Validity=value;
            }
        }
        private static void Begin0(BehaviourContext __0,out State __state) { Begin(0,__0,out __state); }
        private static void Begin1(BehaviourContext __0,out State __state) { Begin(1,__0,out __state); }
        private static void Begin2(BehaviourContext __0,out State __state) { Begin(2,__0,out __state); }
        private static void Begin3(BehaviourContext __0,out State __state) { Begin(3,__0,out __state); }
        private static void Begin4(BehaviourContext __0,out State __state) { Begin(4,__0,out __state); }
        private static void Begin5(BehaviourContext __0,out State __state) { Begin(5,__0,out __state); }
        private static void Begin6(BehaviourContext __0,out State __state) { Begin(6,__0,out __state); }
        private static void Begin(int stage,BehaviourContext __0,out State __state)
        {
            if(active.Track!=null)active.Reentrant=true;
            __state=new State {Previous=active}; active=default(Invocation);
            var hub=shared; Track track;
            if(hub==null || !hub.Bodies.TryGetValue(__0.BodyComp,out track) || track.Consumers==0)return;
            var plan=hub.Stages[stage]; var body=track.Body;
            __state.Capturing=true;
            active=new Invocation {Track=track,Plan=plan,Reentrant=__state.Previous.Track!=null,Sample=new MovementSample {Stage=plan.Stage,PositionBefore=body.Position,VelocityBefore=body.Velocity}};
        }
        private static void End(Exception __exception,State __state)
        {
            try
            {
                if(!__state.Capturing)return;
                var sample=active.Sample; var body=active.Track.Body;
                sample.PositionAfter=body.Position; sample.VelocityAfter=body.Velocity;
                sample.Completed=__exception==null; sample.Covered=sample.Completed && !active.Reentrant && active.Plan.Covered;
                active.Track.Publish(sample);
            }
            finally { active=__state.Previous; }
        }
        private static bool Query(ICollisionQuery query,Rectangle rect,out Rectangle overlap,out AdvCollisionInfo info)
        {
            if(active.Track!=null)active.Probe=rect;
            bool result=query.CheckCollision(rect,out overlap,out info);
            if(result)Accept(ContactPath.Native,null,rect);
            return result;
        }
        private static bool AdditionalX(IBlockBehaviour source,AdvCollisionInfo info,BehaviourContext context)
        { bool result=source.AdditionalXCollisionCheck(info,context); if(result && ReferenceEquals(active.Track==null?null:active.Track.Body,context.BodyComp))Accept(ContactPath.Additional,source.GetType(),active.Probe); return result; }
        private static bool AdditionalY(IBlockBehaviour source,AdvCollisionInfo info,BehaviourContext context)
        { bool result=source.AdditionalYCollisionCheck(info,context); if(result && ReferenceEquals(active.Track==null?null:active.Track.Body,context.BodyComp))Accept(ContactPath.Additional,source.GetType(),active.Probe); return result; }
        private static void Accept(ContactPath path,Type handler,Rectangle rect)
        {
            if(active.Track==null)return;
            var contact=active.Sample.Contact; contact.Path=path; contact.HandlerType=handler; contact.Probe=rect; contact.AcceptedQueries++;
            float velocity=active.Sample.Stage==MovementStage.XCollision?active.Track.Body.Velocity.X:active.Sample.VelocityBefore.Y;
            if(contact.AcceptedQueries==1)contact.Direction=velocity>0?1:-1; active.Sample.Contact=contact;
        }
        internal static object Diagnostics()
        { var hub=shared; return new {active=hub!=null,bodies=hub==null?0:hub.Bodies.Count,stages=hub==null?new object[0]:hub.Plans.Values.Select(p=>(object)new {stage=p.Stage.ToString(),covered=p.Covered}).ToArray()}; }
    }
}
