using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    // stage confirmed momentum until the next native step, never in the receive callback
    internal sealed class PlayerActionInbox
    {
        private sealed class Pending {internal PlayerActionPacket Packet;internal double Received;}
        private readonly List<Pending> items=new List<Pending>();
        internal bool HasPending {get{return items.Count!=0;}}
        internal void Clear() {items.Clear();}
        internal void Add(PlayerActionPacket packet,double now)
        {if(items.Count<32) items.Add(new Pending{Packet=packet.Copy(),Received=now});}
        internal Vector2 Drain(ulong self,ulong session,ulong revision,ulong policy,InteractionFrame target,double now,bool enabled)
        {
            Vector2 result=Vector2.Zero;
            if(enabled && target!=null && target.Active && target.Presence==PlayerPresence.Playing)
                foreach(var item in items) {
                    var packet=item.Packet;
                    if(now<item.Received || now-item.Received>.45 || packet.Target!=self || packet.Map!=target.Map || packet.Policy!=policy || packet.Session!=session || packet.Revision!=revision ||
                        packet.TargetEpoch!=target.Epoch || packet.TargetTransition!=target.Transition) continue;
                    result+=PlayerActionRules.Impulse(packet.Direction);
                }
            items.Clear();return result;
        }
    }
}
