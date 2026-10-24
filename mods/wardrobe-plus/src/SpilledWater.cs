using System;
using System.Collections.Generic;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace WardrobePlus.Advanced
{
    // Cosmetic point droplets. Queries native block geometry without running body behaviours
    internal static class SpilledWater
    {
        // Wardrobe owns water behaviour, Runtime owns collision storage and world lifetime
        internal sealed class CollisionFrame
        {
            private readonly JKRuntime.Particles.ParticleCollisionFrame frame=new JKRuntime.Particles.ParticleCollisionFrame();
            internal Func<Vector2,bool> Prepare(IReadOnlyList<PresentationActor.Particle> particles)
            {
                frame.Clear();float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
                for(int i=0;i<particles.Count;i++)if(particles[i].Effect.Definition.collision=="water")
                {var p=particles[i].Position;minX=Math.Min(minX,p.X);minY=Math.Min(minY,p.Y);maxX=Math.Max(maxX,p.X);maxY=Math.Max(maxY,p.Y);}
                if(minX==float.MaxValue)return null;
                int left=(int)Math.Floor(minX)-128,top=(int)Math.Floor(minY)-128;
                frame.Begin(JKRuntime.Particles.ParticleWorlds.Current,new Rectangle(left,top,(int)Math.Ceiling(maxX)-left+129,(int)Math.Ceiling(maxY)-top+129));
                return frame.PointQuery;
            }
            internal void Clear(){frame.Clear();}
        }
        internal static Func<Vector2,bool> World(IReadOnlyList<PresentationActor.Particle> particles)
        {return new CollisionFrame().Prepare(particles);}
        internal static void Step(PresentationActor.Particle p,float delta,Func<Vector2,bool> solid)
        {
            var d=p.Effect.Definition;
            p.Velocity.Y+=d.gravity*delta;p.Velocity*= (float)Math.Exp(-d.drag*delta);
            p.Velocity=Vector2.Clamp(p.Velocity,new Vector2(-1200),new Vector2(1200));
            int steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(p.Velocity.X),Math.Abs(p.Velocity.Y))*delta/.75f));
            float dt=delta/steps;p.Grounded=false;
            for(int step=0;step<steps;step++)
            {
                var next=p.Position+new Vector2(p.Velocity.X*dt,0);
                if(solid!=null && solid(next))p.Velocity.X=-p.Velocity.X*.15f;else p.Position=next;
                next=p.Position+new Vector2(0,p.Velocity.Y*dt);
                if(solid!=null && solid(next))
                {
                    if(p.Velocity.Y>0)
                    {
                        // Impact spreads water along the surface, remaining momentum carries it over edges
                        if(!p.Grounded && p.Velocity.Y>12)p.Velocity.X+=p.FlowDirection*Math.Min(28,p.Velocity.Y*.18f);
                        p.Grounded=true;
                    }
                    p.Velocity.Y=0;
                }
                else p.Position=next;
            }
            if(solid!=null && solid(p.Position+new Vector2(0,1)))
            {p.Grounded=true;p.Velocity.X*=(float)Math.Exp(-.65f*delta);}
            p.Rotation=p.Grounded?MathHelper.PiOver2:0;
        }
    }
}
