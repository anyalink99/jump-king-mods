using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MultiplayerExpansion
{
    internal static class NetworkLab
    {
        private static string session;
        private static DebugPacketLink link;
        private static Task<string> replay;
        private static NetworkConditions cached=new NetworkConditions();
        private static string failure="";
        internal static bool Available {get {return link!=null;}}
        internal static string Status {get {return failure!="" ? failure : replay!=null ? (replay.IsCompleted ? replay.Result : "Checking the last packet recording...") : link==null ? "Start two clients first." : link.Diagnostics;}}
        internal static string ConfigPath {get {return Path.Combine(session,"network-test.txt");}}
        internal static IPacketLink Wrap(IPacketLink inner,string root,int role)
        {session=root;link=new DebugPacketLink(inner,root,role);return link;}
        internal static NetworkConditions Read() {return cached;}
        internal static NetworkConditions Reload()
        {
            try {cached=session!=null && File.Exists(ConfigPath) ? NetworkConditions.Parse(File.ReadAllText(ConfigPath)) ?? cached : new NetworkConditions();}
            catch(IOException) { }
            catch(UnauthorizedAccessException) { }
            return cached;
        }
        internal static void Change(Action<NetworkConditions> change)
        {
            if(!Available) return;
            try
            {
                var value=NetworkConditions.Parse(Reload().Encode());change(value);
                string temp=ConfigPath+"."+Client.Role+".tmp";File.WriteAllText(temp,value.Encode());
                if(File.Exists(ConfigPath)) File.Replace(temp,ConfigPath,null);else File.Move(temp,ConfigPath);
                cached=value;replay=null;failure="";
            }
            catch(IOException e) {failure="Network settings: "+e.Message;}
            catch(UnauthorizedAccessException e) {failure="Network settings: "+e.Message;}
        }
        internal static void VerifyTrace()
        {
            if(!Available || replay!=null && !replay.IsCompleted) return;
            Change(x=>x.Recording=0);
            string path=Path.Combine(session,"client"+Client.Role+".network.trace");
            link.StopRecording();
            replay=Task.Run(delegate {
                try {var first=NetworkReplay.Run(path);var second=NetworkReplay.Run(path);return first.Hash==second.Hash ? "Replay matched: "+first : "Replay mismatch.";}
                catch(Exception error) {return "Replay: "+error.Message;}
            });
        }
        internal static void Detached(DebugPacketLink disposed) {if(ReferenceEquals(link,disposed)) {link=null;session=null;replay=null;failure="";cached=new NetworkConditions();}}
    }

    internal sealed class DebugPacketLink : IPacketLink
    {
        private readonly IPacketLink inner;
        private readonly string root;
        private readonly int role;
        private readonly Stopwatch clock=Stopwatch.StartNew();
        private readonly Queue<byte[]> ready=new Queue<byte[]>();
        private PacketSchedule scheduler;
        private NetworkConditions conditions=new NetworkConditions();
        private NetworkTrace trace;
        private double nextPoll;
        private long recordToken;
        private string error="";
        private int readyBytes;
        internal DebugPacketLink(IPacketLink inner,string root,int role)
        {this.inner=inner;this.root=root;this.role=role;scheduler=new PacketSchedule(conditions.Seed+role);}
        public bool Ready {get {return inner.Ready;}}
        public string State {get {return inner.State;}}
        public long Sent {get {return inner.Sent;}}
        public long Received {get {return inner.Received;}}
        public uint Available {get {return ready.Count==0 ? 0 : (uint)ready.Peek().Length;}}
        internal string Diagnostics {get {return error!="" ? error : "Queued "+scheduler.Count+" | dropped "+scheduler.Dropped+" | duplicates "+scheduler.Duplicated+(trace!=null ? trace.Recording ? " | Recording" : " | Recording limit reached" : "");}}
        public void Pump()
        {
            inner.Pump();double now=clock.Elapsed.TotalSeconds;
            if(now>=nextPoll)
            {
                nextPoll=now+.25;var next=NetworkLab.Reload();
                if(next.Seed!=conditions.Seed) {scheduler=new PacketSchedule(next.Seed+role);ready.Clear();readyBytes=0;}
                conditions=next;
                if(recordToken!=next.Recording)
                {
                    StopRecording();recordToken=next.Recording;
                    if(recordToken!=0)
                        try {trace=new NetworkTrace(Path.Combine(root,"client"+role+".network.trace"),role,now);error="";}
                        catch(IOException e) {error="Recording: "+e.Message;}
                        catch(UnauthorizedAccessException e) {error="Recording: "+e.Message;}
                }
            }
            for(int i=0;i<256 && inner.Available!=0;i++) {var packet=inner.Read();if(packet==null) break;scheduler.Enqueue(packet,now,conditions);}
            for(int i=0;i<256;i++)
            {
                var packet=scheduler.Take(now);if(packet==null) break;
                if(ready.Count>=512 || readyBytes+packet.Length>4*1024*1024) {scheduler.Dropped++;continue;}
                ready.Enqueue(packet);readyBytes+=packet.Length;
            }
        }
        private void Record(bool outgoing,byte[] bytes)
        {
            if(trace==null) return;
            try {trace.Record(outgoing,bytes,clock.Elapsed.TotalSeconds);}
            catch(IOException e) {error="Recording: "+e.Message;StopRecording();}
        }
        public byte[] Read()
        {if(ready.Count==0) return null;var packet=ready.Dequeue();readyBytes-=packet.Length;Record(false,packet);return packet;}
        public bool Send(byte[] bytes) {bool sent=inner.Send(bytes);if(sent) Record(true,bytes);return sent;}
        internal void StopRecording()
        {
            if(trace==null) return;
            try {trace.Dispose();}catch(IOException e) {error="Recording: "+e.Message;}finally {trace=null;}
        }
        public void Dispose() {try {StopRecording();}finally {inner.Dispose();scheduler.Clear();ready.Clear();NetworkLab.Detached(this);}}
    }
}
