using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime.World
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class WorldMechanicAttribute : Attribute { }

    public sealed class WorldActor
    {
        public ulong Id {get;private set;}
        public ulong Attempt {get;private set;}
        public Vector2 Position {get;private set;}
        public Vector2 Velocity {get;private set;}
        public bool Grounded {get;private set;}
        public int Screen {get{return Math.Max(1,-(int)Math.Floor((Position.Y+Height/2f)/360f)+1);}}
        public WorldActor(ulong id,ulong attempt,Vector2 position,Vector2 velocity,bool grounded)
        {
            if(id==0||attempt==0||!Finite(position)||!Finite(velocity)||Math.Abs(position.X)>10000000||Math.Abs(position.Y)>10000000)throw new ArgumentException("Invalid world actor");
            Id=id;Attempt=attempt;Position=position;Velocity=velocity;Grounded=grounded;Width=18;Height=26;
        }
        public int Width {get;private set;}
        public int Height {get;private set;}
        public ulong Sequence {get;private set;}
        public ulong Jump {get;private set;}
        public bool JumpPressed {get;private set;}
        public bool SwitchPressed {get;private set;}
        public WorldActor(ulong id,ulong attempt,ulong sequence,ulong jump,Vector2 position,Vector2 velocity,bool grounded,int width,int height,bool jumpPressed,bool switchPressed)
            : this(id,attempt,position,velocity,grounded)
        {
            if(width<1||width>256||height<1||height>256)throw new ArgumentException("Invalid actor dimensions");
            Width=width;Height=height;Sequence=sequence;Jump=jump;JumpPressed=jumpPressed;SwitchPressed=switchPressed;
        }
        private static bool Finite(Vector2 v) {return !float.IsNaN(v.X)&&!float.IsInfinity(v.X)&&!float.IsNaN(v.Y)&&!float.IsInfinity(v.Y);}
    }

    /// <summary>Exclusive world authority. releasing a receiver restores its pre-session state</summary>
    public sealed class WorldControl : IDisposable
    {
        private static WorldControl active;
        private readonly IDisposable preserved;
        private WorldActor[] actors=new WorldActor[0];
        private readonly string owner,world;
        private readonly ulong attempt;
        private long tick;
        private ulong localActor;
        private byte[] canonical;
        private double time;
        private bool closed;
        public WorldRole Role {get;private set;}
        public static WorldRole CurrentRole {get{return active==null?WorldRole.Local:active.Role;}}
        public static double Time {get{return active==null?0:active.time;}}
        public static bool Following {get{return CurrentRole==WorldRole.Replica||CurrentRole==WorldRole.Playback;}}
        public static IDisposable EnterActor(ulong actor,WorldPhase phase) {return active==null?null:active.Enter(actor,phase);}
        public void SetTime(double seconds) {Check();if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentException("Invalid world time");time=seconds;}
        public static string Owner {get{return active==null?null:active.owner;}}
        public static ulong LocalActor {get{return active==null?0:active.localActor;}}
        public static WorldActor[] Actors {get{RuntimeApi.Kernel.CheckThread();return (WorldActor[])ActorSamples.Clone();}}
        internal static WorldActor[] ActorSamples {get{return active==null?emptyActors:active.actors;}}
        private static readonly WorldActor[] emptyActors=new WorldActor[0];
        public static WorldControl Begin(string owner,string world,ulong attempt,WorldRole role)
        {
            RuntimeApi.Kernel.CheckThread();WorldRegistry.ValidateId(owner);
            if(WorldObservation.BoundaryStatus!=null&&WorldObservation.BoundaryStatus.StartsWith("unavailable:",StringComparison.Ordinal))throw new InvalidOperationException("World boundaries "+WorldObservation.BoundaryStatus);
            if(active!=null)throw new InvalidOperationException("World execution already owned by "+active.owner);
            var value=new WorldControl(owner,world,attempt,role);active=value;return value;
        }
        private WorldControl(string source,string identity,ulong epoch,WorldRole role)
        {
            new WorldFrame(identity,epoch,0,0,role,WorldPhase.Update);
            owner=source;world=identity;attempt=epoch;Role=role;
            if(role==WorldRole.Replica||role==WorldRole.Playback)preserved=WorldRegistry.Shared.Preserve();
        }
        private void Check() {RuntimeApi.Kernel.CheckThread();if(closed||active!=this)throw new ObjectDisposedException("WorldControl");}
        public void SetLocalActor(ulong actor) {Check();localActor=actor;}
        internal static IDisposable EnterLocal(WorldPhase phase) {return active==null?null:active.Enter(active.localActor,phase);}
        public void SetActors(IEnumerable<WorldActor> values)
        {
            Check();var next=(values??new WorldActor[0]).ToArray();
            if(next.Length>64||next.Any(x=>x==null)||next.Select(x=>x.Id).Distinct().Count()!=next.Length)throw new ArgumentException("Invalid world actor set");
            actors=next.OrderBy(x=>x.Id).ToArray();
        }
        public void Receive(byte[] state)
        {
            Check();if(Role!=WorldRole.Replica&&Role!=WorldRole.Playback)throw new InvalidOperationException("This world role cannot receive state");
            using(Enter(0,WorldPhase.Restore))WorldRegistry.Shared.Apply(state);
            canonical=(byte[])state.Clone();
        }
        public IDisposable Enter(ulong actor,WorldPhase phase)
        {Check();return WorldExecution.Enter(new WorldFrame(world,attempt,tick,actor,Role,phase));}
        internal static IDisposable BeforeUpdate()
        {
            if(active==null)return null;
            active.Check();if(active.tick==long.MaxValue)throw new InvalidOperationException("World tick exhausted");active.tick++;
            return active.Enter(0,WorldPhase.Update);
        }
        public static void Maintain()
        {if(active!=null&&active.canonical!=null)using(active.Enter(0,WorldPhase.Restore))WorldRegistry.Shared.Apply(active.canonical);}
        public void Dispose()
        {
            RuntimeApi.Kernel.CheckThread();if(closed)return;Check();
            if(preserved!=null)using(Enter(0,WorldPhase.Restore))preserved.Dispose();
            NativeWorldState.Reset();NativeWorldInteractions.ResetReceived();SwitchBlocksWorld.Release();
            actors=emptyActors;canonical=null;active=null;closed=true;
        }
        internal static void EndAttempt() {if(active!=null)active.Dispose();WorldExecution.ClearEffects();}
    }
}
