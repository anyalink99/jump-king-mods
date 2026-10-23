using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal sealed class NetworkTrace : IDisposable
    {
        private StreamWriter writer;
        private readonly double started;
        private int bytes;
        private double flushed;
        internal bool Recording {get {return writer!=null;}}
        internal NetworkTrace(string path,int role,double now)
        {
            // only rotate the three trace files owned by this recorder
            if(File.Exists(path+".2")) File.Delete(path+".2");
            if(File.Exists(path+".1")) File.Move(path+".1",path+".2");
            if(File.Exists(path)) File.Move(path,path+".1");
            writer=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false));
            writer.WriteLine("MPEXTRACE1 "+role);writer.Flush();started=now;
        }
        internal void Record(bool outgoing,byte[] packet,double now)
        {
            if(writer==null || !InteractionWire.Recognizes(packet) || WorldWire.Recognizes(packet) || WorldInputs.Recognizes(packet) || PlayerActionWire.Recognizes(packet)) return;
            if(now-started>120 || bytes+packet.Length*2>4*1024*1024) {Dispose();return;}
            string line=(now-started).ToString("R",CultureInfo.InvariantCulture)+" "+(outgoing ? "T" : "R")+" "+Convert.ToBase64String(packet);
            writer.WriteLine(line);bytes+=line.Length+2;
            if(now-flushed>=.5) {writer.Flush();flushed=now;}
        }
        public void Dispose() {var closing=writer;writer=null;if(closing!=null) closing.Dispose();}
    }

    internal sealed class ReplayReport
    {
        internal int Records, Accepted, Rejected;
        internal string Hash;
        public override string ToString() {return Records+" packets; "+Rejected+" rejected; "+Hash.Substring(0,12);}
    }
    internal static class NetworkReplay
    {
        internal static ReplayReport Run(string path)
        {
            if(new FileInfo(path).Length>4*1024*1024+1024) throw new IOException("Trace exceeds the recording limit.");
            var report=new ReplayReport();var peers=new Dictionary<ulong,InteractionPeer>();double previous=-1;
            using(var reader=new StreamReader(path))
            using(var hash=SHA256.Create())
            {
                string header=reader.ReadLine();int role;
                if(header==null || !header.StartsWith("MPEXTRACE1 ") || !int.TryParse(header.Substring(11),out role) || role<1 || role>2) throw new IOException("Unsupported network trace.");
                string line;
                while((line=reader.ReadLine())!=null)
                {
                    if(line.Length>4096 || report.Records>=50000) throw new IOException("Invalid trace record size.");
                    var fields=line.Split(' ');double now;
                    if(fields.Length!=3 || !double.TryParse(fields[0],NumberStyles.Float,CultureInfo.InvariantCulture,out now) || double.IsNaN(now) || double.IsInfinity(now) || now<previous || now>121 || (fields[1]!="T" && fields[1]!="R")) throw new IOException("Invalid trace timestamp or direction.");
                    previous=now;report.Records++;
                    byte[] packet=Convert.FromBase64String(fields[2]);InteractionFrame frame;
                    ulong source=(ulong)(fields[1]=="T" ? role : 3-role);
                    InteractionPeer peer;if(!peers.TryGetValue(source,out peer)) {peer=new InteractionPeer{Id=source};peers.Add(source,peer);}
                    bool accepted=InteractionWire.Decode(packet,out frame) && peer.Accept(frame,now);
                    if(accepted) report.Accepted++;else report.Rejected++;
                    if(peer.Frame!=null) peer.Prepare(now,false);
                    var state=peer.Sample;
                    string value=source+":"+accepted+":"+(state==null ? "none" : state.Epoch+":"+state.Sequence+":"+(byte)state.Presence+":"+state.Position.X.ToString("R",CultureInfo.InvariantCulture)+":"+state.Position.Y.ToString("R",CultureInfo.InvariantCulture));
                    byte[] data=Encoding.UTF8.GetBytes(value+"\n");hash.TransformBlock(data,0,data.Length,data,0);
                }
                hash.TransformFinalBlock(new byte[0],0,0);report.Hash=BitConverter.ToString(hash.Hash).Replace("-","");
            }
            return report;
        }
    }
}
