using System;
using System.Collections.Generic;
using System.IO;
using JumpKing.Controller;

namespace MultiplayerExpansion
{
    internal sealed class WorldInput { internal ulong Epoch,Map,Sequence,Jump,Switch; }
    internal sealed class WorldInputLedger
    {
        internal WorldInput Latest;
        private ulong jump,switches;
        internal bool Accept(WorldInput value,ulong epoch,ulong map)
        {
            if(value.Epoch!=epoch || value.Map!=map || value.Sequence==0)return false;
            if(Latest!=null && Latest.Epoch==epoch && (value.Sequence<=Latest.Sequence || value.Jump<Latest.Jump || value.Switch<Latest.Switch))return false;
            if(Latest==null || Latest.Epoch!=epoch){jump=switches=0;}
            Latest=value;return true;
        }
        internal void Consume(out bool jumpPressed,out bool switchPressed)
        {
            jumpPressed=Latest!=null&&Latest.Jump>jump;switchPressed=Latest!=null&&Latest.Switch>switches;
            if(Latest!=null){jump=Latest.Jump;switches=Latest.Switch;}
        }
    }
    internal static class WorldInputs
    {
        private const uint Magic=0x3258504d;
        private static readonly Dictionary<ulong,WorldInputLedger> peers=new Dictionary<ulong,WorldInputLedger>();
        private static WorldInput local=new WorldInput();
        private static bool jumpHeld,switchHeld;
        internal static bool Recognizes(byte[] bytes){return bytes!=null&&bytes.Length>=5&&BitConverter.ToUInt32(bytes,0)==Magic&&bytes[4]==129;}
        internal static byte[] Encode(WorldInput value)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(Magic);w.Write((byte)129);w.Write(value.Epoch);w.Write(value.Map);w.Write(value.Sequence);w.Write(value.Jump);w.Write(value.Switch);return s.ToArray();}
        }
        internal static bool Decode(byte[] bytes,out WorldInput value)
        {
            value=null;if(!Recognizes(bytes)||bytes.Length!=45)return false;
            using(var r=new BinaryReader(new MemoryStream(bytes,false))){r.ReadUInt32();r.ReadByte();value=new WorldInput{Epoch=r.ReadUInt64(),Map=r.ReadUInt64(),Sequence=r.ReadUInt64(),Jump=r.ReadUInt64(),Switch=r.ReadUInt64()};return value.Epoch!=0&&value.Sequence!=0;}
        }
        internal static void Reset(){peers.Clear();local=new WorldInput();jumpHeld=switchHeld=false;}
        internal static void Poll()
        {
            if(!WorldSession.Follower || ControllerManager.instance==null)return;
            bool jump=ControllerManager.instance.GetPadState().jump,button=JKRuntime.World.SwitchBlocksWorld.SwitchPressed;
            if(local.Epoch!=AdvancedSession.Epoch || local.Map!=AdvancedSession.CurrentMap){local=new WorldInput{Epoch=AdvancedSession.Epoch,Map=AdvancedSession.CurrentMap};jumpHeld=jump;switchHeld=button;}
            if(jump&&!jumpHeld)local.Jump++;if(button&&!switchHeld)local.Switch++;
            jumpHeld=jump;switchHeld=button;
        }
        internal static void Send()
        {if(WorldSession.Follower && local.Epoch!=0){local.Sequence++;InteractionTransport.Send(AdvancedSession.Owner,Encode(local));}}
        internal static void Receive(ulong id,byte[] bytes)
        {
            if(!WorldState.IsHost)return;var peer=AdvancedSession.VisualPeer(id);WorldInput value;
            if(peer==null||!Decode(bytes,out value))return;
            WorldInputLedger ledger;if(!peers.TryGetValue(id,out ledger)){if(peers.Count>=64)return;peers[id]=ledger=new WorldInputLedger();}
            ledger.Accept(value,peer.Frame.Epoch,AdvancedSession.CurrentMap);
        }
        internal static void Consume(ulong id,ulong epoch,out bool jump,out bool button)
        {jump=button=false;WorldInputLedger ledger;if(peers.TryGetValue(id,out ledger)&&ledger.Latest!=null&&ledger.Latest.Epoch==epoch)ledger.Consume(out jump,out button);}
    }
}
