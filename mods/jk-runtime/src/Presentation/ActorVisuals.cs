using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.Presentation
{
    /// <summary>A visual-only instance. Compose may prepare owned art, but must never apply gameplay or change shared outfit sprites.</summary>
    public interface IActorVisual : IDisposable
    {
        Sprite Compose(Sprite outfit, Sprite chargeOutfit, byte[] state);
    }
    /// <summary>Consumer clock and placement for local visual simulation. Doesn't describe gameplay authority.</summary>
    public struct ActorVisualFrame
    {
        public double Time;
        public Vector2 WorldAnchor, Velocity;
        public SpriteEffects Facing;
        public bool Cut, Paused, EffectsVisible;
        public ActorVisualFrame(double time,Vector2 anchor,Vector2 velocity,SpriteEffects facing,bool cut=false,bool paused=false,bool effectsVisible=true)
        {Time=time;WorldAnchor=anchor;Velocity=velocity;Facing=facing;Cut=cut;Paused=paused;EffectsVisible=effectsVisible;}
    }
    /// <summary>Optional client-side effect clock. Advance runs during Update, never during Draw.</summary>
    public interface IActorVisualAnimation
    {
        void Advance(ActorVisualFrame frame,byte[] state);
    }

    /// <summary>Versioned, bounded player visuals shared by recordings and remote actors. Game-thread API.</summary>
    public static class ActorVisuals
    {
        public const int MaximumBytes = 2048;
        public const int MaximumProviders = 8;
        public const int MaximumPayload = 256;
        private static readonly Dictionary<string,Provider> providers = new Dictionary<string,Provider>(StringComparer.Ordinal);
        private static Provider[] ordered = new Provider[0];
        private static readonly byte[] Empty=new byte[0];
        private static PlayerEntity cachedPlayer;
        private static byte[] cachedState;
        private static byte[] lastSerialized=Empty;
        private static long generation;
        /// <summary>Invalidate the borrowed snapshot after a live visual changes. Does no capture or serialization.</summary>
        public static void Changed(PlayerEntity player)
        {RuntimeApi.Kernel.CheckThread();if(ReferenceEquals(cachedPlayer,player))cachedState=null;}
        internal sealed class Provider
        {
            internal string Id;
            internal int Version, Order;
            internal VisualPhase Phase;
            internal Func<PlayerEntity,byte[]> Capture;
            internal Func<IActorVisual> Create;
        }
        internal sealed class State
        {
            internal string Id;
            internal int Version;
            internal byte[] Data;
        }
        public static IDisposable Register(string id,int version,VisualPhase phase,
            Func<PlayerEntity,byte[]> capture,Func<IActorVisual> create,int order=0)
        {
            RuntimeApi.Kernel.CheckThread();ModuleDefinition.ValidId(id);
            if(id.Length>64||version<1||version>65535||capture==null||create==null)
                throw new ArgumentException("Invalid actor visual provider");
            if(providers.ContainsKey(id)||providers.Count>=MaximumProviders)throw new InvalidOperationException("Actor visual provider limit or duplicate: "+id);
            var provider=new Provider {Id=id,Version=version,Phase=phase,Order=order,Capture=capture,Create=create};
            providers.Add(id,provider);Sort();
            return new ActionLease(()=>{providers.Remove(id);Sort();});
        }
        private static void Sort()
        {ordered=providers.Values.OrderBy(p=>p.Phase).ThenBy(p=>p.Order).ThenBy(p=>p.Id,StringComparer.Ordinal).ToArray();cachedState=null;cachedPlayer=null;lastSerialized=Empty;generation++;}
        public static byte[] Capture(PlayerEntity player)
        {
            RuntimeApi.Kernel.CheckThread();if(player==null||ordered.Length==0)return Empty;
            if(ReferenceEquals(cachedPlayer,player)&&cachedState!=null)return cachedState;
            cachedPlayer=player;
            var states=new List<State>();int size=2;
            foreach(var provider in ordered)
            {
                try {
                    var data=provider.Capture(player);if(data==null||data.Length==0)continue;
                    if(data.Length>MaximumPayload)throw new InvalidDataException("Actor visual payload exceeds 256 bytes");
                    if(size+provider.Id.Length+5+data.Length>MaximumBytes)throw new InvalidDataException("Actor visual snapshot exceeds 2048 bytes");
                    size+=provider.Id.Length+5+data.Length;
                    states.Add(new State {Id=provider.Id,Version=provider.Version,Data=data});
                }catch(Exception error){RuntimeJournal.Record(provider.Id,"actor-visual.capture",error.GetBaseException().Message);}
            }
            if(states.Count==0)return cachedState=Empty;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8))
            {
                writer.Write((byte)1);writer.Write((byte)states.Count);
                foreach(var state in states){writer.Write(state.Id);writer.Write((ushort)state.Version);writer.Write((ushort)state.Data.Length);writer.Write(state.Data);}
                if(stream.Length>MaximumBytes)throw new InvalidDataException("Actor visual snapshot exceeds 2048 bytes");
                var bytes=stream.ToArray();bool same=bytes.Length==lastSerialized.Length;
                if(same)for(int i=0;i<bytes.Length;i++)if(bytes[i]!=lastSerialized[i]){same=false;break;}
                return cachedState=lastSerialized=same?lastSerialized:bytes;
            }
        }
        internal static State[] Decode(byte[] bytes)
        {
            if(bytes==null||bytes.Length==0)return new State[0];
            if(bytes.Length>MaximumBytes)throw new InvalidDataException("Actor visual snapshot exceeds 2048 bytes");
            using(var reader=new BinaryReader(new MemoryStream(bytes),Encoding.UTF8))
            {
                if(reader.ReadByte()!=1)throw new InvalidDataException("Unknown actor visual snapshot version");
                int count=reader.ReadByte();if(count>MaximumProviders)throw new InvalidDataException("Too many actor visuals");
                var states=new State[count];var ids=new HashSet<string>(StringComparer.Ordinal);
                for(int i=0;i<count;i++)
                {
                    // ids use a single-byte length so malformed strings can't allocate beyond the envelope
                    int length=reader.ReadByte();if(length<1||length>64)throw new InvalidDataException("Invalid actor visual ID");
                    byte[] name=reader.ReadBytes(length);if(name.Length!=length||name.Any(b=>b>127))throw new InvalidDataException("Invalid actor visual ID");
                    string id=Encoding.ASCII.GetString(name);try{ModuleDefinition.ValidId(id);}catch(ArgumentException){throw new InvalidDataException("Invalid actor visual ID");}
                    int version=reader.ReadUInt16(),size=reader.ReadUInt16();
                    if(!ids.Add(id)||version<1||size<1||size>MaximumPayload)throw new InvalidDataException("Invalid actor visual payload");
                    byte[] data=reader.ReadBytes(size);if(data.Length!=size)throw new EndOfStreamException();
                    states[i]=new State {Id=id,Version=version,Data=data};
                }
                if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new InvalidDataException("Trailing actor visual data");
                return states;
            }
        }
        public static bool Valid(byte[] bytes)
        {try{Decode(bytes);return true;}catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}}
        public static Actor Create() {RuntimeApi.Kernel.CheckThread();return new Actor();}

        public sealed class Actor : IDisposable
        {
            private sealed class Instance {internal Provider Provider;internal IActorVisual Visual;internal IDisposable Lifetime;internal bool Failed;internal byte[] Data;internal Sprite Source,Charge,Result;internal long Revision=-1;}
            private readonly Dictionary<string,Instance> instances=new Dictionary<string,Instance>(StringComparer.Ordinal);
            private byte[] previous;
            private State[] states=new State[0];
            private readonly List<string> retired=new List<string>(MaximumProviders);
            private Sprite lastOutfit,lastCharge,lastResult;
            private long lastGeneration=-1,lastRevision=-1;
            private double lastTime=double.NaN;
            public Sprite Present(byte[] snapshot,Sprite outfit,Sprite chargeOutfit,ActorVisualFrame? clock=null)
            {
                RuntimeApi.Kernel.CheckThread();
                if(ReferenceEquals(previous,snapshot)&&ReferenceEquals(lastOutfit,outfit)&&ReferenceEquals(lastCharge,chargeOutfit)
                    &&lastGeneration==generation&&lastRevision==PlayerAppearance.Revision&&(!clock.HasValue||!clock.Value.Cut&&clock.Value.Time==lastTime))return lastResult;
                if(!ReferenceEquals(previous,snapshot)){states=Decode(snapshot);previous=snapshot;}
                retired.Clear();
                foreach(var pair in instances)
                {
                    Provider provider;var instance=pair.Value;bool present=false;
                    if(providers.TryGetValue(pair.Key,out provider)&&ReferenceEquals(provider,instance.Provider))
                        foreach(var state in states)if(state.Id==pair.Key&&state.Version==provider.Version){present=true;break;}
                    if(!present)retired.Add(pair.Key);
                }
                foreach(var id in retired)Release(id);
                Sprite result=outfit;bool form=false;
                foreach(var provider in ordered)
                {
                    State state=null;foreach(var value in states)if(value.Id==provider.Id&&value.Version==provider.Version){state=value;break;}
                    if(state==null)continue;
                    if(provider.Phase==VisualPhase.Form&&form)continue;
                    Instance instance;
                    if(!instances.TryGetValue(provider.Id,out instance))
                    {instance=new Instance {Provider=provider};instances.Add(provider.Id,instance);}
                    if(instance.Failed)continue;
                    try {
                        if(instance.Visual==null){instance.Visual=provider.Create();if(instance.Visual!=null)instance.Lifetime=RuntimeResources.Track(provider.Id,"actor-visual",instance.Visual);}
                        if(instance.Visual==null)throw new InvalidOperationException("Actor visual factory returned null");
                        var animation=instance.Visual as IActorVisualAnimation;
                        if(animation!=null&&clock.HasValue)animation.Advance(clock.Value,state.Data);
                        if(instance.Data==null||!Same(instance.Data,state.Data)||!ReferenceEquals(instance.Source,result)
                            ||!ReferenceEquals(instance.Charge,chargeOutfit)||instance.Revision!=PlayerAppearance.Revision||clock.HasValue&&clock.Value.Cut){
                            instance.Source=result;instance.Charge=chargeOutfit;instance.Data=state.Data;instance.Revision=PlayerAppearance.Revision;
                            instance.Result=instance.Visual.Compose(result,chargeOutfit,state.Data)??result;
                        }
                        result=instance.Result;
                        if(provider.Phase==VisualPhase.Form)form=true;
                    }catch(Exception error){instance.Failed=true;Close(instance);
                        RuntimeJournal.Record(provider.Id,"actor-visual.playback",error.GetBaseException().Message);}
                }
                lastOutfit=outfit;lastCharge=chargeOutfit;lastGeneration=generation;lastRevision=PlayerAppearance.Revision;if(clock.HasValue)lastTime=clock.Value.Time;return lastResult=result;
            }
            private static bool Same(byte[] a,byte[] b){if(ReferenceEquals(a,b))return true;if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
            private void Release(string id)
            {var instance=instances[id];instances.Remove(id);Close(instance);}
            private static void Close(Instance instance)
            {
                try{if(instance.Lifetime!=null)instance.Lifetime.Dispose();}
                catch(Exception error){RuntimeJournal.Record(instance.Provider.Id,"actor-visual.cleanup",error.GetBaseException().Message);}
                finally{instance.Visual=null;instance.Lifetime=null;}
            }
            public void Dispose()
            {RuntimeApi.Kernel.CheckThread();foreach(var id in instances.Keys.ToArray())Release(id);previous=null;states=new State[0];lastGeneration=-1;lastOutfit=lastCharge=lastResult=null;}
        }

        public static Sprite Layers(params Sprite[] sprites) {return new LayerSprite(sprites.Where(s=>s!=null).ToArray());}
        private sealed class LayerSprite : Sprite
        {
            public Sprite[] Sprites {get;private set;}
            internal LayerSprite(Sprite[] sprites){Sprites=sprites;}
            public override void Draw(Vector2 anchor,SpriteEffects facing=SpriteEffects.None)
            {foreach(var sprite in Sprites)sprite.Draw(anchor,facing);}
        }
        /// <summary>Draws virtual sprites with actor tint and restores borrowed colors afterwards.</summary>
        public static void Draw(Sprite sprite,Vector2 anchor,SpriteEffects facing,Color tint)
        {Draw(sprite,anchor,facing,tint,0);}
        private static void Draw(Sprite sprite,Vector2 anchor,SpriteEffects facing,Color tint,int depth)
        {
            if(sprite==null||depth>16)return;
            var layers=PlayerAppearance.Layers(sprite);
            if(layers.Length!=1||!ReferenceEquals(layers[0],sprite))
            {foreach(var layer in layers)Draw(layer,anchor,facing,tint,depth+1);return;}
            Color original=sprite.GetColor();
            try {sprite.SetColor(new Color(original.ToVector4()*tint.ToVector4()));sprite.Draw(anchor,facing);}
            finally {sprite.SetColor(original);}
        }
    }
}
