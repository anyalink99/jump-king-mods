using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    // choose which body can yield before recovery or contact correction gets a say
    internal sealed class ContactConstraints
    {
        private sealed class Edge
        {
            internal ulong Epoch, Map;
            internal int Side;
            internal Vector2 Received;
            internal readonly InteractionPeer Proxy=new InteractionPeer();
        }
        private readonly Dictionary<ulong,Edge> edges=new Dictionary<ulong,Edge>();
        private readonly List<InteractionPeer> filtered=new List<InteractionPeer>();
        private Vector2 previousLocal;
        private bool hasLocal;
        internal void Reset() { edges.Clear();filtered.Clear();hasLocal=false; }
        internal void Forget(ulong id) { edges.Remove(id); }
        internal int Side(ulong id,InteractionFrame frame)
        { Edge edge;return edges.TryGetValue(id,out edge) && edge.Epoch==frame.Epoch && edge.Map==frame.Map ? edge.Side : 0; }
        internal void Finish(Vector2 position) { previousLocal=position;hasLocal=true; }
        internal IList<InteractionPeer> Prepare(ContactBody body,IList<InteractionPeer> peers,ulong support,ulong self,InteractionRules rules)
        {
            if(hasLocal && Vector2.DistanceSquared(previousLocal,body.Before)>4096) edges.Clear();
            filtered.Clear();
            foreach(var peer in peers)
            {
                var frame=peer.Frame;var sample=peer.Sample ?? frame;
                if(body.NativeGrounded && sample.Physics && !sample.Anchored && peer.Id!=support)
                {edges.Remove(peer.Id);continue;}
                Edge edge;edges.TryGetValue(peer.Id,out edge);
                bool walking=(rules & InteractionRules.Push)==0 && body.NativeGrounded && sample.Physics && sample.Anchored
                    && peer.Id!=support && sample.Support!=self
                    && body.Before.Y+body.Height>sample.Position.Y+1 && body.Before.Y<sample.Position.Y+sample.Height-1;
                if(!walking) {edges.Remove(peer.Id);filtered.Add(peer);continue;}
                if(edge!=null && (edge.Epoch!=frame.Epoch || edge.Map!=frame.Map || Vector2.DistanceSquared(edge.Received,frame.Position)>4096))
                {edges.Remove(peer.Id);edge=null;}
                if(edge==null)
                {
                    int side=body.Before.X+body.Width<=frame.Position.X+.01f ? -1 : body.Before.X>=frame.Position.X+frame.Width-.01f ? 1 : 0;
                    // an already overlapping spawn has no established side; recovery still owns it
                    if(side!=0) {edge=new Edge{Epoch=frame.Epoch,Map=frame.Map,Side=side};edges[peer.Id]=edge;}
                }
                if(edge==null) {filtered.Add(peer);continue;}
                edge.Received=frame.Position;
                float boundary=edge.Side<0 ? body.Before.X+body.Width : body.Before.X-sample.Width;
                bool intrudes=edge.Side<0 ? sample.Position.X<boundary : sample.Position.X>boundary;
                if(!intrudes) {filtered.Add(peer);continue;}
                var contact=sample.Copy();contact.Position.X=boundary;
                // keep the network snapshot intact for rendering and later samples
                edge.Proxy.Id=peer.Id;edge.Proxy.Frame=frame;edge.Proxy.Sample=contact;edge.Proxy.Received=peer.Received;
                filtered.Add(edge.Proxy);
            }
            return filtered;
        }
    }
}
