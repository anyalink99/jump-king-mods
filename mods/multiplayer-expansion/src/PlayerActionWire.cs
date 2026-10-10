using System;
using System.IO;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal enum PlayerActionKind : byte { Policy, KickRequest, KickEvent, Hello }
    internal sealed class PlayerActionPacket
    {
        internal PlayerActionKind Kind;
        internal byte Flags;
        internal int Direction;
        internal ulong Session, Revision, Map, Serial, Actor, ActorEpoch, ActorTransition, Target, TargetEpoch, TargetTransition, Policy;
        internal double Time;
        internal Vector2 Origin, Hit;
        internal PlayerActionPacket Copy() { return (PlayerActionPacket)MemberwiseClone(); }
    }
    internal static class PlayerActionWire
    {
        private const uint Magic=0x3258504d;
        internal const int Size=120;
        internal static bool Recognizes(byte[] bytes)
        { return bytes!=null && bytes.Length>=5 && BitConverter.ToUInt32(bytes,0)==Magic && bytes[4]==131; }
        internal static byte[] Encode(PlayerActionPacket value)
        {
            using(var stream=new MemoryStream()) using(var writer=new BinaryWriter(stream)) {
                writer.Write(Magic);writer.Write((byte)131);writer.Write((byte)value.Kind);writer.Write(value.Flags);writer.Write((sbyte)value.Direction);
                foreach(ulong number in new[]{value.Session,value.Revision,value.Map,value.Serial,value.Actor,value.ActorEpoch,value.ActorTransition,value.Target,value.TargetEpoch,value.TargetTransition,value.Policy}) writer.Write(number);
                writer.Write(value.Time);writer.Write(value.Origin.X);writer.Write(value.Origin.Y);writer.Write(value.Hit.X);writer.Write(value.Hit.Y);
                return stream.ToArray();
            }
        }
        internal static bool Decode(byte[] bytes,out PlayerActionPacket value)
        {
            value=null;
            if(!Recognizes(bytes) || bytes.Length!=Size) return false;
            using(var reader=new BinaryReader(new MemoryStream(bytes,false))) {
                reader.ReadUInt32();reader.ReadByte();
                var result=new PlayerActionPacket{Kind=(PlayerActionKind)reader.ReadByte(),Flags=reader.ReadByte(),Direction=reader.ReadSByte(),
                    Session=reader.ReadUInt64(),Revision=reader.ReadUInt64(),Map=reader.ReadUInt64(),Serial=reader.ReadUInt64(),Actor=reader.ReadUInt64(),ActorEpoch=reader.ReadUInt64(),
                    ActorTransition=reader.ReadUInt64(),Target=reader.ReadUInt64(),TargetEpoch=reader.ReadUInt64(),TargetTransition=reader.ReadUInt64(),Policy=reader.ReadUInt64(),Time=reader.ReadDouble(),
                    Origin=new Vector2(reader.ReadSingle(),reader.ReadSingle()),Hit=new Vector2(reader.ReadSingle(),reader.ReadSingle())};
                if(result.Kind>PlayerActionKind.Hello || result.Flags>3 || result.Session==0 || result.Revision==0 || result.Serial==0 || result.Actor==0 || result.ActorEpoch==0 ||
                    double.IsNaN(result.Time) || double.IsInfinity(result.Time) || result.Time<0 || result.Time>1e10 || !Finite(result.Origin) || !Finite(result.Hit)) return false;
                bool kick=result.Kind==PlayerActionKind.KickRequest || result.Kind==PlayerActionKind.KickEvent;
                if(kick && (Math.Abs(result.Direction)!=1 || result.Flags!=0 || result.Policy==0 || result.Target==0 || result.Target==result.Actor || result.TargetEpoch==0)) return false;
                if(!kick && (result.Direction!=0 || result.Target!=0 || result.TargetEpoch!=0 || result.TargetTransition!=0)) return false;
                if(result.Kind==PlayerActionKind.Policy && result.Policy!=result.Serial || result.Kind==PlayerActionKind.Hello && (result.Policy!=0 || result.Flags!=0)) return false;
                value=result;return true;
            }
        }
        private static bool Finite(Vector2 v) { return !float.IsNaN(v.X) && !float.IsNaN(v.Y) && Math.Abs(v.X)<=1e7 && Math.Abs(v.Y)<=1e7; }
    }
}
