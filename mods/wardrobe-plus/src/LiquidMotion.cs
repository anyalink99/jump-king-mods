using System;
using Microsoft.Xna.Framework;

namespace WardrobePlus.Advanced
{
    // Cosmetic damped free-surface model. never writes to gameplay physics
    internal sealed class LiquidMotion
    {
        internal const float Capacity=.72f;
        internal float Level=Capacity,Tilt,Wave;
        private float tiltSpeed,waveSpeed,draining;
        private Vector2 previousVelocity;
        private bool sampled,spilled;
        internal void Event(PresentationEvent e)
        {
            if(e.Trigger=="jump")waveSpeed-=.45f+e.Charge*.5f;
            if(e.Trigger=="land")waveSpeed+=MathHelper.Clamp(e.Speed*.1f,.4f,2.5f);
            if(e.Trigger=="splat"){draining=.3f;spilled=true;waveSpeed+=2.5f;}
        }
        internal void Update(float delta,Vector2 velocity,string state)
        {
            if(delta<=0)return;delta=Math.Min(delta,.1f);
            if(sampled)
            {
                var change=velocity-previousVelocity;
                tiltSpeed-=MathHelper.Clamp(change.X,-12,12)*.9f;
                waveSpeed-=MathHelper.Clamp(change.Y,-24,24)*.075f;
            }
            previousVelocity=velocity;sampled=true;
            int steps=(int)Math.Ceiling(delta*240);float dt=delta/steps;
            for(int i=0;i<steps;i++)
            {
                tiltSpeed+=(-Tilt*42-tiltSpeed*3.8f)*dt;
                waveSpeed+=(-Wave*85-waveSpeed*4.2f)*dt;
                Tilt=MathHelper.Clamp(Tilt+tiltSpeed*dt,-.3f,.3f);
                Wave=MathHelper.Clamp(Wave+waveSpeed*dt,-.14f,.14f);
                if(Math.Abs(Tilt)>=.3f)tiltSpeed*=.6f;
                if(Math.Abs(Wave)>=.14f)waveSpeed*=.6f;
            }
            if(draining>0){Level=Math.Max(0,Level-Capacity*delta/.3f);draining=Math.Max(0,draining-delta);}
            else if(spilled && state!="splat")Level=Math.Min(Capacity,Level+delta*.24f);
        }
        internal void Reset()
        {Level=Capacity;Tilt=Wave=tiltSpeed=waveSpeed=draining=0;sampled=spilled=false;previousVelocity=Vector2.Zero;}
    }
}
