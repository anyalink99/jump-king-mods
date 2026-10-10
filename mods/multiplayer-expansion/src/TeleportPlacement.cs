using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class TeleportPlacement
    {
        internal static bool Find(InteractionFrame target,int width,int height,IList<InteractionPeer> peers,ulong self,Func<Rectangle,bool> blocked,out Vector2 position)
        {
            position=Vector2.Zero;
            if(target==null || !target.Active || width<=0 || height<=0) return false;
            var trial=new ContactBody{Position=target.Position+new Vector2((target.Width-width)*.5f,target.Height-height),Width=width,Height=height};
            if(!ContactRecovery.Separate(trial,peers,self,blocked) || blocked(PlayerContacts.Bounds(trial.Position,width,height))) return false;
            position=trial.Position;return true;
        }
    }
}
