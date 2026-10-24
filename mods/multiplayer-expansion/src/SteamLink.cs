using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Steamworks;

namespace MultiplayerExpansion
{
    // each process owns its Steam connection; game state never crosses shared memory
    internal sealed class SteamLink : IPacketLink
    {
        private readonly bool server;
        private readonly string directory;
        private readonly byte[] hello;
        private readonly Callback<SteamNetConnectionStatusChangedCallback_t> callback;
        private HSteamListenSocket listener;
        private HSteamNetConnection connection;
        private readonly Queue<byte[]> packets = new Queue<byte[]>();
        private DateTime started = DateTime.UtcNow;
        private bool connected;
        private bool authenticated;
        private bool disposed;
        private readonly SteamNetworkingConfigValue_t[] options;
        public long Sent { get; private set; }
        public long Received { get; private set; }
        public string State { get; private set; }
        public bool Ready { get { return connected && authenticated; } }

        public SteamLink(bool isServer, string session, string token)
        {
            server = isServer;
            directory = session;
            hello = Encoding.ASCII.GetBytes("MPEX/1:" + token);
            callback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatus);
            options = new[] { new SteamNetworkingConfigValue_t {
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_IP_AllowWithoutAuth,
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 1 }
            } };
            if (server)
            {
                var address = new SteamNetworkingIPAddr();
                for (int attempt = 0; attempt < 8 && listener.m_HSteamListenSocket == 0; attempt++)
                {
                    int port;
                    using (var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
                        port = ((IPEndPoint)reservation.Client.LocalEndPoint).Port;
                    address.SetIPv4(0x7f000001, (ushort)port);
                    listener = SteamNetworkingSockets.CreateListenSocketIP(ref address, options.Length, options);
                }
                if (listener.m_HSteamListenSocket == 0) throw new IOException("Steam could not open a loopback listener.");
                if (!SteamNetworkingSockets.GetListenSocketAddress(listener, out address)) throw new IOException("Steam could not read its listener address.");
                File.WriteAllText(Path.Combine(directory, "port.txt"), address.m_port.ToString());
                State = "Listening";
            }
            else State = "Waiting for host";
        }

        private void OnStatus(SteamNetConnectionStatusChangedCallback_t change)
        {
            if (server && change.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
            {
                if (change.m_info.m_hListenSocket != listener || connection.m_HSteamNetConnection != 0 || !change.m_info.m_addrRemote.IsLocalHost())
                {
                    SteamNetworkingSockets.CloseConnection(change.m_hConn, 0, "Unexpected peer", false);
                    return;
                }
                connection = change.m_hConn;
                if (SteamNetworkingSockets.AcceptConnection(connection) != EResult.k_EResultOK) throw new IOException("Steam rejected the local connection.");
            }
            if (change.m_hConn != connection) return;
            if (change.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                connected = true;
                State = "Authenticating local session";
                SendRaw(hello);
            }
            if (change.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer ||
                change.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
            {
                connected = authenticated = false;
                packets.Clear();
                State = "Disconnected: " + change.m_info.m_szEndDebug;
                SteamNetworkingSockets.CloseConnection(connection, 0, "Session ended", false);
                connection = default(HSteamNetConnection);
            }
        }

        public void Pump()
        {
            if (disposed) return;
            if (!server && State == "Waiting for host")
            {
                string path = Path.Combine(directory, "port.txt");
                ushort port;
                if (File.Exists(path) && ushort.TryParse(File.ReadAllText(path), out port) && port != 0)
                {
                    var address = new SteamNetworkingIPAddr();
                    address.SetIPv4(0x7f000001, port);
                    connection = SteamNetworkingSockets.ConnectByIPAddress(ref address, options.Length, options);
                    if (connection.m_HSteamNetConnection == 0) throw new IOException("Steam could not connect to the local host.");
                    State = "Connecting";
                }
            }
            if (!Ready && !State.StartsWith("Disconnected") && (DateTime.UtcNow - started).TotalSeconds > 60)
                throw new TimeoutException("The other test client did not connect within 60 seconds.");
            if (!connected) return;
            var pointers = new IntPtr[32];
            int count = SteamNetworkingSockets.ReceiveMessagesOnConnection(connection, pointers, pointers.Length);
            if (count < 0) throw new IOException("Steam receive failed.");
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var message = SteamNetworkingMessage_t.FromIntPtr(pointers[i]);
                    if (message.m_cbSize < 1 || message.m_cbSize > 65536) throw new IOException("Invalid multiplayer packet size.");
                    byte[] bytes = new byte[message.m_cbSize];
                    Marshal.Copy(message.m_pData, bytes, 0, bytes.Length);
                    if (!authenticated)
                    {
                        if (!Equal(bytes, hello)) throw new IOException("Local session token does not match.");
                        authenticated = true;
                        State = "Connected (Steam / loopback)";
                    }
                    else
                    {
                        if (packets.Count >= 1024) throw new IOException("The multiplayer receive queue is full.");
                        packets.Enqueue(bytes);
                        Received++;
                    }
                }
            }
            finally { for (int i = 0; i < count; i++) if (pointers[i] != IntPtr.Zero) SteamNetworkingMessage_t.Release(pointers[i]); }
        }

        public static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public bool Send(byte[] bytes)
        {
            if (!Ready) return false;
            SendRaw(bytes);
            Sent++;
            return true;
        }

        private void SendRaw(byte[] bytes)
        {
            var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                long number;
                // legacy Multiplayer uses ReliableWithBuffering; Steam's reliable flag preserves ordering
                EResult result = SteamNetworkingSockets.SendMessageToConnection(connection, pin.AddrOfPinnedObject(), (uint)bytes.Length, 8, out number);
                if (result != EResult.k_EResultOK) throw new IOException("Steam send failed: " + result);
            }
            finally { pin.Free(); }
        }

        public uint Available { get { return packets.Count == 0 ? 0 : (uint)packets.Peek().Length; } }
        public byte[] Read() { return packets.Count == 0 ? null : packets.Dequeue(); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            callback.Dispose();
            if (connection.m_HSteamNetConnection != 0) SteamNetworkingSockets.CloseConnection(connection, 0, "Lab closed", false);
            if (listener.m_HSteamListenSocket != 0) SteamNetworkingSockets.CloseListenSocket(listener);
            packets.Clear();
        }
    }
}
