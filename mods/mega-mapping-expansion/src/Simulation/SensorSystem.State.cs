using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class SensorSnapshot
    {
        internal SensorSystem Owner;
        internal Dictionary<string,Dictionary<string,SensorMembership>> Memberships;
        internal string[] Initialized,Faulted;
    }
    internal sealed partial class SensorSystem
    {
        private static SurfaceObservation Copy(SurfaceObservation c)
        {return c==null?null:new SurfaceObservation{Id=c.Id,Type=c.Type,Tags=(string[])c.Tags.Clone(),Screen=c.Screen,Position=c.Position,Normal=c.Normal,Velocity=c.Velocity,PositionQuality=c.PositionQuality,NormalQuality=c.NormalQuality,VelocityQuality=c.VelocityQuality,Impact=c.Impact};}
        private static SensorMembership Copy(SensorMembership m){return new SensorMembership{Dwell=m.Dwell,Inside=m.Inside,Actor=m.Actor,Contact=Copy(m.Contact)};}
        internal SensorSnapshot Capture()
        {
            return new SensorSnapshot{Owner=this,Memberships=memberships.ToDictionary(p=>p.Key,p=>p.Value.ToDictionary(v=>v.Key,v=>Copy(v.Value),StringComparer.Ordinal),StringComparer.Ordinal),Initialized=initialized.ToArray(),Faulted=faulted.ToArray()};
        }
        internal void Validate(SensorSnapshot snapshot)
        {
            if(snapshot==null||snapshot.Owner!=this||snapshot.Memberships==null||snapshot.Memberships.Count!=memberships.Count||snapshot.Initialized==null||snapshot.Initialized.Length>4096||snapshot.Faulted==null||snapshot.Faulted.Any(id=>!memberships.ContainsKey(id)))throw new InvalidDataException("Sensor snapshot layout differs");
            foreach(var sensor in snapshot.Memberships)
            {
                if(!memberships.ContainsKey(sensor.Key)||sensor.Value==null||sensor.Value.Count>256)throw new InvalidDataException("Invalid sensor membership layout");
                foreach(var pair in sensor.Value)
                {
                    var m=pair.Value;var c=m==null?null:m.Contact;
                    if(pair.Key==null||pair.Key.Length>512||m==null||string.IsNullOrEmpty(m.Actor)||m.Actor.Length>128||!SceneValue.Finite(m.Dwell)||m.Dwell<0||c==null||string.IsNullOrEmpty(c.Id)||c.Id.Length>256||c.Type==null||c.Type.Length>256||c.Screen<1||c.Screen>4096||c.Tags==null||c.Tags.Length>32||c.Tags.Any(t=>t==null||t.Length>128)||c.Position.Kind!=SceneValueKind.Vector2||c.Normal.Kind!=SceneValueKind.Vector2||c.Velocity.Kind!=SceneValueKind.Vector2||!Enum.IsDefined(typeof(ObservationQuality),c.PositionQuality)||!Enum.IsDefined(typeof(ObservationQuality),c.NormalQuality)||!Enum.IsDefined(typeof(ObservationQuality),c.VelocityQuality))throw new InvalidDataException("Invalid detached sensor contact");
                }
            }
            foreach(string key in snapshot.Initialized)if(string.IsNullOrEmpty(key)||key.Length>257||!memberships.Keys.Any(id=>key.StartsWith(id+"/",StringComparison.Ordinal)))throw new InvalidDataException("Unknown initialized sensor actor");
        }
        internal void Restore(SensorSnapshot snapshot)
        {
            Validate(snapshot);Clear();foreach(var sensor in snapshot.Memberships)foreach(var pair in sensor.Value)memberships[sensor.Key].Add(pair.Key,Copy(pair.Value));
            foreach(string id in snapshot.Initialized)initialized.Add(id);foreach(string id in snapshot.Faulted)faulted.Add(id);
        }
        internal void WriteWorld(BinaryWriter writer)
        {
            writer.Write((ushort)memberships.Count);
            foreach(var sensor in memberships.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                writer.Write((ushort)sensor.Value.Count);
                foreach(var pair in sensor.Value.OrderBy(p=>p.Key,StringComparer.Ordinal))
                {
                    var m=pair.Value;var c=m.Contact;WorldValues.Text(writer,pair.Key,512);WorldValues.Text(writer,m.Actor,128);writer.Write(m.Dwell);writer.Write(m.Inside);
                    WorldValues.Text(writer,c.Id,256);WorldValues.Text(writer,c.Type,256);writer.Write((ushort)c.Screen);writer.Write(c.Impact);
                    WorldValues.Write(writer,c.Position);WorldValues.Write(writer,c.Normal);WorldValues.Write(writer,c.Velocity);writer.Write((byte)c.PositionQuality);writer.Write((byte)c.NormalQuality);writer.Write((byte)c.VelocityQuality);
                    writer.Write((byte)c.Tags.Length);foreach(string tag in c.Tags)WorldValues.Text(writer,tag,128);
                }
            }
            writer.Write((ushort)initialized.Count);foreach(string id in initialized.OrderBy(i=>i,StringComparer.Ordinal))WorldValues.Text(writer,id,257);
            writer.Write((ushort)faulted.Count);foreach(string id in faulted.OrderBy(i=>i,StringComparer.Ordinal))WorldValues.Text(writer,id,128);
        }
        internal SensorSnapshot ReadWorld(BinaryReader reader)
        {
            var snapshot=new SensorSnapshot{Owner=this,Memberships=new Dictionary<string,Dictionary<string,SensorMembership>>(StringComparer.Ordinal)};
            if(reader.ReadUInt16()!=memberships.Count)throw new InvalidDataException("World sensor layout differs");
            foreach(string id in memberships.Keys.OrderBy(i=>i,StringComparer.Ordinal))
            {
                int count=reader.ReadUInt16();if(count>256)throw new InvalidDataException("World sensor contact budget exceeded");var contacts=new Dictionary<string,SensorMembership>(StringComparer.Ordinal);snapshot.Memberships.Add(id,contacts);
                for(int i=0;i<count;i++)
                {
                    string key=WorldValues.Text(reader,512);var m=new SensorMembership{Actor=WorldValues.Text(reader,128),Dwell=reader.ReadDouble(),Inside=reader.ReadBoolean()};
                    var c=new SurfaceObservation{Id=WorldValues.Text(reader,256),Type=WorldValues.Text(reader,256),Screen=reader.ReadUInt16(),Impact=reader.ReadBoolean()};m.Contact=c;
                    c.Position=WorldValues.Read(reader,SceneValueKind.Vector2);c.Normal=WorldValues.Read(reader,SceneValueKind.Vector2);c.Velocity=WorldValues.Read(reader,SceneValueKind.Vector2);c.PositionQuality=(ObservationQuality)reader.ReadByte();c.NormalQuality=(ObservationQuality)reader.ReadByte();c.VelocityQuality=(ObservationQuality)reader.ReadByte();
                    int tags=reader.ReadByte();if(tags>32)throw new InvalidDataException("World contact tag budget exceeded");c.Tags=new string[tags];for(int t=0;t<tags;t++)c.Tags[t]=WorldValues.Text(reader,128);contacts.Add(key,m);
                }
            }
            int initial=reader.ReadUInt16();if(initial>4096)throw new InvalidDataException("World sensor actor budget exceeded");snapshot.Initialized=new string[initial];for(int i=0;i<initial;i++)snapshot.Initialized[i]=WorldValues.Text(reader,257);
            int faults=reader.ReadUInt16();if(faults>memberships.Count)throw new InvalidDataException("World sensor fault layout differs");snapshot.Faulted=new string[faults];for(int i=0;i<faults;i++)snapshot.Faulted[i]=WorldValues.Text(reader,128);
            Validate(snapshot);return snapshot;
        }
    }
}
