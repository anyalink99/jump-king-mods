using System;
using System.IO;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal enum PlayerPresence : byte { Playing, Paused, Spawning, Teleporting, Unavailable }
    internal sealed class InteractionFrame
    {
        internal ulong Epoch, Sequence, RulesEpoch, Revision, Map;
        internal InteractionRules Rules;
        internal bool Active;
        internal PlayerPresence Presence;
        internal ulong SimulationTick, Transition;
        internal bool Anchored { get { return Grounded && Support==0 || Presence==PlayerPresence.Paused; } }
        internal Vector2 Position, Velocity;
        internal int Width, Height;
        internal double Time;
        internal byte Pose, Flip;
        internal ulong Equipment;
        internal bool Modern;
        internal bool Physics, Grounded;
        internal ulong Support, SupportEpoch, Jump;
        internal Vector2 SupportOffset;
        internal double Echo, EchoAge;
        internal ContactImpact[] Impacts = ContactImpact.None;
        internal InteractionFrame Copy() { return (InteractionFrame)MemberwiseClone(); }
    }
    internal static class InteractionWire
    {
        private const uint Magic = 0x3258504d;
        internal const int Size = 152;
        internal const int ImpactSize = 72;
        internal static bool Recognizes(byte[] bytes)
        { return bytes != null && bytes.Length >= 4 && BitConverter.ToUInt32(bytes, 0) == Magic; }
        internal static byte[] Encode(InteractionFrame frame)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic); writer.Write((byte)4); writer.Write((byte)frame.Rules); writer.Write(frame.Active);
                writer.Write((byte)(!frame.Active && frame.Presence==PlayerPresence.Playing ? PlayerPresence.Unavailable : frame.Presence)); writer.Write(frame.Epoch); writer.Write(frame.Sequence);
                writer.Write(frame.RulesEpoch); writer.Write(frame.Revision); writer.Write(frame.Map);
                writer.Write(frame.Position.X); writer.Write(frame.Position.Y); writer.Write(frame.Velocity.X); writer.Write(frame.Velocity.Y);
                writer.Write((ushort)frame.Width); writer.Write((ushort)frame.Height);
                writer.Write(frame.Time); writer.Write(frame.Pose); writer.Write(frame.Flip); writer.Write(frame.Equipment);
                writer.Write(frame.Support); writer.Write(frame.SupportEpoch);writer.Write(frame.SupportOffset.X); writer.Write(frame.SupportOffset.Y);
                writer.Write(frame.Grounded); writer.Write(frame.Jump); writer.Write(frame.Echo); writer.Write(frame.EchoAge);
                writer.Write(frame.SimulationTick);writer.Write(frame.Transition);
                writer.Write((byte)frame.Impacts.Length);
                foreach(var hit in frame.Impacts)
                {
                    writer.Write(hit.Id);writer.Write(hit.Target);writer.Write(hit.TargetEpoch);writer.Write(hit.TargetJump);writer.Write(hit.TargetTransition);writer.Write(hit.Time);
                    writer.Write(hit.Normal.X);writer.Write(hit.Normal.Y);writer.Write(hit.Incoming.X);writer.Write(hit.Incoming.Y);writer.Write(hit.Outgoing.X);writer.Write(hit.Outgoing.Y);
                }
                return stream.ToArray();
            }
        }
        internal static bool Decode(byte[] bytes, out InteractionFrame frame)
        {
            frame = null;
            if (!Recognizes(bytes) || bytes.Length < 8 || !(bytes[4] == 1 && bytes.Length == 68 || bytes[4] == 2 && bytes.Length == 86 || bytes[4] == 3 && bytes.Length >= 136 && bytes.Length <= 136+8*64 || bytes[4]==4 && bytes.Length>=Size && bytes.Length<=Size+8*ImpactSize) || bytes[6] > 1 || (bytes[4]<4 ? bytes[7]!=0 : bytes[7]>(byte)PlayerPresence.Unavailable)) return false;
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                reader.ReadUInt32(); reader.ReadByte();
                var result = new InteractionFrame { Rules = (InteractionRules)reader.ReadByte(), Active = reader.ReadBoolean() };
                result.Presence=(PlayerPresence)reader.ReadByte(); result.Epoch = reader.ReadUInt64(); result.Sequence = reader.ReadUInt64();
                if(bytes[4]<4) result.Presence=result.Active ? PlayerPresence.Playing : PlayerPresence.Unavailable;
                if(bytes[4]==4 && result.Active!=(result.Presence==PlayerPresence.Playing || result.Presence==PlayerPresence.Paused)) return false;
                result.RulesEpoch = reader.ReadUInt64(); result.Revision = reader.ReadUInt64(); result.Map = reader.ReadUInt64();
                result.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                result.Velocity = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                result.Width = reader.ReadUInt16(); result.Height = reader.ReadUInt16();
                if (bytes[4] >= 2)
                {
                    result.Modern = true; result.Time = reader.ReadDouble(); result.Pose = reader.ReadByte(); result.Flip = reader.ReadByte(); result.Equipment = reader.ReadUInt64();
                    if (double.IsNaN(result.Time) || double.IsInfinity(result.Time) || result.Time < 0 || result.Time > 1e10 || result.Pose > 12 || result.Flip > 2 || (result.Equipment >> 18) != 0) return false;
                }
                if(bytes[4]>=3)
                {
                    result.Physics=bytes[4]==4;result.Support=reader.ReadUInt64();result.SupportEpoch=reader.ReadUInt64();result.SupportOffset=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                    byte flags=reader.ReadByte();if(flags>1) return false;result.Grounded=flags==1;
                    result.Jump=reader.ReadUInt64();result.Echo=reader.ReadDouble();result.EchoAge=reader.ReadDouble();
                    if(bytes[4]==4) {result.SimulationTick=reader.ReadUInt64();result.Transition=reader.ReadUInt64();}
                    int count=reader.ReadByte();if(count>8 || bytes.Length!=(bytes[4]==4 ? Size+count*ImpactSize : 136+count*64) || result.Support!=0 && result.SupportEpoch==0 || !Time(result.Echo) || !Time(result.EchoAge) || result.EchoAge>10 || !Finite(result.SupportOffset.X,256) || !Finite(result.SupportOffset.Y,256)) return false;
                    result.Impacts=new ContactImpact[count];
                    for(int i=0;i<count;i++)
                    {
                        var hit=new ContactImpact { Id=reader.ReadUInt64(),Target=reader.ReadUInt64(),TargetEpoch=reader.ReadUInt64(),TargetJump=reader.ReadUInt64() };
                        if(bytes[4]==4) hit.TargetTransition=reader.ReadUInt64();
                        hit.Time=reader.ReadDouble();hit.Normal=new Vector2(reader.ReadSingle(),reader.ReadSingle());hit.Incoming=new Vector2(reader.ReadSingle(),reader.ReadSingle());hit.Outgoing=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                        if(hit.Id==0 || hit.Target==0 || hit.TargetEpoch==0 || !Time(hit.Time) || hit.Time>result.Time || !((Math.Abs(hit.Normal.X)==1 && hit.Normal.Y==0)||(hit.Normal.X==0 && Math.Abs(hit.Normal.Y)==1)) || !Finite(hit.Incoming.X,100) || !Finite(hit.Incoming.Y,100) || !Finite(hit.Outgoing.X,100) || !Finite(hit.Outgoing.Y,100)) return false;
                        result.Impacts[i]=hit;
                    }
                    if(result.Presence==PlayerPresence.Paused && (result.Velocity!=Vector2.Zero || result.Support!=0 || count!=0)) return false;
                }
                if (!InteractionSettings.Valid(result.Rules) || result.Epoch == 0 || result.Sequence == 0 || result.Revision == 0
                    || !Finite(result.Position.X, 10000000) || !Finite(result.Position.Y, 10000000)
                    || !Finite(result.Velocity.X, 100) || !Finite(result.Velocity.Y, 100)
                    || result.Width < 1 || result.Width > 128 || result.Height < 1 || result.Height > 128) return false;
                frame = result; return true;
            }
        }
        private static bool Finite(float value, float limit) { return !float.IsNaN(value) && Math.Abs(value) <= limit; }
        private static bool Time(double value) { return !double.IsNaN(value) && !double.IsInfinity(value) && value>=0 && value<=1e10; }
    }

    internal sealed class InteractionPeer
    {
        internal ulong Id;
        internal InteractionFrame Frame;
        internal double Received;
        internal readonly RemoteMotion Motion = new RemoteMotion();
        internal readonly RemotePlayback Playback = new RemotePlayback();
        internal readonly VisualCorrection Visual = new VisualCorrection();
        internal InteractionFrame Sample;
        internal double OneWay;
        private readonly System.Collections.Generic.Queue<ulong> retired = new System.Collections.Generic.Queue<ulong>();
        internal bool Accept(InteractionFrame frame, double now)
        {
            if(retired.Contains(frame.Epoch)) return false;
            if (Frame != null && Frame.Epoch == frame.Epoch && frame.Sequence <= Frame.Sequence) return false;
            if (Frame != null && Frame.Epoch == frame.Epoch && frame.Modern && frame.Time < Frame.Time) return false;
            if(Frame!=null && Frame.Epoch==frame.Epoch && frame.Physics && (frame.Transition<Frame.Transition || frame.SimulationTick<Frame.SimulationTick)) return false;
            if(Frame!=null && Frame.Epoch!=frame.Epoch) {retired.Enqueue(Frame.Epoch);if(retired.Count>8) retired.Dequeue();OneWay=0;}
            var previousPrediction=Motion.Predict(now,OneWay);
            bool continuous=Frame!=null && Frame.Epoch==frame.Epoch && Frame.Map==frame.Map && Frame.Transition==frame.Transition && Frame.Presence==frame.Presence && Frame.Support==frame.Support && now-Received<.5;
            Motion.Accept(frame, now);
            Playback.Accept(frame, now);
            if(frame.Physics && frame.Echo>0)
            {
                double rtt=now-frame.Echo-frame.EchoAge;
                if(rtt>=0 && rtt<2) OneWay=OneWay==0 ? rtt*.5 : OneWay*.9+rtt*.05;
            }
            Visual.Correct(previousPrediction,Motion.Predict(now,OneWay),now,continuous);
            Frame = frame; Received = now; return true;
        }
        internal void Prepare(double now, bool local) { Sample = Frame!=null && Frame.Physics ? Motion.Predict(now,OneWay) : Motion.Sample(now,local) ?? Frame; }
        internal bool Ready(double now, ulong map, ulong epoch, ulong revision, InteractionRules rules)
        { return Frame != null && Frame.Active && now - Received < 0.5 && Frame.Map == map && Frame.RulesEpoch == epoch && Frame.Revision == revision && Frame.Rules == rules; }
    }
}
