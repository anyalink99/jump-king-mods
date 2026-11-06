using System;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal sealed class VisualCorrection
    {
        private Vector2 offset;
        private double updated;
        internal long Corrections, Snaps;
        internal float Error { get { return offset.Length(); } }
        internal void Reset(double now) {offset=Vector2.Zero;updated=now;}
        private void Advance(double now)
        {
            double elapsed=Math.Max(0,now-updated);updated=now;
            offset*=(float)Math.Exp(-elapsed/.045);
            if(offset.LengthSquared()<.0001f) offset=Vector2.Zero;
        }
        internal void Correct(InteractionFrame previous,InteractionFrame next,double now,bool continuous)
        {
            Advance(now);
            if(!continuous || previous==null || next==null || !next.Active || next.Presence!=PlayerPresence.Playing)
            {Reset(now);return;}
            Vector2 error=previous.Position-next.Position;
            if(error.LengthSquared()>32*32) {Snaps++;Reset(now);return;}
            offset+=error;
            // a small visual correction mustn't turn into a second collision position
            if(offset.LengthSquared()>8*8) offset=Vector2.Normalize(offset)*8;
            if(next.Grounded || next.Support!=0) offset.Y=0;
            if(error.LengthSquared()>.01f) Corrections++;
        }
        internal InteractionFrame Apply(InteractionFrame frame,double now,bool contact)
        {
            Advance(now);
            if(frame==null || contact || frame.Presence!=PlayerPresence.Playing) {Reset(now);return frame;}
            var result=frame.Copy();result.Position+=offset;return result;
        }
    }
}
