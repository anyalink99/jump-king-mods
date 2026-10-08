using System;
using System.Diagnostics;
using System.Threading;

namespace MultiplayerExpansion
{
    internal static class LocalTransportTests
    {
        internal static void Run()
        {
            string token = Guid.NewGuid().ToString("N");
            using (var first = new LocalLink(true, token))
            using (var second = new LocalLink(false, token))
            {
                var timeout = Stopwatch.StartNew();
                while (!first.Ready || !second.Ready)
                { if (timeout.ElapsedMilliseconds > 5000) throw new Exception("Local handshake timed out: " + first.State + " / " + second.State); Thread.Sleep(5); }
                for (int packet = 0; packet < 20; packet++)
                {
                    byte[] bytes = new byte[4096];
                    for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)((i + packet) % 251);
                    if (!first.Send(bytes) || !second.Send(bytes)) throw new Exception("Local send failed.");
                }
                while (first.Received < 20 || second.Received < 20)
                { if (timeout.ElapsedMilliseconds > 5000) throw new Exception("Local receive timed out."); Thread.Sleep(5); }
                for (int packet = 0; packet < 20; packet++)
                    foreach (var link in new[] { first, second })
                    {
                        byte[] bytes = link.Read();
                        if (bytes.Length != 4096) throw new Exception("Local framing changed.");
                        for (int i = 0; i < bytes.Length; i++) if (bytes[i] != (byte)((i + packet) % 251)) throw new Exception("Local payload or ordering changed.");
                    }
            }
            Console.WriteLine("[OK] Local transport: authenticated named pipe, exact bidirectional packets and ordering");
        }
    }
}
