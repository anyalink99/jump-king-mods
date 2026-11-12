using System;
using Microsoft.Xna.Framework;

namespace JKRuntime.Presentation
{
    internal sealed class PositionHistory
    {
        public Vector2 Current, Velocity;
        private bool initialized;
        private bool wasGrounded;
        public bool CanPredict { get; private set; }
        private int screen;
        public void Reset() { initialized = false; CanPredict=false; }
        public void Observe(Vector2 position, Vector2 velocity, int nextScreen, bool snap, bool grounded=false, Vector2? nativeBefore=null)
        {
            bool discontinuity = !initialized || snap || screen != nextScreen || Vector2.DistanceSquared(Current, position) > 64*64
                || float.IsNaN(velocity.X) || float.IsNaN(velocity.Y) || float.IsInfinity(velocity.X) || float.IsInfinity(velocity.Y);
            // Water/material behaviours modify displacement without necessarily
            // writing BodyComp.Velocity. the completed tick already includes all
            // those effects, including foreign handlers, don't simulate them
            Vector2 movedBy=position-Current;
            Velocity = discontinuity ? Vector2.Zero : Vector2.Clamp(movedBy, new Vector2(-64), new Vector2(64));
            if(!discontinuity)
            {
                // the player's behaviour tree runs after BodyComp. A fresh walk
                // or takeoff must be visible before the next body integration
                var before=nativeBefore ?? velocity;
                if(Math.Abs(movedBy.X)<.001f && before.X==0) Velocity.X=velocity.X;
                if(Math.Abs(movedBy.Y)<.001f && velocity.Y<0 && before.Y>=0) Velocity.Y=velocity.Y;
                // A native stop or bounce supersedes the pre-impact displacement
                if(velocity.X==0) Velocity.X=0;
                else if(velocity.X*Velocity.X<0) Velocity.X=velocity.X;
                if(velocity.Y==0) Velocity.Y=0;
                else if(velocity.Y*Velocity.Y<0) Velocity.Y=velocity.Y;
                // the next draw starts from the completed tick. keep observed
                // material scaling, but use its outgoing speed, not the speed
                // before gravity, drag or a same-direction collision response
                if(nativeBefore.HasValue)
                {
                    if(before.X*velocity.X>0 && movedBy.X*before.X>0)
                        Velocity.X=Math.Abs(before.X)<.01f ? velocity.X : movedBy.X*(velocity.X/before.X);
                    if(before.Y*velocity.Y>0 && movedBy.Y*before.Y>0)
                        Velocity.Y=Math.Abs(before.Y)<.01f ? velocity.Y : movedBy.Y*(velocity.Y/before.Y);
                }
                // the native sand behaviour adds one positional pixel on the
                // first downward tick off support, even outside sand. that
                // one-time offset isn't a persistent displacement multiplier
                if(wasGrounded && !grounded && before.Y>0 && velocity.Y>0)
                    Velocity.Y=velocity.Y;
            }
            if(grounded)
            {
                if(Velocity.Y>0) Velocity.Y=0; // native gravity remains positive on support
                float moved=position.X-Current.X;
                if(Math.Abs(moved)<.001f && (!nativeBefore.HasValue || nativeBefore.Value.X!=0)
                    || moved*Velocity.X<0) Velocity.X=0;
            }
            Velocity=Vector2.Clamp(Velocity,new Vector2(-64),new Vector2(64));
            Current = position; screen = nextScreen; initialized = true;
            wasGrounded=grounded; CanPredict=!discontinuity;
        }
        // observed motion is in pixels per physics tick. Predict at most one
        // tick, no speculative body update, collision callback or save mutation
        public Vector2 At(float alpha) { return Current + Velocity * Math.Max(0, Math.Min(1, alpha)); }
    }
}
