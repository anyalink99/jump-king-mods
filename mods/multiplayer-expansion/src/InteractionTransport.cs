using System;
using Steamworks;

namespace MultiplayerExpansion
{
    internal static class InteractionTransport
    {
        private const int Channel=21741;
        internal static void Pump(Action<ulong,byte[]> receive)
        {
            if(Client.Link!=null) {Client.PacketAvailableForGame();return;}
            for(int i=0;i<64;i++)
            {
                var packet=LegacySafety.ReadSteam(Channel);if(packet==null) break;
                if(packet.Item2.Length<=65536) receive(packet.Item1,packet.Item2);
            }
        }
        internal static void Send(ulong recipient,byte[] packet,bool reliable=false)
        {
            if(Client.Link!=null)
            {if(Client.Link.Ready && (Client.Role!=1 || NativeSession.PeerReady)) Client.Link.Send(packet);}
            else SteamNetworking.SendP2PPacket(new CSteamID(recipient),packet,(uint)packet.Length,reliable?EP2PSend.k_EP2PSendReliable:EP2PSend.k_EP2PSendUnreliableNoDelay,Channel);
        }
    }
}
