using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class FieldSystem
    {
        internal bool IsSpring(string id){return springs.ContainsKey(id);}
        private double Position(string id,SceneValue point)
        {
            var axis=scene.Properties.Read(id,"direction");var origin=scene.Properties.Read(id,"origin");double length=Math.Sqrt(axis.X*axis.X+axis.Y*axis.Y);
            if(length<1e-10)return 0;return((point.X-origin.X)*axis.X+(point.Y-origin.Y)*axis.Y)/length/scene.Properties.Read(id,"length").X;
        }
        internal void Impulse(string id,SceneValue position,double force)
        {
            SpringChain field;if(!springs.TryGetValue(id,out field))throw new InvalidDataException(id+": impulse requires a spring field");
            field.Impulse(Position(id,position),force,scene.Properties.Read(id,"impulseRadius").X);
        }
        internal void Tick(double gameplayDelta,double presentationDelta)
        {
            foreach(var pair in springs)
                pair.Value.Tick(scene.Properties.Read(pair.Key,"clock").Text=="presentation"?presentationDelta:gameplayDelta,scene.Properties.Read(pair.Key,"stiffness").X,scene.Properties.Read(pair.Key,"propagation").X,scene.Properties.Read(pair.Key,"damping").X,scene.Properties.Read(pair.Key,"limit").X);
        }
        internal Dictionary<string,double[]> Capture(){return springs.ToDictionary(p=>p.Key,p=>p.Value.Capture(),StringComparer.Ordinal);}
        internal void Validate(Dictionary<string,double[]> values)
        {
            if(values==null||values.Count!=springs.Count)throw new InvalidDataException("Spatial field state layout differs");
            foreach(var pair in springs){double[] data;if(!values.TryGetValue(pair.Key,out data))throw new InvalidDataException("Missing spatial field state: "+pair.Key);pair.Value.Validate(data);}
        }
        internal void Restore(Dictionary<string,double[]> values){Validate(values);foreach(var pair in springs)pair.Value.Restore(values[pair.Key]);}
        internal void WriteWorld(BinaryWriter writer)
        {
            writer.Write((ushort)springs.Count);
            foreach(var pair in springs.OrderBy(p=>p.Key,StringComparer.Ordinal)){var data=pair.Value.Capture();writer.Write((ushort)data.Length);foreach(double n in data)writer.Write((float)n);}
        }
        internal Dictionary<string,double[]> ReadWorld(BinaryReader reader)
        {
            if(reader.ReadUInt16()!=springs.Count)throw new InvalidDataException("World spatial field layout differs");var result=new Dictionary<string,double[]>(StringComparer.Ordinal);
            foreach(var pair in springs.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                int count=reader.ReadUInt16();if(count!=pair.Value.Count*2)throw new InvalidDataException("World spring field cell count differs");var data=new double[count];for(int i=0;i<count;i++)data[i]=reader.ReadSingle();pair.Value.Validate(data);result.Add(pair.Key,data);
            }
            return result;
        }
    }
}
