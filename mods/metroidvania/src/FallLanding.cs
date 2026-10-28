using System;
using Microsoft.Xna.Framework;

namespace Metroidvania
{
    internal static class FallRules
    {
        // Quest's shipped Fall Damage Settings uses 0.08; keep the percentage as integers
        internal static int Damage(int maximumHealth){return (maximumHealth*8+99)/100;}
    }
    // only observes the two native collision boundaries; no height scan or extra physics step
    internal sealed class FallLanding
    {
        private Vector2 previous;
        private long epoch;
        private bool known,beforeValid,wasGrounded,fast,suppress;
        private ulong serial,pending;
        internal void Synchronize(Vector2 position,long restoreEpoch)
        {
            if(known&&epoch==restoreEpoch)return;
            previous=position;epoch=restoreEpoch;known=true;beforeValid=false;pending=0;suppress=true;
        }
        internal void Before(Vector2 position,bool grounded,float velocity,float terminal,long restoreEpoch,bool teleported)
        {
            Synchronize(position,restoreEpoch);
            beforeValid=!suppress&&!teleported&&Vector2.DistanceSquared(position,previous)<=128*128;suppress=false;
            if(!beforeValid)pending=0;
            previous=position;wasGrounded=grounded;fast=!float.IsNaN(velocity)&&!float.IsInfinity(velocity)&&velocity>=terminal;
        }
        internal void After(Vector2 position,bool grounded,long restoreEpoch,bool teleported)
        {
            if(restoreEpoch!=epoch){Synchronize(position,restoreEpoch);return;}
            if(teleported||Vector2.DistanceSquared(position,previous)>128*128){Cut(position,restoreEpoch);return;}
            if(beforeValid&&!teleported&&!wasGrounded&&grounded&&fast&&pending==0)pending=++serial;
            previous=position;beforeValid=false;
        }
        internal void Suspend(){beforeValid=false;}
        internal void Cut(Vector2 position,long restoreEpoch){known=false;Synchronize(position,restoreEpoch);}
        internal ulong Take(){ulong value=pending;pending=0;return value;}
    }
}
