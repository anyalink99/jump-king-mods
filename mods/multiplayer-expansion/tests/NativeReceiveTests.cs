using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using HarmonyLib;
using JumpKingMultiplayer.Models;
using Steamworks;

namespace MultiplayerExpansion
{
    internal static class NativeReceiveTests
    {
        private static MultiplayerManager manager;
        private static TrackData received;
        private static ulong peerId;
        private static bool applied;
        internal static void Warm()
        {
            // native mode attaches after this method has already been compiled and called
            manager = (MultiplayerManager)FormatterServices.GetUninitializedObject(typeof(MultiplayerManager));
            manager.GetUpdates();
        }
        private static bool Capture(CSteamID id, TrackData data)
        { peerId = id.m_SteamID; received = data; applied = true; return false; }
        internal static void Run()
        {
            var capture = new Harmony("multiplayer-expansion.native-receive-test");
            capture.Patch(AccessTools.Method(typeof(MultiplayerManager), "UpdatePlayerState"), prefix: new HarmonyMethod(typeof(NativeReceiveTests), "Capture"));
            string token = Guid.NewGuid().ToString("N");
            try
            {
                using (var receiver = new LocalLink(true, token))
                using (var sender = new LocalLink(false, token))
                {
                    var timeout = Stopwatch.StartNew();
                    while (!receiver.Ready || !sender.Ready)
                    { if (timeout.ElapsedMilliseconds > 5000) throw new Exception("Native receive handshake timed out"); Thread.Sleep(5); }
                    Client.Link = receiver; Client.Role = 1; manager.LobbyId = new CSteamID(1);
                    sender.Send(Encoding.ASCII.GetBytes(Parser.ToString(new TrackData { posX = 123, posY = 234 })));
                    while (!applied)
                    {
                        manager.GetUpdates();
                        if (timeout.ElapsedMilliseconds > 5000) throw new Exception("The running Multiplayer manager did not apply a received packet");
                        Thread.Sleep(5);
                    }
                    if (received.posX != 123 || received.posY != 234 || peerId != 2) throw new Exception("Native packet parsing or peer identity changed");
                }
            }
            finally { Client.Link = null; capture.UnpatchAll(capture.Id); }
            Console.WriteLine("[OK] Native late attach: real Multiplayer receive/parser reaches player state over the local transport");
        }
    }
}
