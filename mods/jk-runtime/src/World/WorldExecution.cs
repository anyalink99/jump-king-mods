using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace JKRuntime.World
{
    public enum WorldRole { Local, Host, Replica, Playback }
    public enum WorldPhase { Update, Contact, Input, Restore, Presentation, Personal }

    /// <summary>Immutable execution identity. actor 0 means shared world work, never an implicit local player</summary>
    public sealed class WorldFrame
    {
        public string World { get; private set; }
        public ulong Attempt { get; private set; }
        public long Tick { get; private set; }
        public ulong Actor { get; private set; }
        public WorldRole Role { get; private set; }
        public WorldPhase Phase { get; private set; }
        public WorldFrame(string world, ulong attempt, long tick, ulong actor, WorldRole role, WorldPhase phase)
        {
            if(string.IsNullOrEmpty(world) || world.Length>256 || attempt==0 || tick<0 || !Enum.IsDefined(typeof(WorldRole),role) || !Enum.IsDefined(typeof(WorldPhase),phase))
                throw new ArgumentException("Invalid world execution identity");
            World=world;Attempt=attempt;Tick=tick;Actor=actor;Role=role;Phase=phase;
        }
    }

    public sealed class WorldEffect
    {
        public long Sequence { get; internal set; }
        public string Kind { get; internal set; }
        public WorldFrame Frame { get; internal set; }
        private byte[] payload;
        public byte[] Payload { get { return (byte[])payload.Clone(); } }
        internal WorldEffect(string kind,WorldFrame frame,byte[] data) {Kind=kind;Frame=frame;payload=(byte[])data.Clone();}
    }

    /// <summary>Scoped execution and bounded effects shared by networking, playback and native hooks. no background polling</summary>
    public static class WorldExecution
    {
        private sealed class Entry : IDisposable
        {
            internal Entry Prior;internal WorldFrame Frame;internal int Thread;
            private bool released;
            public void Dispose()
            {
                if(released)return;
                if(Thread!=System.Threading.Thread.CurrentThread.ManagedThreadId || !ReferenceEquals(current,this))
                    throw new InvalidOperationException("World contexts must leave in reverse order on their owning thread");
                current=Prior;released=true;
            }
        }
        [ThreadStatic] private static Entry current;
        private static readonly Queue<WorldEffect> effects=new Queue<WorldEffect>();
        private static long sequence;
        public static WorldFrame Current {get{return current==null ? null : current.Frame;}}
        public static bool CanSimulate {get{return Current==null ? !WorldControl.Following : Current.Phase!=WorldPhase.Restore && (Current.Role==WorldRole.Local || Current.Role==WorldRole.Host);}}
        public static bool CanPersist {get{return Current==null ? !WorldControl.Following : (Current.Phase!=WorldPhase.Restore && (Current.Role==WorldRole.Local || Current.Role==WorldRole.Host || Current.Role==WorldRole.Replica && Current.Phase==WorldPhase.Personal));}}
        public static IDisposable Enter(WorldFrame frame)
        {
            RuntimeApi.Kernel.CheckThread();if(frame==null)throw new ArgumentNullException("frame");
            var value=new Entry{Prior=current,Frame=frame,Thread=Thread.CurrentThread.ManagedThreadId};current=value;return value;
        }
        public static void Run(WorldFrame frame,Action action)
        {if(action==null)throw new ArgumentNullException("action");using(Enter(frame))action();}
        public static bool Emit(string kind,byte[] payload,Action present)
        {
            RuntimeApi.Kernel.CheckThread();
            if(string.IsNullOrEmpty(kind)||kind.Length>160||payload==null||payload.Length>4096)throw new ArgumentException("Invalid world effect");
            var frame=Current;
            // restoring state must never replay an irreversible effect
            if(frame==null ? WorldControl.Following : (frame.Phase==WorldPhase.Restore || frame.Role==WorldRole.Replica || frame.Role==WorldRole.Playback))return false;
            if(sequence==long.MaxValue)throw new InvalidOperationException("World effect sequence exhausted");
            effects.Enqueue(new WorldEffect(kind,frame,payload){Sequence=++sequence});while(effects.Count>256)effects.Dequeue();
            if(present!=null)present();return true;
        }
        public static WorldEffect[] ReadEffects(long after)
        {RuntimeApi.Kernel.CheckThread();return effects.Where(e=>e.Sequence>after).ToArray();}
        public static long EffectSequence {get{RuntimeApi.Kernel.CheckThread();return sequence;}}
        internal static void ClearEffects() {RuntimeApi.Kernel.CheckThread();effects.Clear();}
    }

    /// <summary>Names are authored IDs, not object hashes or collection indices</summary>
    public sealed class WorldObjects
    {
        private readonly Dictionary<string,object> values=new Dictionary<string,object>(StringComparer.Ordinal);
        public IDisposable Add(string id,object value)
        {
            RuntimeApi.Kernel.CheckThread();WorldRegistry.ValidateId(id);
            if(value==null)throw new ArgumentNullException("value");if(values.Count>=4096)throw new InvalidOperationException("World object limit");
            values.Add(id,value);return new ActionLease(delegate{RuntimeApi.Kernel.CheckThread();values.Remove(id);});
        }
        public object Find(string id) {RuntimeApi.Kernel.CheckThread();object value;return values.TryGetValue(id,out value)?value:null;}
        public string[] Ids {get{RuntimeApi.Kernel.CheckThread();return values.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray();}}
    }
}
