using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    public struct PresentationRequest
    {
        public readonly bool Sample, Draw;
        public PresentationRequest(bool sampleAt240Hz, bool drawAt240Hz) { Sample=sampleAt240Hz; Draw=drawAt240Hz; }
    }
    /// <summary>One native accumulator owner. Requests are sampled at Tick boundaries; fixed native simulation time is preserved independently of input and drawing cadence.</summary>
    public static class PresentationScheduling
    {
        public static readonly TimeSpan Interval = TimeSpan.FromTicks(41667);
        private static readonly FieldInfo Accumulated=typeof(Game).GetField("_accumulatedElapsedTime", OwnedPatches.Members);
        private static readonly FieldInfo Clock=typeof(Game).GetField("_gameTime", OwnedPatches.Members);
        private static readonly FieldInfo Lag=typeof(Game).GetField("_updateFrameLag", OwnedPatches.Members);
        private static readonly List<Client> clients=new List<Client>();
        private static Client[] snapshot=new Client[0];
        private static OwnedPatches patches, failedInstallation;
        private static Game owner;
        private static long remainder;
        private static int previousLag, simulationUpdates;
        private static bool previouslySlow, highRefresh, nativeDrawPending, dispatching, evaluating;
        public static float Alpha { get { return owner==null ? 1 : (float)(remainder/(double)owner.TargetElapsedTime.Ticks); } }
        public static bool IsActive(string id)
        { foreach (var client in snapshot) if (client.Id==id && client.Active && !client.Disposed) return true; return false; }
        public static bool IsHighRefresh(string id)
        { foreach (var client in snapshot) if (client.Id==id && client.Active && client.HighRefresh && !client.Disposed) return true; return false; }
        public sealed class Client : IDisposable
        {
            internal readonly string Id;
            internal readonly Func<PresentationRequest> Request;
            internal readonly Action<float> Sample;
            internal readonly Action Released;
            internal bool Disposed, Faulted, ReleasePending, Releasing;
            internal long RetryReleaseAt;
            public string LastError { get; internal set; }
            public bool Active { get; internal set; }
            public bool HighRefresh { get; internal set; }
            internal Client(string id, Func<PresentationRequest> request, Action<float> sample, Action released)
            { Id=id; Request=request; Sample=sample; Released=released; }
            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread();
                if (Releasing) { Disposed=true; return; }
                if (Disposed && !ReleasePending) return; Disposed=true;
                if (!dispatching && !evaluating)
                {
                    Deactivate(this);
                    if (ReleasePending) { RetryReleaseAt=0; TryRelease(this); }
                    if (!ReleasePending) clients.Remove(this); snapshot=clients.ToArray();
                    if (!clients.Any(c=>c.Active)) ReleaseClock();
                }
            }
        }
        public static Client Register(string id, Func<PresentationRequest> request, Action<float> sample = null, Action released = null)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(id);
            if (request==null) throw new ArgumentNullException("request");
            if (clients.Any(c=>c.Id==id)) throw new InvalidOperationException("Presentation request already registered: "+id);
            Install();
            var client=new Client(id,request,sample,released); clients.Add(client); snapshot=clients.ToArray(); return client;
        }
        private static MethodInfo Method(string name) { return typeof(PresentationScheduling).GetMethod(name, OwnedPatches.Members); }
        private static void Install()
        {
            if (patches!=null) return;
            if (failedInstallation!=null) { failedInstallation.Dispose(); failedInstallation=null; }
            ValidateContract();
            var group=new OwnedPatches("jk-runtime.presentation-clock");
            try
            {
                var tick=typeof(Game).GetMethod("Tick");
                group.Add(tick,prefix:Method("BeginTick"));
                group.ReplaceCalls(tick,typeof(Game).GetProperty("TargetElapsedTime").GetGetMethod(),Method("TickInterval"),7);
                group.ReplaceCalls(tick,typeof(Game).GetMethod("DoUpdate",OwnedPatches.Members),Method("DispatchUpdate"),2);
                group.ReplaceCalls(tick,typeof(Game).GetMethod("DoDraw",OwnedPatches.Members),Method("DispatchDraw"),1);
                patches=group;
            }
            catch (Exception failure)
            {
                try { group.Dispose(); } catch (Exception cleanup) { failedInstallation=group; throw new AggregateException("Presentation hooks and rollback failed",failure,cleanup); }
                throw;
            }
        }
        public static void ValidateContract()
        {
            if (Accumulated==null || Accumulated.FieldType!=typeof(TimeSpan) || Clock==null || Clock.FieldType!=typeof(GameTime)
                || Lag==null || Lag.FieldType!=typeof(int)) throw new NotSupportedException("Native presentation clock layout changed");
        }
        /// <summary>Observe requests at a native scheduler boundary. Exposed for hosts that explicitly drive the native loop; ordinary mods only register requests.</summary>
        public static void BeginTick(Game __instance)
        {
            bool supported=ReferenceEquals(__instance,Game1.instance) && __instance.IsFixedTimeStep && __instance.TargetElapsedTime>Interval;
            if (owner!=null && !ReferenceEquals(owner,__instance))
            { foreach (var client in snapshot) Deactivate(client); ReleaseClock(); }
            bool wanted=false; highRefresh=false; evaluating=true;
            try
            {
                foreach (var client in snapshot)
                {
                    TryRelease(client);
                    var request=default(PresentationRequest);
                    if (supported && !client.Disposed && !client.Faulted && !client.ReleasePending)
                        try { request=client.Request(); } catch (Exception error) { Fault(client, error); }
                    bool active=supported && !client.Disposed && !client.Faulted && !client.ReleasePending && (request.Sample || request.Draw);
                    if (client.Active && !active) Deactivate(client);
                    client.Active=active; client.HighRefresh=active && request.Draw;
                    wanted|=active; highRefresh|=client.HighRefresh;
                }
                if (clients.RemoveAll(c=>c.Disposed && !c.ReleasePending)!=0) snapshot=clients.ToArray();
            }
            finally { evaluating=false; }
            if (!wanted) { ReleaseClock(); return; }
            if (owner==null) { remainder=0; owner=__instance; }
            previousLag=(int)Lag.GetValue(owner); simulationUpdates=0;
            previouslySlow=((GameTime)Clock.GetValue(owner)).IsRunningSlowly;
        }
        private static void Deactivate(Client client)
        {
            if (!client.Active) return;
            client.Active=client.HighRefresh=false;
            client.ReleasePending=client.Released!=null;
            TryRelease(client);
        }
        private static void Fault(Client client, Exception error)
        {
            client.Faulted=true;
            string message=error.GetBaseException().Message;
            if (client.LastError!=message) Console.WriteLine("[JK Runtime] Presentation client "+client.Id+": "+message);
            client.LastError=message;
            Deactivate(client);
        }
        private static void TryRelease(Client client)
        {
            if (!client.ReleasePending || client.Releasing || Stopwatch.GetTimestamp()<client.RetryReleaseAt) return;
            client.Releasing=true;
            try { client.Released(); client.ReleasePending=false; client.RetryReleaseAt=0; }
            catch (Exception error)
            {
                client.RetryReleaseAt=Stopwatch.GetTimestamp()+Stopwatch.Frequency;
                Fault(client,error);
            }
            finally { client.Releasing=false; }
        }
        private static TimeSpan TickInterval(Game game) { return ReferenceEquals(owner,game) ? Interval : game.TargetElapsedTime; }
        private static void DispatchUpdate(Game game, GameTime time)
        {
            dispatching=true;
            try
            {
                if (ReferenceEquals(owner,game))
                {
                    float delta=(float)(time.ElapsedGameTime.Ticks/(double)game.TargetElapsedTime.Ticks/60);
                    foreach (var client in snapshot) if (client.Active && !client.Disposed && client.Sample!=null)
                        try { client.Sample(delta); } catch (Exception error) { Fault(client,error); }
                    remainder+=Math.Max(0,time.ElapsedGameTime.Ticks);
                    if (remainder<game.TargetElapsedTime.Ticks) return;
                    remainder-=game.TargetElapsedTime.Ticks; nativeDrawPending=true; simulationUpdates++;
                    time=new GameTime(TimeSpan.FromTicks(time.TotalGameTime.Ticks-remainder),game.TargetElapsedTime,time.IsRunningSlowly);
                }
                NativeFrameDispatch.Update(game,time);
            }
            finally { dispatching=false; }
        }
        private static void DispatchDraw(Game game, GameTime time)
        {
            dispatching=true;
            try
            {
                if (ReferenceEquals(owner,game))
                {
                    int lag=previousLag+Math.Max(0,simulationUpdates-1);
                    time.IsRunningSlowly=previouslySlow ? lag!=0 : lag>=5;
                    if (simulationUpdates==1 && lag>0) lag--;
                    Lag.SetValue(owner,lag);
                    bool draw=highRefresh || nativeDrawPending; nativeDrawPending=false;
                    if (!draw) return;
                }
                NativeFrameDispatch.Draw(game,time);
            }
            finally { dispatching=false; }
        }
        private static void ReleaseClock()
        {
            if (owner==null) return;
            var time=(GameTime)Clock.GetValue(owner); time.ElapsedGameTime=owner.TargetElapsedTime;
            if (remainder!=0)
            {
                time.TotalGameTime-=TimeSpan.FromTicks(remainder);
                Accumulated.SetValue(owner,(TimeSpan)Accumulated.GetValue(owner)+TimeSpan.FromTicks(remainder));
            }
            remainder=0; owner=null; nativeDrawPending=false;
        }
    }
}
