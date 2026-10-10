using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    // sender time keeps burst delivery from turning into a burst of movement
    internal sealed class RemoteMotion
    {
        private readonly List<InteractionFrame> history = new List<InteractionFrame>(32);
        private double offset, lastReceive, cursor;
        private bool started;
        internal int Count { get { return history.Count; } }
        internal void Accept(InteractionFrame frame, double now)
        {
            var last = history.Count == 0 ? null : history[history.Count - 1];
            bool reset = last == null || last.Epoch != frame.Epoch || last.Map != frame.Map || last.Active != frame.Active || last.Presence!=frame.Presence || last.Transition!=frame.Transition
                || now - lastReceive > .5 || Vector2.DistanceSquared(last.Position, frame.Position) > 128 * 128;
            if (reset) { history.Clear(); offset = now - frame.Time; started = false; }
            else offset = Math.Min(offset + Math.Max(0, now - lastReceive) * .001, now - frame.Time);
            history.Add(frame); if (history.Count > 32) history.RemoveAt(0);
            lastReceive = now;
        }
        internal InteractionFrame Sample(double now, bool local)
        {
            if (history.Count == 0) return null;
            var last = history[history.Count - 1];
            if (!last.Modern || !last.Active) return last;
            // local needs just one frame of buffering; internet absorbs three frames of jitter
            double target = now - offset - (local ? 1.0 / 60 : .05);
            cursor = started ? Math.Max(cursor, target) : target; started = true;
            if (cursor <= history[0].Time) return history[0];
            for (int i = 1; i < history.Count; i++)
            {
                var a = history[i - 1]; var b = history[i];
                if (cursor > b.Time) continue;
                double span = b.Time - a.Time;
                float t = span <= 0 ? 1 : (float)((cursor - a.Time) / span);
                var result = a.Copy();
                result.Position = Vector2.Lerp(a.Position, b.Position, t);
                result.Velocity = Vector2.Lerp(a.Velocity, b.Velocity, t);
                return result;
            }
            var predicted = last.Copy();
            // use observed displacement, not walking intent against a wall
            if (history.Count > 1)
            {
                var previous = history[history.Count - 2]; double dt = last.Time - previous.Time;
                if (dt > .001 && dt < .15)
                {
                    Vector2 speed = (last.Position - previous.Position) / (float)dt;
                    predicted.Position += speed * (float)Math.Min(.033333, Math.Max(0, cursor - last.Time));
                }
            }
            return predicted;
        }
        internal InteractionFrame Predict(double now,double oneWay)
        {
            if(history.Count==0) return null;
            var last=history[history.Count-1];var result=last.Copy();
            if(!last.Active || last.Presence==PlayerPresence.Paused) return result;
            double ahead=Math.Min(.1,Math.Max(0,now-lastReceive)+Math.Min(.1,oneWay));
            Vector2 speed=last.Velocity;
            float acceleration=0;
            if(last.Grounded || last.Support!=0) speed.Y=0;
            if(history.Count>1)
            {
                var previous=history[history.Count-2];double dt=last.Time-previous.Time;
                if(dt>.001 && dt<.15)
                {
                    Vector2 observed=(last.Position-previous.Position)/(float)(dt*60);
                    // intent against a wall isn't actual motion; a fresh reversal still takes effect
                    if(Math.Abs(observed.X)<.1f && Math.Abs(previous.Velocity.X)>.1f) speed.X=0;
                    if(!last.Grounded && last.Support==0 && Math.Sign(observed.Y)==Math.Sign(speed.Y))
                        speed.Y=Math.Abs(observed.Y)<Math.Abs(speed.Y) ? observed.Y : speed.Y;
                    if(!last.Grounded && !previous.Grounded && last.Support==0 && previous.Support==0 && last.Jump==previous.Jump)
                    { float measured=(last.Velocity.Y-previous.Velocity.Y)/(float)(dt*60);if(measured>=0 && measured<=2) acceleration=measured; }
                }
            }
            float steps=(float)(ahead*60);
            result.Position+=speed*steps+new Vector2(0,.5f*acceleration*steps*Math.Max(0,steps-1));
            return result;
        }
    }
}
