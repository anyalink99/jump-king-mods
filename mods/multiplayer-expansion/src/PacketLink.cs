using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace MultiplayerExpansion
{
    internal interface IPacketLink : IDisposable
    {
        bool Ready { get; }
        string State { get; }
        long Sent { get; }
        long Received { get; }
        uint Available { get; }
        void Pump();
        bool Send(byte[] bytes);
        byte[] Read();
    }

    internal sealed class LocalLink : IPacketLink
    {
        private readonly PipeStream pipe;
        private readonly Thread reader;
        private Thread writer;
        private readonly AutoResetEvent outgoing = new AutoResetEvent(false);
        private readonly Queue<byte[]> incoming = new Queue<byte[]>(), pending = new Queue<byte[]>();
        private readonly object sync = new object();
        private volatile bool ready, disposed;
        private volatile string state = "Connecting locally";
        private long sent, received;
        public bool Ready { get { return ready; } }
        public string State { get { return state; } }
        public long Sent { get { return Interlocked.Read(ref sent); } }
        public long Received { get { return Interlocked.Read(ref received); } }
        public uint Available { get { lock (sync) return incoming.Count == 0 ? 0 : (uint)incoming.Peek().Length; } }

        internal LocalLink(bool server, string token)
        {
            string name = "MPEX-" + token;
            pipe = server ? (PipeStream)new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536)
                : new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            reader = new Thread(delegate() { Receive(server, token); }) { IsBackground = true, Name = "Multiplayer local receive" };
            reader.Start();
        }
        private void Receive(bool server, string token)
        {
            try
            {
                if (server) ((NamedPipeServerStream)pipe).WaitForConnection();
                else ((NamedPipeClientStream)pipe).Connect(60000);
                byte[] hello = Encoding.ASCII.GetBytes("MPEX/1:" + token);
                WritePacket(hello);
                if (!SteamLink.Equal(ReadPacket(), hello)) throw new IOException("Local session token does not match.");
                ready = true; state = "Connected (Local)";
                writer = new Thread(WritePending) { IsBackground = true, Name = "Multiplayer local send" };
                writer.Start();
                while (!disposed)
                {
                    byte[] packet = ReadPacket();
                    lock (sync)
                    {
                        if (incoming.Count >= 1024) throw new IOException("Local receive queue is full.");
                        incoming.Enqueue(packet);
                    }
                    Interlocked.Increment(ref received);
                }
            }
            catch (Exception error) { Fail(error); }
        }
        private byte[] ReadPacket()
        {
            byte[] header = ReadExactly(4);
            int count = BitConverter.ToInt32(header, 0);
            if (count < 1 || count > 65536) throw new IOException("Invalid local packet size.");
            return ReadExactly(count);
        }
        private byte[] ReadExactly(int count)
        {
            byte[] bytes = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = pipe.Read(bytes, offset, count - offset);
                if (read == 0) throw new EndOfStreamException("Peer closed the local connection.");
                offset += read;
            }
            return bytes;
        }
        private void WritePacket(byte[] bytes)
        { byte[] header = BitConverter.GetBytes(bytes.Length); pipe.Write(header, 0, 4); pipe.Write(bytes, 0, bytes.Length); pipe.Flush(); }
        private void WritePending()
        {
            try
            {
                while (!disposed)
                {
                    byte[] packet = null;
                    lock (sync) if (pending.Count != 0) packet = pending.Dequeue();
                    if (packet == null) { outgoing.WaitOne(100); continue; }
                    WritePacket(packet);
                }
            }
            catch (Exception error) { Fail(error); }
        }
        private void Fail(Exception error)
        { ready = false; if (!disposed) state = "Disconnected: " + error.Message; }
        public bool Send(byte[] bytes)
        {
            if (!ready || disposed) return false;
            if (bytes == null || bytes.Length == 0 || bytes.Length > 65536) throw new IOException("Invalid local packet size.");
            lock (sync)
            {
                if (pending.Count >= 1024) throw new IOException("Local send queue is full.");
                pending.Enqueue((byte[])bytes.Clone());
            }
            Interlocked.Increment(ref sent); outgoing.Set(); return true;
        }
        public byte[] Read() { lock (sync) return incoming.Count == 0 ? null : incoming.Dequeue(); }
        public void Pump() { }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; ready = false; outgoing.Set(); pipe.Dispose();
            reader.Join(1000);
            if (writer != null) writer.Join(1000);
            outgoing.Dispose();
        }
    }
}
