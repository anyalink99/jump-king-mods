using System;
using System.IO;
using System.Linq;
using System.Text;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal static class WorldValues
    {
        private static readonly UTF8Encoding encoding=new UTF8Encoding(false,true);
        internal static void Text(BinaryWriter writer,string text,int maximum=4096)
        {
            text=text??"";if(text.Length>maximum)throw new InvalidDataException("Scene world text exceeds its limit");
            byte[] bytes=encoding.GetBytes(text);if(bytes.Length>65535)throw new InvalidDataException("Scene world text exceeds UTF-8 limit");writer.Write((ushort)bytes.Length);writer.Write(bytes);
        }
        internal static string Text(BinaryReader reader,int maximum=4096)
        {
            int count=reader.ReadUInt16();if(count>maximum*4)throw new InvalidDataException("Invalid scene world text length");
            byte[] bytes=reader.ReadBytes(count);if(bytes.Length!=count)throw new EndOfStreamException();string text=encoding.GetString(bytes);if(text.Length>maximum)throw new InvalidDataException("Invalid scene world text length");return text;
        }
        internal static void Write(BinaryWriter writer,SceneValue value)
        {
            switch(value.Kind)
            {
                case SceneValueKind.Text:Text(writer,value.Text);break;
                case SceneValueKind.Boolean:writer.Write(value.X!=0);break;
                case SceneValueKind.Integer:writer.Write((int)value.X);break;
                case SceneValueKind.Number:writer.Write(value.X);break;
                case SceneValueKind.Vector2:writer.Write(value.X);writer.Write(value.Y);break;
                case SceneValueKind.Color:writer.Write(value.X);writer.Write(value.Y);writer.Write(value.Z);writer.Write(value.W);break;
            }
        }
        internal static SceneValue Read(BinaryReader reader,SceneValueKind kind)
        {
            switch(kind)
            {
                case SceneValueKind.Text:return SceneValue.String(Text(reader));
                case SceneValueKind.Boolean:return SceneValue.Boolean(reader.ReadBoolean());
                case SceneValueKind.Integer:return SceneValue.Integer(reader.ReadInt32());
                case SceneValueKind.Number:return SceneValue.Number(reader.ReadDouble());
                case SceneValueKind.Vector2:return SceneValue.Vector(reader.ReadDouble(),reader.ReadDouble());
                case SceneValueKind.Color:return SceneValue.Color(reader.ReadDouble(),reader.ReadDouble(),reader.ReadDouble(),reader.ReadDouble());
                default:throw new InvalidDataException("Invalid scene world value kind");
            }
        }
        internal static ObservationQuality Quality(BinaryReader reader)
        {var value=(ObservationQuality)reader.ReadByte();if(!Enum.IsDefined(typeof(ObservationQuality),value))throw new InvalidDataException("Invalid world observation quality");return value;}
        internal static void Event(BinaryWriter writer,SceneEventSample value)
        {
            writer.Write(value!=null);if(value==null)return;Text(writer,value.Id,128);Text(writer,value.Source,512);Text(writer,value.Actor,128);writer.Write(value.Screen);writer.Write(value.Sequence);writer.Write(value.Time);
            var payload=value.Payload;writer.Write((byte)payload.Length);foreach(var sample in payload){Text(writer,sample.Id,128);writer.Write((byte)sample.Value.Kind);Write(writer,sample.Value);writer.Write((byte)sample.Quality);writer.Write(sample.Sequence);}
        }
        internal static SceneEventSample Event(BinaryReader reader)
        {
            if(!reader.ReadBoolean())return null;string id=Text(reader,128),source=Text(reader,512),actor=Text(reader,128);int screen=reader.ReadInt32();long sequence=reader.ReadInt64();double time=reader.ReadDouble();int count=reader.ReadByte();
            if(id==""||screen<1||screen>4096||sequence<0||!SceneValue.Finite(time)||time<0||count>32)throw new InvalidDataException("Invalid scene world event");
            var payload=new SignalSample[count];for(int i=0;i<count;i++){string key=Text(reader,128);var kind=(SceneValueKind)reader.ReadByte();var value=Read(reader,kind);var quality=Quality(reader);long serial=reader.ReadInt64();payload[i]=new SignalSample(key,value,quality,serial);}
            return new SceneEventSample(id,source,actor,screen,sequence,time,payload);
        }
    }
    internal sealed class GraphWorldEntry
    {
        internal int Index;
        internal SceneValue Value,Memory,Velocity;
        internal bool Initialized,Computed;
        internal ObservationQuality Quality,MemoryQuality;
    }
    internal sealed class GraphWorldData
    {
        internal SignalSample[] Inputs;
        internal GraphWorldEntry[] State,Outputs;
    }
    internal sealed partial class ValueGraph
    {
        private static bool Stateful(ValueOperation op){return op==ValueOperation.Smooth||op==ValueOperation.Spring||op==ValueOperation.Integrate||op==ValueOperation.Delay||op==ValueOperation.Hysteresis;}
        private string[] worldInputs,worldOutputSource;
        private int[] worldStateIndices,worldOutputIndices;
        private string[] WorldInputs(){return worldInputs??(worldInputs=code.Where(n=>n.Op==ValueOperation.Input&&!signals.Describe(n.Data.Source).Observation).Select(n=>n.Data.Source).Distinct(StringComparer.Ordinal).OrderBy(id=>id,StringComparer.Ordinal).ToArray());}
        private int[] WorldStateIndices(){return worldStateIndices??(worldStateIndices=Enumerable.Range(0,code.Length).Where(i=>Stateful(code[i].Op)).ToArray());}
        private int[] WorldOutputIndices(string[] outputs){if(ReferenceEquals(outputs,worldOutputSource))return worldOutputIndices;worldOutputSource=outputs;return worldOutputIndices=outputs.Distinct(StringComparer.Ordinal).Select(RequireIndex).OrderBy(i=>i).ToArray();}
        internal void PrepareWorld(string[] outputs){WorldInputs();WorldStateIndices();WorldOutputIndices(outputs);}
        internal void WriteWorld(BinaryWriter writer,string[] outputs)
        {
            var sources=WorldInputs();writer.Write((ushort)sources.Length);
            foreach(string source in sources){var sample=signals.Read(source);WorldValues.Write(writer,sample.Value);writer.Write((byte)sample.Quality);}
            int[] state=WorldStateIndices();writer.Write((ushort)state.Length);
            foreach(int i in state){WorldValues.Write(writer,values[i]);WorldValues.Write(writer,memory[i]);WorldValues.Write(writer,velocity[i]);writer.Write(initialized[i]);writer.Write(computed[i]);writer.Write((byte)quality[i]);writer.Write((byte)memoryQuality[i]);}
            int[] selected=WorldOutputIndices(outputs);writer.Write((ushort)selected.Length);
            foreach(int i in selected){WorldValues.Write(writer,values[i]);writer.Write(computed[i]);writer.Write((byte)quality[i]);}
        }
        internal GraphWorldData ReadWorld(BinaryReader reader,string[] outputs)
        {
            var sources=WorldInputs();if(reader.ReadUInt16()!=sources.Length)throw new InvalidDataException("World graph input layout differs");
            var data=new GraphWorldData{Inputs=new SignalSample[sources.Length]};
            for(int i=0;i<sources.Length;i++){var descriptor=signals.Describe(sources[i]);data.Inputs[i]=new SignalSample(sources[i],WorldValues.Read(reader,descriptor.Kind),WorldValues.Quality(reader),0);}
            int[] state=WorldStateIndices();if(reader.ReadUInt16()!=state.Length)throw new InvalidDataException("World graph state layout differs");data.State=new GraphWorldEntry[state.Length];
            for(int i=0;i<state.Length;i++){int index=state[i];var kind=code[index].Port.Kind;data.State[i]=new GraphWorldEntry{Index=index,Value=WorldValues.Read(reader,kind),Memory=WorldValues.Read(reader,kind),Velocity=WorldValues.Read(reader,kind),Initialized=reader.ReadBoolean(),Computed=reader.ReadBoolean(),Quality=WorldValues.Quality(reader),MemoryQuality=WorldValues.Quality(reader)};}
            int[] selected=WorldOutputIndices(outputs);if(reader.ReadUInt16()!=selected.Length)throw new InvalidDataException("World graph output layout differs");data.Outputs=new GraphWorldEntry[selected.Length];
            for(int i=0;i<selected.Length;i++)data.Outputs[i]=new GraphWorldEntry{Index=selected[i],Value=WorldValues.Read(reader,code[selected[i]].Port.Kind),Computed=reader.ReadBoolean(),Quality=WorldValues.Quality(reader)};
            return data;
        }
        internal void RestoreWorld(GraphWorldData state)
        {
            force=true;
            foreach(var input in state.Inputs)signals.Publish(input.Id,input.Value,input.Quality);
            foreach(var entry in state.State){int i=entry.Index;values[i]=entry.Value;memory[i]=entry.Memory;velocity[i]=entry.Velocity;initialized[i]=entry.Initialized;computed[i]=entry.Computed;quality[i]=entry.Quality;memoryQuality[i]=entry.MemoryQuality;}
            RefreshPure();
            foreach(var entry in state.Outputs){values[entry.Index]=entry.Value;computed[entry.Index]=entry.Computed;quality[entry.Index]=entry.Quality;}
        }
    }
}
