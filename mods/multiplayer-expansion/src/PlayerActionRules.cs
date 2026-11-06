using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class PlayerActionRules
    {
        internal const float Range=72, VerticalRange=38;
        internal const double Cooldown=.55;
        internal static Vector2 Center(InteractionFrame frame) { return frame.Position+new Vector2(frame.Width*.5f,frame.Height*.5f); }
        internal static bool Reachable(InteractionFrame source,InteractionFrame target,int direction,Func<Rectangle,bool> blocked)
        {
            if(Math.Abs(direction)!=1 || source==null || target==null || !source.Active || !target.Active || source.Presence!=PlayerPresence.Playing || target.Presence!=PlayerPresence.Playing ||
                !source.Physics || !target.Physics || source.Map!=target.Map || source.RulesEpoch!=target.RulesEpoch || source.Revision!=target.Revision) return false;
            Vector2 start=Center(source),end=Center(target),delta=end-start;
            if(delta.X*direction<0 || Math.Abs(delta.X)>(source.Width+target.Width)*.5f+Range || Math.Abs(delta.Y)>VerticalRange) return false;
            int steps=Math.Max(1,(int)Math.Ceiling(delta.Length()/2));
            for(int i=1;i<steps;i++) {var p=start+delta*((float)i/steps);if(blocked(new Rectangle((int)p.X,(int)p.Y,1,1))) return false;}
            return true;
        }
        internal static Vector2 Impulse(int direction) { return new Vector2(direction*6,-3); }
    }
    internal sealed class KickAuthority
    {
        private sealed class Actor { internal ulong Epoch,Serial;internal double Last=-10; }
        private readonly Dictionary<ulong,Actor> actors=new Dictionary<ulong,Actor>();
        internal void Reset() { actors.Clear(); }
        internal bool Accept(PlayerActionPacket request,InteractionFrame source,InteractionFrame target,double age,double now,Func<Rectangle,bool> blocked)
        {
            if(request==null || request.Kind!=PlayerActionKind.KickRequest || request.Serial==0 || request.Actor==0 || request.Actor==request.Target ||
                source==null || target==null || double.IsNaN(age) || double.IsInfinity(age) || age<0 || age>.35 || request.Map!=source.Map ||
                request.Session!=source.RulesEpoch || request.Revision!=source.Revision || request.ActorEpoch!=source.Epoch || request.ActorTransition!=source.Transition ||
                request.TargetEpoch!=target.Epoch || request.TargetTransition!=target.Transition || !PlayerActionRules.Reachable(source,target,request.Direction,blocked)) return false;
            Actor actor;
            if(!actors.TryGetValue(request.Actor,out actor)) {
                if(actors.Count>=64) return false;
                actors[request.Actor]=actor=new Actor();
            }
            if(actor.Epoch!=source.Epoch) {actor.Epoch=source.Epoch;actor.Serial=0;actor.Last=-10;}
            if(request.Serial<=actor.Serial) return false;
            actor.Serial=request.Serial;
            if(now-actor.Last<PlayerActionRules.Cooldown) return false;
            actor.Last=now;return true;
        }
    }
    internal sealed class ActionReplayWindow
    {
        private readonly HashSet<ulong> seen=new HashSet<ulong>();
        private ulong newest;
        internal void Reset() {seen.Clear();newest=0;}
        internal bool Accept(ulong serial)
        {
            if(serial==0 || newest>serial && newest-serial>=64 || !seen.Add(serial)) return false;
            newest=Math.Max(newest,serial);seen.RemoveWhere(x=>newest>x && newest-x>=64);return true;
        }
    }
}
