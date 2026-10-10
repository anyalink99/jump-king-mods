using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using JumpKingMultiplayer.Models;
using Steamworks;

namespace MultiplayerExpansion
{
    internal static class LegacySafety
    {
        internal const int PacketBudget = 64, MaxPayload = 4096;
        internal static long Rejected, Coalesced;
        internal static void Install(Harmony hooks)
        {
            hooks.Patch(AccessTools.Method(typeof(MultiplayerManager), "GetUpdates"), prefix:new HarmonyMethod(typeof(LegacySafety),"Receive"));
            hooks.Patch(AccessTools.Method(typeof(MultiplayerManager), "UpdatePlayerState"), prefix:new HarmonyMethod(typeof(LegacySafety),"ValidateState"));
            hooks.Patch(AccessTools.Method(typeof(MultiplayerManager), "UpdateLobbyMemberIds"), prefix:new HarmonyMethod(typeof(LegacySafety),"HasLobby"), postfix:new HarmonyMethod(typeof(LegacySafety),"RefreshOwner"));
            hooks.Patch(AccessTools.Method(typeof(MultiplayerManager), "LeaveLobby"), postfix:new HarmonyMethod(typeof(LegacySafety),"Left"));
            hooks.Patch(AccessTools.Method(typeof(MultiplayerManager), "HandlePlayerJoined"), postfix:new HarmonyMethod(typeof(LegacySafety),"Joined"));
            // callbacks capture the manager; reject failed or late events before .Value access
            foreach (var method in typeof(MultiplayerManager).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)) {
                var args = method.GetParameters(); if (args.Length != 1) continue;
                string guard = args[0].ParameterType == typeof(LobbyDataUpdate_t) ? "LobbyData" : args[0].ParameterType == typeof(LobbyChatUpdate_t) ? "LobbyChat"
                    : args[0].ParameterType == typeof(LobbyEnter_t) ? "LobbyEnter" : args[0].ParameterType == typeof(LobbyCreated_t) ? "LobbyCreated" : null;
                if (guard != null) hooks.Patch(method, prefix:new HarmonyMethod(typeof(LegacySafety),guard));
            }
        }
        internal static bool Member(MultiplayerManager manager, ulong id)
        {
            return manager != null && manager.LobbyId.HasValue && manager.LobbyPlayers != null
                && id != manager.UserSteamId.m_SteamID && manager.LobbyPlayers.Contains(new CSteamID(id));
        }
        internal static bool Decode(byte[] bytes, out TrackData data)
        {
            data = new TrackData();
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxPayload) return false;
            string json = Encoding.ASCII.GetString(bytes).Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}")) return false;
            try { data = Parser.FromString<TrackData>(json); }
            catch (Exception e) {
                if (e.GetType().Namespace != "Newtonsoft.Json" && !(e is FormatException) && !(e is OverflowException) && !(e is ArgumentException)) throw;
                return false;
            }
            return Valid(data);
        }
        internal static bool Valid(TrackData data)
        {
            return !float.IsNaN(data.posX) && !float.IsNaN(data.posY) && Math.Abs(data.posX)<=10000000 && Math.Abs(data.posY)<=10000000
                && (int)data.sprite>=0 && (int)data.sprite<=12 && (int)data.flip>=0 && (int)data.flip<=2
                && data.colorIdx>=0 && data.colorIdx<8 && data.skippedFrames>=0 && data.skippedFrames<=600
                && data.screenIndex1>=-100000 && data.screenIndex1<=100000;
        }
        internal static void Drain(Func<Tuple<ulong,byte[]>> read, Func<ulong,bool> member, Action<ulong,TrackData> apply)
        {
            var latest = new Dictionary<ulong,TrackData>();
            for (int i=0; i<PacketBudget; i++) {
                var packet = read(); if (packet == null) break;
                TrackData data;
                if (!member(packet.Item1) || !Decode(packet.Item2,out data)) { Rejected++; continue; }
                if (latest.ContainsKey(packet.Item1)) Coalesced++;
                latest[packet.Item1] = data;
            }
            foreach (var pair in latest) if (member(pair.Key)) apply(pair.Key,pair.Value);
        }
        private static bool Receive(MultiplayerManager __instance)
        {
            if (!__instance.LobbyId.HasValue) return false;
            Drain(Read, id=>Member(__instance,id), (id,data)=>__instance.UpdatePlayerState(new CSteamID(id),data));
            return false;
        }
        internal static void Pump(MultiplayerManager manager) { if(manager!=null) Receive(manager); }
        private static Tuple<ulong,byte[]> Read()
        {
            if (Client.Link != null) {
                byte[] bytes = Client.TakeGamePacket();
                return bytes == null ? null : Tuple.Create((ulong)(3-Client.Role),bytes);
            }
            return ReadSteam(0);
        }
        internal static Tuple<ulong,byte[]> ReadSteam(int channel)
        {
            uint size, count; CSteamID peer;
            if (!SteamNetworking.IsP2PPacketAvailable(out size,channel)) return null;
            // drain oversized legacy messages too, so one bad message can't block good ones
            if (size > 1048576) return null;
            var packet = new byte[size];
            if (!SteamNetworking.ReadP2PPacket(packet,size,out count,out peer,channel)) return null;
            return Tuple.Create(peer.m_SteamID,count==size ? packet : new byte[0]);
        }
        private static bool ValidateState(MultiplayerManager __instance, CSteamID id, ref TrackData data)
        {
            if (!Member(__instance,id.m_SteamID) || !Valid(data)) { Rejected++; return false; }
            // the original constructor indexes an eight-entry palette without checking
            if (data.colorIdx==0) data.colorIdx=1+(int)(id.m_SteamID%7);
            return true;
        }
        private static bool HasLobby(MultiplayerManager __instance) { return __instance.LobbyId.HasValue; }
        private static void RefreshOwner(MultiplayerManager __instance)
        { if(Client.Link==null) __instance.LobbyOwner=__instance.LobbyId.HasValue ? SteamMatchmaking.GetLobbyOwner(__instance.LobbyId.Value) : (CSteamID?)null; }
        private static void Left(MultiplayerManager __instance) { __instance.LobbyOwner=null; }
        private static void Joined(MultiplayerManager __instance)
        { AccessTools.Field(typeof(MultiplayerManager),"lastTrackData").SetValue(__instance,new TrackData{posX=-10000000,posY=-10000000}); }
        private static bool LobbyData(MultiplayerManager __instance, LobbyDataUpdate_t __0)
        { return __instance.LobbyId.HasValue && __0.m_ulSteamIDLobby==__instance.LobbyId.Value.m_SteamID; }
        private static bool LobbyChat(MultiplayerManager __instance, LobbyChatUpdate_t __0)
        { return __instance.LobbyId.HasValue && __0.m_ulSteamIDLobby==__instance.LobbyId.Value.m_SteamID; }
        private static bool LobbyEnter(LobbyEnter_t __0) { return __0.m_EChatRoomEnterResponse==(uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess; }
        private static bool LobbyCreated(LobbyCreated_t __0) { return __0.m_eResult==EResult.k_EResultOK; }
    }
}
