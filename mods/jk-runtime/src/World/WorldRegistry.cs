using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace JKRuntime.World
{
    public sealed class WorldStateInfo
    {
        public string Id {get;internal set;}
        public ulong Schema {get;internal set;}
        public string Coverage {get;internal set;}
    }

    /// <summary>Transport-neutral bounded world snapshots with transactional restoration</summary>
    public sealed class WorldRegistry
    {
        private sealed class State
        {internal string Id,Coverage;internal ulong Schema;internal Func<byte[]> Capture;internal Func<byte[],Action> Prepare;}
        private readonly List<State> states=new List<State>();
        private readonly Dictionary<string,State> byId=new Dictionary<string,State>(StringComparer.Ordinal);
        private State[] ordered=new State[0];
        private long generation;
        private bool busy,poisoned;
        public const int MaximumBytes=48000;
        public static readonly WorldRegistry Shared=new WorldRegistry();
        internal static void ValidateId(string id)
        {if(string.IsNullOrEmpty(id)||Encoding.UTF8.GetByteCount(id)>160||id.Any(char.IsControl))throw new ArgumentException("Invalid world state ID");}
        private void Check()
        {RuntimeApi.Kernel.CheckThread();if(busy||poisoned)throw new InvalidOperationException("World transaction is busy or rollback failed");}
        public WorldStateInfo[] Inspect()
        {RuntimeApi.Kernel.CheckThread();return ordered.Select(x=>new WorldStateInfo{Id=x.Id,Schema=x.Schema,Coverage=x.Coverage}).ToArray();}
        public IDisposable Register(string id,ulong schema,Func<byte[]> capture,Func<byte[],Action> prepare)
        {return Register(id,schema,capture,prepare,"explicit byte contract; execution coverage is separate");}
        private IDisposable Register(string id,ulong schema,Func<byte[]> capture,Func<byte[],Action> prepare,string coverage)
        {
            Check();CheckMembership();ValidateId(id);
            if(capture==null||prepare==null||byId.ContainsKey(id))throw new ArgumentException("Invalid or duplicate world state");
            if(states.Count>=64)throw new InvalidOperationException("World state limit");
            var value=new State{Id=id,Schema=schema,Capture=capture,Prepare=prepare,Coverage=coverage};states.Add(value);byId.Add(id,value);Changed();
            return new ActionLease(delegate{Check();CheckMembership();states.Remove(value);byId.Remove(id);Changed();});
        }
        private void Changed() {ordered=states.OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray();generation++;}
        private void CheckMembership()
        {if(ReferenceEquals(this,Shared)&&WorldControl.Owner!=null)throw new InvalidOperationException("Release world control before changing shared registrations");}
        public IDisposable Attach(string id,object target)
        {
            if(target==null)throw new ArgumentNullException("target");
            var codec=new WorldStateCodec(target.GetType(),true);
            return Register(id,codec.Schema,()=>codec.Capture(target),bytes=>{var state=codec.Decode(bytes);return ()=>codec.Apply(target,state);},"declared fields; execution coverage is separate");
        }
        public IDisposable Bind<T>(string id,Func<T> capture,Action<T> restore,Action<T> validate) where T : class
        {
            if(capture==null||restore==null||validate==null)throw new ArgumentNullException("World binding callbacks");
            var codec=new WorldStateCodec(typeof(T),true);
            return Register(id,codec.Schema,()=>codec.Capture(capture()),bytes=>{
                var value=(T)codec.Decode(bytes);if(value==null)throw new InvalidDataException("Missing declared world state");validate(value);
                return ()=>restore(value);
            },"declared snapshot; execution coverage is separate");
        }
        public byte[] Capture()
        {
            Check();busy=true;
            try {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
                writer.Write((byte)states.Count);
                foreach(var state in ordered) {
                    byte[] data=state.Capture();if(data==null||data.Length>MaximumBytes)throw new InvalidDataException("World state too large: "+state.Id);
                    writer.Write(state.Id);writer.Write(state.Schema);writer.Write(data.Length);writer.Write(data);
                    if(stream.Length>MaximumBytes)throw new InvalidDataException("World snapshot exceeds 48 KB");
                }
                return stream.ToArray();
            }}finally{busy=false;}
        }
        public Action Prepare(byte[] bytes)
        {
            Check();if(bytes==null||bytes.Length<1||bytes.Length>MaximumBytes)throw new InvalidDataException("Invalid world snapshot length");
            busy=true;
            try {
                long expected=generation;var entries=new List<State>();var actions=new List<Action>();
                using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream)) {
                    int count=reader.ReadByte();if(count!=states.Count)throw new InvalidDataException("Different world state sets");
                    var seen=new HashSet<string>();
                    for(int i=0;i<count;i++) {
                        string id=reader.ReadString();ulong schema=reader.ReadUInt64();int length=reader.ReadInt32();State state;byId.TryGetValue(id,out state);
                        if(!seen.Add(id)||state==null||state.Schema!=schema||length<0||length>stream.Length-stream.Position)throw new InvalidDataException("Incompatible world state: "+id);
                        entries.Add(state);actions.Add(state.Prepare(reader.ReadBytes(length))??delegate{});
                    }
                    if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing world data");
                }
                return delegate {
                    Check();if(generation!=expected)throw new InvalidOperationException("World registrations changed before application");
                    busy=true;
                    try {
                        // capture and prepare every undo before the first mutation
                        var undo=entries.Select(s=>s.Prepare(s.Capture())??delegate{}).ToArray();int applied=-1;
                        try {for(int i=0;i<actions.Count;i++){applied=i;actions[i]();}}
                        catch(Exception failure) {
                            var errors=new List<Exception>{failure};
                            for(int i=applied;i>=0;i--)try{undo[i]();}catch(Exception error){errors.Add(error);}
                            if(errors.Count>1)poisoned=true;
                            throw new AggregateException("World application failed; rollback "+(poisoned?"incomplete":"completed"),errors);
                        }
                    }finally{busy=false;}
                };
            }finally{busy=false;}
        }
        public void Apply(byte[] bytes) {Prepare(bytes)();}
        public IDisposable Preserve()
        {var restore=Prepare(Capture());return new ActionLease(restore);}
    }
}
