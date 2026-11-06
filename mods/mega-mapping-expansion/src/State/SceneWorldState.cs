using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using JKRuntime.World;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class SceneWorldPacket
    {
        [WorldField]public string Layout;
        [WorldField]public string[] Parts;
    }
    internal sealed class SceneWorldData
    {
        internal GraphWorldData Graph;
        internal Dictionary<string,SceneValue> Variables;
        internal Dictionary<string,RuleProgress> Rules;
        internal EffectInstance[] Effects;
        internal SceneEventSample[] Events,Causes;
        internal WorldTreeState[] Trees;
        internal double Gameplay,Presentation;
        internal long Serial;
        internal Dictionary<string,double[]> Fields;
        internal SensorSnapshot Sensors;
        internal SignalSample[] Observations;
    }
    internal sealed partial class SceneProgram
    {
        private string worldIdentity;
        private string[] worldOutputs;
        private string assetIdentity="";
        private VariableData[] worldVariables;
        private KeyValuePair<string,RuleProgress>[] worldRules;
        private void PrepareWorldLayout()
        {
            worldVariables=variableDefinitions.Values.OrderBy(v=>v.Id,StringComparer.Ordinal).ToArray();worldRules=ruleProgress.OrderBy(p=>p.Key,StringComparer.Ordinal).ToArray();
            Graph.PrepareWorld(WorldOutputs);Signals.CaptureObservations();string prepared=WorldIdentity;
        }
        internal void AssetIdentity(string identity){assetIdentity=identity;worldIdentity=null;string prepared=WorldIdentity;}
        internal Action WorldRestored;
        internal string WorldIdentity
        {
            get
            {
                if(worldIdentity!=null)return worldIdentity;
                string xml=SceneDocument.Xml(Definition);
                using(var hash=System.Security.Cryptography.SHA256.Create())worldIdentity=Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes("MME-CONSTRUCTION-1\n"+SemanticIdentity.Value+"\n"+assetIdentity+"\n"+xml)));
                return worldIdentity;
            }
        }
        private string[] WorldOutputs
        {get{return worldOutputs??(worldOutputs=Definition.Bindings.Select(b=>b.Source).Concat(Definition.Effects.SelectMany(e=>e.Values).Where(v=>!string.IsNullOrEmpty(v.Source)).Select(v=>v.Source)).Concat(Definition.Exports.Where(e=>e.Role=="node").Select(e=>e.Target)).Distinct(StringComparer.Ordinal).ToArray());}}
        internal IDisposable BindWorld(){return WorldRegistry.Shared.Bind<SceneWorldPacket>("mega.mapping.scene",CaptureWorld,RestoreWorld,ValidateWorld);}
        internal SceneWorldPacket CaptureWorld()
        {
            byte[] raw;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(2);writer.Write(gameplayTime);writer.Write(presentationTime);writer.Write(effectSerial);Graph.WriteWorld(writer,WorldOutputs);
                var variables=worldVariables;writer.Write((ushort)variables.Length);foreach(var variable in variables)WorldValues.Write(writer,ReadVariable(variable.Id));
                var rules=worldRules;writer.Write((ushort)rules.Length);
                foreach(var rule in rules){writer.Write(rule.Value.Fired);writer.Write(rule.Value.Faulted);writer.Write(rule.Value.Last);}
                writer.Write((ushort)activeEffects.Count);foreach(var effect in activeEffects){writer.Write(effect.Id);WorldValues.Text(writer,effect.Owner,256);WorldValues.Text(writer,effect.Definition,128);writer.Write(effect.Started);}
                var events=Signals.CaptureEvents();writer.Write((ushort)events.Length);foreach(var sample in events)WorldValues.Event(writer,sample);
                writer.Write((ushort)trees.Length);
                foreach(var tree in trees)
                {
                    writer.Write((byte)tree.State.Result);writer.Write(tree.State.Fired);WorldValues.Event(writer,tree.Cause);
                    var nodes=tree.State.Nodes;
                    writer.Write((ushort)(nodes==null?0:nodes.Length));
                    if(nodes==null)continue;
                    for(int i=0;i<nodes.Length;i++)
                    {
                        var node=nodes[i];writer.Write((byte)node.Result);byte flags=(byte)((node.Cursor!=0?1:0)|(node.Iterations!=0?2:0)|(node.Started!=0?4:0)|(node.Effect!=0?8:0)|(node.Armed?16:0)|(node.Signalled?32:0));writer.Write(flags);
                        if((flags&1)!=0)writer.Write(node.Cursor);if((flags&2)!=0)writer.Write(node.Iterations);if((flags&4)!=0)writer.Write(node.Started);if((flags&8)!=0)writer.Write(node.Effect);
                    }
                }
                Fields.WriteWorld(writer);Sensors.WriteWorld(writer);Signals.WriteObservations(writer);writer.Flush();if(stream.Length>4*1024*1024)throw new InvalidDataException("Scene world state exceeds 4 MiB before compression");raw=stream.ToArray();
            }
            byte[] compressed;using(var stream=new MemoryStream()){using(var zip=new DeflateStream(stream,CompressionMode.Compress,true))zip.Write(raw,0,raw.Length);compressed=stream.ToArray();}
            if(compressed.Length>24*1024)throw new InvalidDataException("MME shared state exceeds 24 KiB compressed; reduce shared inputs, outputs, queued events or active control state");
            string payload=Convert.ToBase64String(compressed);var parts=Enumerable.Range(0,(payload.Length+255)/256).Select(i=>payload.Substring(i*256,Math.Min(256,payload.Length-i*256))).ToArray();
            return new SceneWorldPacket{Layout=WorldIdentity,Parts=parts};
        }
        private SceneWorldData DecodeWorld(SceneWorldPacket packet)
        {
            if(packet==null||packet.Layout!=WorldIdentity||packet.Parts==null||packet.Parts.Length>128||packet.Parts.Any(p=>p==null||p.Length>256))throw new InvalidDataException("Scene world layout differs from the loaded construction scene");
            byte[] compressed=Convert.FromBase64String(string.Concat(packet.Parts));if(compressed.Length>24*1024)throw new InvalidDataException("Invalid compressed scene world size");
            byte[] raw;
            using(var input=new MemoryStream(compressed,false))using(var zip=new DeflateStream(input,CompressionMode.Decompress))using(var output=new MemoryStream())
            {
                var buffer=new byte[4096];int count;
                while((count=zip.Read(buffer,0,buffer.Length))>0){if(output.Length+count>4*1024*1024)throw new InvalidDataException("Scene world data exceeds decompression limit");output.Write(buffer,0,count);}raw=output.ToArray();
            }
            using(var stream=new MemoryStream(raw,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=2)throw new InvalidDataException("Unknown construction world format");
                var state=new SceneWorldData{Gameplay=reader.ReadDouble(),Presentation=reader.ReadDouble(),Serial=reader.ReadInt64()};
                if(!SceneValue.Finite(state.Gameplay)||!SceneValue.Finite(state.Presentation)||state.Gameplay<0||state.Presentation<0||state.Serial<0)throw new InvalidDataException("Invalid scene world clocks");
                state.Graph=Graph.ReadWorld(reader,WorldOutputs);
                var variables=worldVariables;if(reader.ReadUInt16()!=variables.Length)throw new InvalidDataException("World variable layout differs");state.Variables=new Dictionary<string,SceneValue>(StringComparer.Ordinal);
                foreach(var variable in variables)state.Variables.Add(variable.Id,WorldValues.Read(reader,variable.Kind));
                var rules=worldRules;if(reader.ReadUInt16()!=rules.Length)throw new InvalidDataException("World rule layout differs");state.Rules=new Dictionary<string,RuleProgress>(StringComparer.Ordinal);
                foreach(var rule in rules){var progress=new RuleProgress{Fired=reader.ReadBoolean(),Faulted=reader.ReadBoolean(),Last=reader.ReadDouble()};if(!SceneValue.Finite(progress.Last))throw new InvalidDataException("Invalid world rule clock");state.Rules.Add(rule.Key,progress);}
                int active=reader.ReadUInt16();if(active>256)throw new InvalidDataException("World effect budget exceeded");state.Effects=new EffectInstance[active];var ids=new HashSet<long>();
                for(int i=0;i<active;i++)
                {
                    var effect=new EffectInstance{Id=reader.ReadInt64(),Owner=WorldValues.Text(reader,256),Definition=WorldValues.Text(reader,128),Started=reader.ReadDouble()};EffectData definition;
                    if(effect.Id<1||effect.Id>state.Serial||!ids.Add(effect.Id)||effect.Owner==""||!effects.TryGetValue(effect.Definition,out definition)||!SceneValue.Finite(effect.Started)||effect.Started<0||effect.Started>(definition.Clock==SceneClock.Gameplay?state.Gameplay:state.Presentation))throw new InvalidDataException("Invalid world effect state");state.Effects[i]=effect;
                }
                int pending=reader.ReadUInt16();if(pending>256)throw new InvalidDataException("World event budget exceeded");state.Events=new SceneEventSample[pending];for(int i=0;i<pending;i++){state.Events[i]=WorldValues.Event(reader);if(state.Events[i]==null)throw new InvalidDataException("Missing world event");}
                if(reader.ReadUInt16()!=trees.Length)throw new InvalidDataException("World tree layout differs");state.Trees=new WorldTreeState[trees.Length];state.Causes=new SceneEventSample[trees.Length];
                for(int t=0;t<trees.Length;t++)
                {
                    var tree=new WorldTreeState{Result=(WorldTreeResult)reader.ReadByte(),Fired=reader.ReadBoolean()};state.Causes[t]=WorldValues.Event(reader);int length=reader.ReadUInt16();
                    if(!Enum.IsDefined(typeof(WorldTreeResult),tree.Result)||(tree.Result==WorldTreeResult.Running?(length!=trees[t].Code.Length||state.Causes[t]==null):length!=0))throw new InvalidDataException("Invalid world control tree state");
                    if(length>0)
                    {
                        tree.Nodes=new WorldTreeNodeState[length];
                        for(int n=0;n<length;n++)
                        {
                            var node=new WorldTreeNodeState{Result=(WorldTreeResult)reader.ReadByte()};int flags=reader.ReadByte();
                            if(flags>63||!Enum.IsDefined(typeof(WorldTreeResult),node.Result))throw new InvalidDataException("Invalid world control node");
                            if((flags&1)!=0)node.Cursor=reader.ReadInt32();if((flags&2)!=0)node.Iterations=reader.ReadInt32();if((flags&4)!=0)node.Started=reader.ReadDouble();if((flags&8)!=0)node.Effect=reader.ReadInt64();node.Armed=(flags&16)!=0;node.Signalled=(flags&32)!=0;
                            if(node.Cursor<0||node.Cursor>trees[t].Code[n].Children.Length||node.Iterations<0||!SceneValue.Finite(node.Started)||node.Started<0||node.Effect<0)throw new InvalidDataException("Invalid world control cursor");tree.Nodes[n]=node;
                        }
                    }
                    state.Trees[t]=tree;
                }
                state.Fields=Fields.ReadWorld(reader);state.Sensors=Sensors.ReadWorld(reader);state.Observations=Signals.ReadObservations(reader);if(stream.Position!=stream.Length)throw new InvalidDataException("Unexpected trailing scene world data");return state;
            }
        }
        internal void ValidateWorld(SceneWorldPacket packet){DecodeWorld(packet);}
        internal void RestoreWorld(SceneWorldPacket packet)
        {
            using(Signals.Batch())RestoreWorldValues(packet);
        }
        private void RestoreWorldValues(SceneWorldPacket packet)
        {
            var state=DecodeWorld(packet);
            Signals.AdvanceGeneration();
            foreach(var effect in activeEffects.ToArray())Cancel(effect.Id);
            gameplayTime=state.Gameplay;presentationTime=state.Presentation;effectSerial=state.Serial;
            PublishClocks();Signals.RestoreObservations(state.Observations);
            foreach(var variable in state.Variables)Signals.Publish("variable:"+variable.Key,variable.Value,ObservationQuality.Exact);
            Graph.RestoreWorld(state.Graph);Properties.RestoreBindings();
            Fields.Restore(state.Fields);
            Sensors.Restore(state.Sensors);
            foreach(var rule in state.Rules){var value=ruleProgress[rule.Key];value.Fired=rule.Value.Fired;value.Faulted=rule.Value.Faulted;value.Last=rule.Value.Last;}
            activeEffects.AddRange(state.Effects);
            for(int i=0;i<trees.Length;i++){trees[i].State=state.Trees[i];trees[i].Cause=state.Causes[i];}
            Signals.RestoreEvents(state.Events);UpdateEffects();Properties.Compose();if(WorldRestored!=null)WorldRestored();
            // received state never publishes the viewer's personal save packet
        }
    }
}
