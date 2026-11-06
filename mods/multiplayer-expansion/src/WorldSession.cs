using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using JumpKing;
using JKRuntime.State;
using JKRuntime.World;

namespace MultiplayerExpansion
{
    internal sealed class WorldPacket
    {
        internal ulong Epoch,Sequence,Map;
        internal bool Enabled;
        internal double Time;
        internal byte[] State;
    }
    internal static class WorldWire
    {
        private const uint Magic=0x3258504d;
        internal static bool Recognizes(byte[] bytes) {return bytes!=null && bytes.Length>=5 && BitConverter.ToUInt32(bytes,0)==Magic && (bytes[4]==128 || bytes[4]==130);}
        internal static byte[] Hello(ulong epoch)
        {using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(Magic);w.Write((byte)130);w.Write(epoch);return s.ToArray();}}
        internal static byte[] Encode(WorldPacket p)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){
                w.Write(Magic);w.Write((byte)128);w.Write(p.Enabled);w.Write(p.Epoch);w.Write(p.Sequence);w.Write(p.Map);w.Write(p.Time);w.Write(p.State.Length);w.Write(p.State);return s.ToArray();
            }
        }
        internal static bool Decode(byte[] bytes,out WorldPacket packet)
        {
            packet=null;if(!Recognizes(bytes)||bytes.Length<42||bytes.Length>48100)return false;
            try {using(var s=new MemoryStream(bytes,false))using(var r=new BinaryReader(s)){
                r.ReadUInt32();if(r.ReadByte()!=128)return false;byte flag=r.ReadByte();if(flag>1)return false;
                var p=new WorldPacket{Enabled=flag==1,Epoch=r.ReadUInt64(),Sequence=r.ReadUInt64(),Map=r.ReadUInt64(),Time=r.ReadDouble()};
                int length=r.ReadInt32();if(length<0||length!=s.Length-s.Position||p.Epoch==0||p.Sequence==0||double.IsNaN(p.Time)||p.Time<0||p.Time>1e10)return false;
                p.State=r.ReadBytes(length);packet=p;return true;
            }}catch(EndOfStreamException){return false;}
        }
        internal static bool Accepts(WorldPacket p,ulong sender,ulong owner,ulong ownerEpoch,ulong map,ulong priorEpoch,ulong priorSequence)
        {return sender==owner && p.Epoch==ownerEpoch && p.Map==map && (p.Epoch!=priorEpoch || p.Sequence>priorSequence);}
    }
    internal static class WorldSession
    {
        private static readonly Stopwatch clock=Stopwatch.StartNew();
        private static readonly string path=Path.Combine(NativeMod.Package,"world-sync.txt");
        private static bool saved,enabled,installed,received;
        private static ulong sequence,accepted,authorityEpoch,worldMap;
        private static double nextSend,lastReceive,hostTime;
        private static double nextHello;
        private static WorldControl control;
        private static ulong controlMap,controlEpoch;
        private static readonly Dictionary<ulong,ulong> capabilities=new Dictionary<ulong,ulong>();
        internal static bool Supports(ulong id)
        {ulong known;var peer=AdvancedSession.VisualPeer(id);return peer!=null && capabilities.TryGetValue(id,out known)&&known==peer.Frame.Epoch;}
        internal static string Status="";
        internal static bool Ready;
        internal static string Diagnostics {get{return Status+" states="+WorldRegistry.Shared.Inspect().Length+" tx="+sequence+" rx="+accepted;}}
        internal static bool Enabled {get{return AdvancedSession.CanEdit?saved:enabled;}}
        internal static bool Follower {get{return Ready && Enabled && AdvancedSession.Connected && !AdvancedSession.CanEdit && received && worldMap==AdvancedSession.CurrentMap;}}
        internal static double HostTime {get{return hostTime;}}
        internal static void Install()
        {
            if(installed)return;installed=true;
            try{saved=File.Exists(path)&&File.ReadAllText(path).Trim()=="1";}catch(IOException){Status="Cannot read world settings.";}
            if(NativeMod.DebugEnabled && Environment.GetEnvironmentVariable("MPEX_WORLD_SYNC")=="1")saved=true;
        }
        internal static void Set(bool value)
        {
            if(!AdvancedSession.CanEdit)return;
            try{File.WriteAllText(path,value?"1":"0");saved=value;nextSend=0;Status="";}
            catch(IOException e){Status="Cannot save world settings: "+e.Message;}
            catch(UnauthorizedAccessException){Status="Cannot write world settings.";}
        }
        internal static void Reset()
        {ReleaseControl();JKRuntime.World.SwitchBlocksWorld.Release();WorldInputs.Reset();capabilities.Clear();enabled=received=false;sequence=accepted=authorityEpoch=worldMap=0;nextHello=nextSend=0;NativeWorldState.Reset();}
        internal static void Receive(ulong sender,byte[] bytes)
        {
            if(!Ready)return;
            if(bytes.Length==13 && bytes[4]==130){var peer=AdvancedSession.VisualPeer(sender);ulong value=BitConverter.ToUInt64(bytes,5);if(peer!=null&&peer.Frame.Epoch==value)capabilities[sender]=value;return;}
            if(AdvancedSession.CanEdit || sender!=AdvancedSession.Owner)return;
            var owner=AdvancedSession.VisualPeer(sender);WorldPacket p;
            if(owner==null || !WorldWire.Decode(bytes,out p) || !WorldWire.Accepts(p,sender,AdvancedSession.Owner,owner.Frame.Epoch,AdvancedSession.CurrentMap,authorityEpoch,accepted))return;
            try {
                if(p.Enabled){
                    // validate the complete snapshot before taking authority or changing roles
                    RestoreSnapshot(p);
                }else{ReleaseControl();JKRuntime.World.SwitchBlocksWorld.Release();}
                enabled=p.Enabled;received=true;authorityEpoch=p.Epoch;accepted=p.Sequence;worldMap=p.Map;
                lastReceive=clock.Elapsed.TotalSeconds;hostTime=p.Time;Status=p.Enabled?"World: following host":"World: independent";
            }catch(Exception e){if(!(e is InvalidDataException)&&!(e is EndOfStreamException)&&!(e is ArgumentException))throw;Status="World sync rejected: "+e.Message;}
        }
        internal static void RestoreSnapshot(WorldPacket packet)
        {
            // validate before taking authority; an existing receiver validates inside Receive
            if(!MatchesControl(packet.Map,packet.Epoch,WorldRole.Replica)) JKRuntime.World.WorldRegistry.Shared.Prepare(packet.State);
            EnsureControl(packet.Map,packet.Epoch,WorldRole.Replica);
            control.Receive(packet.State);control.SetTime(packet.Time);
        }
        private static bool MatchesControl(ulong map,ulong epoch,WorldRole role)
        {return control!=null && controlMap==map && controlEpoch==epoch && control.Role==role;}
        private static void EnsureControl(ulong map,ulong epoch,WorldRole role)
        {
            if(MatchesControl(map,epoch,role))return;
            ReleaseControl();control=WorldControl.Begin("multiplayer-expansion",map.ToString(System.Globalization.CultureInfo.InvariantCulture),epoch,role);controlMap=map;controlEpoch=epoch;
        }
        private static void ReleaseControl()
        {if(control!=null){control.Dispose();control=null;}controlMap=controlEpoch=0;}
        private static void PublishActors()
        {
            var actors=new List<WorldActor>();
            foreach(var peer in AdvancedSession.WorldPeers){
                var current=AdvancedSession.VisualPeer(peer.Id);if(current==null||!current.Frame.Active||current.Frame.Map!=AdvancedSession.CurrentMap)continue;
                var f=current.Frame;bool jumpPressed,button;WorldInputs.Consume(peer.Id,f.Epoch,out jumpPressed,out button);
                actors.Add(new WorldActor(peer.Id,f.Epoch,f.Sequence,f.Jump,f.Position,f.Velocity,f.Grounded,f.Width,f.Height,jumpPressed,button));
            }
            control.SetActors(actors);control.SetLocalActor(AdvancedSession.Self);
        }
        internal static void Tick()
        {
            if(!Ready || !AdvancedSession.Connected){ReleaseControl();return;}
            if(control!=null && (controlMap!=AdvancedSession.CurrentMap || AdvancedSession.CanEdit && controlEpoch!=AdvancedSession.Epoch)) {ReleaseControl();received=false;JKRuntime.World.SwitchBlocksWorld.Release();}
            if(AdvancedSession.CanEdit){
                if(saved)EnsureControl(AdvancedSession.CurrentMap,AdvancedSession.Epoch,WorldRole.Host);
                else ReleaseControl();
            }
            if(control!=null)PublishActors();
            if(clock.Elapsed.TotalSeconds>=nextHello){
                nextHello=clock.Elapsed.TotalSeconds+.5;
                foreach(var peer in AdvancedSession.WorldPeers)if(AdvancedSession.VisualPeer(peer.Id)!=null)InteractionTransport.Send(peer.Id,WorldWire.Hello(AdvancedSession.Epoch));
            }
            if(!AdvancedSession.CanEdit){
                WorldInputs.Poll();double clientNow=clock.Elapsed.TotalSeconds;
                if(clientNow>=nextSend){nextSend=clientNow+.05;WorldInputs.Send();}
                if(Follower && clientNow-lastReceive>1)Status="World: waiting for host (state held)";return;
            }
            double now=clock.Elapsed.TotalSeconds;if(now<nextSend)return;nextSend=now+.05;
            try {
                var native=GameClock.ReadCurrent();
                double time=TimeSpan.FromSeconds(native.Ticks*Game1.instance.TargetElapsedTime.TotalSeconds+native.Time).TotalSeconds;
                if(control!=null)control.SetTime(time);
                var p=new WorldPacket{Enabled=saved,Epoch=AdvancedSession.Epoch,Sequence=++sequence,Map=AdvancedSession.CurrentMap,Time=time,State=saved?JKRuntime.World.WorldRegistry.Shared.Capture():new byte[0]};
                AdvancedSession.BroadcastWorld(WorldWire.Encode(p));Status=saved?"World: host authority":"World: independent";
            }catch(InvalidDataException e){Status="World sync unavailable: "+e.Message;}
        }
    }
}
