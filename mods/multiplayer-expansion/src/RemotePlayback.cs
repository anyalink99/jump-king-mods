using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    // render received motion on one clock; physics keeps its own current prediction
    internal sealed class RemotePlayback
    {
        private readonly List<InteractionFrame> history=new List<InteractionFrame>(32);
        private double offset,lastReceive,jitter,cursor,lastSample;
        private bool started;
        internal int Count {get{return history.Count;}}
        internal double Time {get{return cursor;}}
        internal void Accept(InteractionFrame frame,double now)
        {
            var last=history.Count==0 ? null : history[history.Count-1];
            bool reset=last==null || last.Epoch!=frame.Epoch || last.Map!=frame.Map || last.Transition!=frame.Transition
                || last.Presence!=frame.Presence || last.Active!=frame.Active || now-lastReceive>.5
                || Vector2.DistanceSquared(last.Position,frame.Position)>128*128;
            if(reset){history.Clear();offset=now-frame.Time;jitter=0;started=false;}
            else {
                double elapsed=Math.Max(0,now-lastReceive),span=Math.Max(0,frame.Time-last.Time);
                offset=Math.Min(offset+elapsed*.001,now-frame.Time);
                jitter=Math.Max(jitter*.97,Math.Min(.1,Math.Max(0,elapsed-span)));
            }
            history.Add(frame);if(history.Count>32)history.RemoveAt(0);lastReceive=now;
        }
        internal InteractionFrame Sample(double now,bool local)
        {
            if(history.Count==0)return null;
            var last=history[history.Count-1];
            if(!last.Modern || !last.Active || last.Presence!=PlayerPresence.Playing)return last;
            double target=now-offset-(local ? 2.0/60 : .05)-jitter;
            if(!started){
                cursor=history[0].Time;lastSample=now;
                if(target<=cursor)return history[0];
                started=true;
            }else {
                double elapsed=Math.Max(0,now-lastSample);lastSample=Math.Max(lastSample,now);
                // arrival bursts change the playback rate gently, never its position
                double rate=Math.Max(.9,Math.Min(1.1,1+(target-cursor)*4));
                cursor=Math.Min(last.Time,cursor+elapsed*rate);
            }
            cursor=Math.Max(history[0].Time,cursor);
            for(int i=1;i<history.Count;i++){
                var a=history[i-1];var b=history[i];if(cursor>b.Time)continue;
                double span=b.Time-a.Time;if(span<=0)continue;
                float t=(float)Math.Max(0,Math.Min(1,(cursor-a.Time)/span));
                var result=(t>=1 ? b : a).Copy();
                var previous=i>=2 ? history[i-2] : a;
                var next=i+1<history.Count ? history[i+1] : b;
                result.Position=new Vector2(
                    Curve(previous.Time,previous.Position.X,a.Time,a.Position.X,b.Time,b.Position.X,next.Time,next.Position.X,t),
                    Curve(previous.Time,previous.Position.Y,a.Time,a.Position.Y,b.Time,b.Position.Y,next.Time,next.Position.Y,t));
                result.Velocity=Vector2.Lerp(a.Velocity,b.Velocity,t);
                if(a.Support==b.Support && a.SupportEpoch==b.SupportEpoch)result.SupportOffset=Vector2.Lerp(a.SupportOffset,b.SupportOffset,t);
                return result;
            }
            // don't invent a jump, bounce or wall crossing when packets run out
            return last;
        }
        private static float Tangent(double left,double right)
        {return left*right<=0 ? 0 : (float)(2*left*right/(left+right));}
        private static float Curve(double pt,float p,double at,float a,double bt,float b,double nt,float n,float t)
        {
            double span=bt-at,slope=(b-a)/span;
            float m0=pt<at ? Tangent((a-p)/(at-pt),slope) : (float)slope;
            float m1=nt>bt ? Tangent(slope,(n-b)/(nt-bt)) : (float)slope;
            float t2=t*t,t3=t2*t;
            float value=(2*t3-3*t2+1)*a+(t3-2*t2+t)*(float)span*m0+(-2*t3+3*t2)*b+(t3-t2)*(float)span*m1;
            return Math.Max(Math.Min(a,b),Math.Min(Math.Max(a,b),value));
        }
        internal static float ContactWeight(Rectangle local,InteractionFrame remote)
        {
            float dx=Math.Max(0,Math.Max(local.Left-(remote.Position.X+remote.Width),remote.Position.X-local.Right));
            float dy=Math.Max(0,Math.Max(local.Top-(remote.Position.Y+remote.Height),remote.Position.Y-local.Bottom));
            float distance=(float)Math.Sqrt(dx*dx+dy*dy);
            float t=Math.Max(0,Math.Min(1,(48-distance)/44));return t*t*(3-2*t);
        }
        internal static InteractionFrame Blend(InteractionFrame playback,InteractionFrame current,float weight)
        {
            if(weight<=0)return playback;if(weight>=1)return current;
            var result=(weight<.5f ? playback : current).Copy();
            result.Position=Vector2.Lerp(playback.Position,current.Position,weight);
            return result;
        }
    }
}
