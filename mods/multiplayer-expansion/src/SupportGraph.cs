using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
namespace MultiplayerExpansion
{
    internal static class SupportGraph
    {
        internal static InteractionFrame Resolve(ulong id,Func<ulong,InteractionFrame> lookup)
        { return Resolve(id,lookup,new HashSet<ulong>()); }
        private static InteractionFrame Resolve(ulong id,Func<ulong,InteractionFrame> lookup,HashSet<ulong> visiting)
        {
            var frame=lookup(id);if(frame==null) return null;
            if(!frame.Physics || !frame.Active || frame.Presence==PlayerPresence.Paused || frame.Support==0) return frame;
            if(!visiting.Add(id) || visiting.Count>8) return null;
            var parent=Resolve(frame.Support,lookup,visiting);visiting.Remove(id);
            if(parent==null) return null;
            if(!parent.Active || parent.Epoch!=frame.SupportEpoch || parent.Map!=frame.Map || parent.RulesEpoch!=frame.RulesEpoch || parent.Revision!=frame.Revision || parent.Rules!=frame.Rules) return frame;
            if(frame.SupportOffset.X<=-frame.Width || frame.SupportOffset.X>=parent.Width || Math.Abs(frame.SupportOffset.Y+frame.Height)>.5f) return frame;
            var result=frame.Copy();result.Position=JKRuntime.Geometry.MapTopology.Normalize(parent.Position+frame.SupportOffset,parent.Position+new Vector2(parent.Width/2f,parent.Height/2f),frame.Width);
            return result;
        }
    }
}
