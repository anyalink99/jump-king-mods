using System;
using JKRuntime.Presentation;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    // regular tracking keeps running underneath a shot, including its return target
    internal sealed class ScriptedView
    {
        internal bool Active {get;private set;}
        internal float X {get;private set;}
        internal float Y {get;private set;}
        private long id;
        private bool returning;
        private Vector2 from;
        private double age,duration,returnDuration;
        private int screen;
        internal void Reset(){id=0;Active=returning=false;age=0;}
        internal bool Advance(CameraShot? request,Vector2 normal,Vector2 visible,double delta,bool paused,Func<int,int,bool> connected,int nativeScreen)
        {
            if(request.HasValue)
            {
                var shot=request.Value;
                if(!Active||returning||id!=shot.Id)
                {
                    from=Active?new Vector2(X,Y):visible;age=0;duration=shot.Smooth&&connected(Active?screen:nativeScreen,shot.Screen)?shot.Duration:0;
                    id=shot.Id;returnDuration=shot.ReturnDuration;screen=shot.Screen;returning=false;Active=true;
                }
                Move(new Vector2(240-shot.Center.X,180-shot.Center.Y),delta,paused);return true;
            }
            if(!Active)return false;
            if(!returning)
            {from=new Vector2(X,Y);age=0;duration=connected(screen,nativeScreen)?returnDuration:0;returning=true;}
            Move(normal,delta,paused);
            if(duration==0||age>=duration){Reset();return false;}return true;
        }
        private void Move(Vector2 target,double delta,bool paused)
        {
            if(!paused)age+=Math.Min(.1,Math.Max(0,delta));
            double t=duration<=0?1:Math.Min(1,age/duration);t=t*t*(3-2*t);
            var point=Vector2.Lerp(from,target,(float)t);X=point.X;Y=point.Y;
        }
    }
}
