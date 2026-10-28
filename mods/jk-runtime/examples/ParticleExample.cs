using System;
using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// an explicitly driven service example. the host calls Update/Draw from its own actor
namespace ParticleExample
{
    [RuntimeModule("example.particles", "Particle example")]
    public static class Module
    {
        [OnWorldReady]
        public static void Prepare(RuntimeScope scope){ParticleWorlds.PrepareNative(scope);}
    }
    public sealed class Sparks:IDisposable
    {
        private readonly ParticleSystem<ParticleState> particles;
        private readonly ParticleCollisionFrame collision=new ParticleCollisionFrame();
        private readonly ParticleBatch draw=new ParticleBatch(64);
        public Sparks(){particles=new ParticleSystem<ParticleState>(64,Move);}
        public void Emit(Vector2 position,Vector2 velocity)
        {var p=particles.Spawn();if(p!=null){p.Position=position;p.Velocity=velocity;p.Life=1;}}
        // the host supplies bounds enclosing all sweeps/support probes for this tick
        public void Update(float delta,Rectangle bounds)
        {collision.Begin(ParticleWorlds.Current,bounds);try{particles.Update(delta);}finally{collision.Clear();}}
        private void Move(ParticleState p,float delta)
        {
            p.Velocity.Y+=80*delta;Vector2 movement=p.Velocity*delta;
            int steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(movement.X),Math.Abs(movement.Y))/.75f));
            for(int i=0;i<steps;i++){var next=p.Position+movement/steps;if(collision.Contains(next)){p.Velocity=Vector2.Zero;break;}p.Position=next;}
        }
        // Position is already in the coordinate system of the host's active pass
        public void Draw(SpriteBatch batch,Texture2D pixel,Vector2 translation)
        {
            for(int i=0;i<particles.Count;i++){var p=particles[i];draw.Add(pixel,p.Position+translation,new Rectangle(0,0,1,1),Color.White*(1-p.Age/p.Life),0,Vector2.Zero,Vector2.One);}
            draw.Flush(batch);
        }
        public void Dispose(){particles.Dispose();collision.Dispose();draw.Dispose();}
    }
}
