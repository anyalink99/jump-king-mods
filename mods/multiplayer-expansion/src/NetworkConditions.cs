using System;
using System.Collections.Generic;
using System.Globalization;

namespace MultiplayerExpansion
{
    internal sealed class NetworkConditions
    {
        internal int Delay, Jitter, Loss, Duplicate, Seed=173;
        internal long Recording;
        internal string Encode() {return string.Join(" ",new[]{Delay.ToString(),Jitter.ToString(),Loss.ToString(),Duplicate.ToString(),Seed.ToString(),Recording.ToString()});}
        internal static NetworkConditions Parse(string text)
        {
            var parts=text.Trim().Split(' ');var result=new NetworkConditions();
            if(parts.Length!=6 || !int.TryParse(parts[0],out result.Delay) || !int.TryParse(parts[1],out result.Jitter)
                || !int.TryParse(parts[2],out result.Loss) || !int.TryParse(parts[3],out result.Duplicate)
                || !int.TryParse(parts[4],out result.Seed) || !long.TryParse(parts[5],NumberStyles.Integer,CultureInfo.InvariantCulture,out result.Recording)
                || result.Delay<0 || result.Delay>500 || result.Jitter<0 || result.Jitter>300 || result.Loss<0 || result.Loss>50
                || result.Duplicate<0 || result.Duplicate>25 || result.Seed<1 || result.Recording<0) return null;
            return result;
        }
    }

    internal sealed class PacketSchedule
    {
        private sealed class Packet {internal byte[] Bytes;internal double Due;internal long Order;}
        private readonly List<Packet> pending=new List<Packet>();
        private uint random;
        private long order;
        private int bytes;
        internal long Dropped, Duplicated, Delivered;
        internal int Count {get {return pending.Count;}}
        internal PacketSchedule(int seed) {random=(uint)Math.Max(1,seed);}
        private double Next() {random^=random<<13;random^=random>>17;random^=random<<5;return random/4294967296.0;}
        internal void Clear() {pending.Clear();bytes=0;}
        internal void Enqueue(byte[] packet,double now,NetworkConditions conditions)
        {
            if(packet==null || packet.Length==0 || packet.Length>65536) {Dropped++;return;}
            if(Next()*100<conditions.Loss) {Dropped++;return;}
            Add(packet,now,conditions);
            if(Next()*100<conditions.Duplicate) {Duplicated++;Add(packet,now,conditions);}
        }
        private void Add(byte[] packet,double now,NetworkConditions conditions)
        {
            if(pending.Count>=512 || bytes+packet.Length>4*1024*1024) {Dropped++;return;}
            // independent jitter can overtake an earlier packet, just like an unordered link
            double delay=Math.Max(0,conditions.Delay+(Next()*2-1)*conditions.Jitter)/1000;
            pending.Add(new Packet{Bytes=packet,Due=now+delay,Order=++order});bytes+=packet.Length;
        }
        internal byte[] Take(double now)
        {
            int best=-1;
            for(int i=0;i<pending.Count;i++)
                if(pending[i].Due<=now && (best<0 || pending[i].Due<pending[best].Due || pending[i].Due==pending[best].Due && pending[i].Order<pending[best].Order)) best=i;
            if(best<0) return null;
            var result=pending[best];pending.RemoveAt(best);bytes-=result.Bytes.Length;Delivered++;return result.Bytes;
        }
    }
}
