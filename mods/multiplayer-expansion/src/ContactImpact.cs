using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
namespace MultiplayerExpansion
{
    internal sealed class ContactImpact
    {
        internal static readonly ContactImpact[] None=new ContactImpact[0];
        internal ulong Id, Target, TargetEpoch, TargetJump, TargetTransition;
        internal double Time;
        internal Vector2 Normal, Incoming, Outgoing;
    }
    internal sealed class ContactLedger
    {
        private sealed class Prediction { internal double Time; internal Vector2 Normal, Outgoing; }
        private readonly Dictionary<ulong,Prediction> predicted=new Dictionary<ulong,Prediction>();
        private readonly Dictionary<ulong,ulong> consumed=new Dictionary<ulong,ulong>();
        private readonly List<ContactImpact> outgoing=new List<ContactImpact>();
        private ulong serial;
        private ContactImpact[] snapshot=ContactImpact.None;
        private bool dirty;
        // pause and rule changes clear work, not event identity or replay protection
        internal void Reset() { predicted.Clear();outgoing.Clear();snapshot=ContactImpact.None;dirty=false; }
        internal void ResetSession() { Reset();consumed.Clear();serial=0; }
        internal void Forget(ulong peer) { predicted.Remove(peer);consumed.Remove(peer);if(outgoing.RemoveAll(x=>x.Target==peer)>0) dirty=true; }
        internal void Record(ulong self,InteractionPeer peer,Vector2 normal,Vector2 localAfter,Vector2 remoteAfter,double now)
        {
            Prediction previous;
            if(predicted.TryGetValue(peer.Id,out previous) && now-previous.Time<.12 && previous.Normal==normal) return;
            predicted[peer.Id]=new Prediction{Time=now,Normal=normal,Outgoing=localAfter};
            if(self>=peer.Id) return;
            outgoing.Add(new ContactImpact{Id=++serial,Target=peer.Id,TargetEpoch=peer.Frame.Epoch,TargetJump=peer.Frame.Jump,TargetTransition=peer.Frame.Transition,Time=now,Normal=-normal,Incoming=(peer.Sample ?? peer.Frame).Velocity,Outgoing=remoteAfter});
            if(outgoing.Count>8) outgoing.RemoveAt(0);
            dirty=true;
        }
        internal ContactImpact[] Pending(double now)
        {
            for(int i=outgoing.Count-1;i>=0;i--) if(now-outgoing[i].Time>.4) {outgoing.RemoveAt(i);dirty=true;}
            if(dirty) {snapshot=outgoing.Count==0 ? ContactImpact.None : outgoing.ToArray();dirty=false;}
            return snapshot;
        }
        internal bool Confirm(ulong source,ulong self,ulong epoch,ulong jump,ContactImpact hit,double age,double now,bool grounded,ref Vector2 velocity)
        {
            bool firstImpact;
            return Confirm(source,self,epoch,jump,hit,age,now,grounded,ref velocity,out firstImpact);
        }
        internal bool Confirm(ulong source,ulong self,ulong epoch,ulong jump,ContactImpact hit,double age,double now,bool grounded,ref Vector2 velocity,out bool firstImpact)
        {
            firstImpact=false;
            if(source>=self || hit.Target!=self || hit.TargetEpoch!=epoch || hit.TargetJump!=jump || age<0 || age>.25) return false;
            ulong last;if(consumed.TryGetValue(source,out last) && hit.Id<=last) return false;
            consumed[source]=hit.Id;
            if(grounded) return false;
            Prediction local;float current=Vector2.Dot(velocity,hit.Normal),desired=Vector2.Dot(hit.Outgoing,hit.Normal);
            if(predicted.TryGetValue(source,out local) && now-local.Time<.25 && local.Normal==hit.Normal)
                velocity+=hit.Normal*(desired-Vector2.Dot(local.Outgoing,hit.Normal));
            else if(current<0) {velocity+=hit.Normal*(desired-current);firstImpact=true;}
            else return false;
            return true;
        }
    }
}
