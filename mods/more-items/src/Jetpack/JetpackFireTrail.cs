using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JumpKingJetpack
{
    internal sealed class JetpackFireTrail : IDisposable
    {
        private const float Step = 1f / 60f;
        private const int MaximumParticles = 180;

        private static readonly Color DeepRed = new Color(105, 25, 24);
        private static readonly Color Red = new Color(170, 43, 24);
        private static readonly Color Orange = new Color(232, 83, 24);
        private static readonly Color Yellow = new Color(255, 177, 43);
        private static readonly Color White = new Color(255, 250, 218);
        private static readonly FieldInfo FlipField =
            typeof(PlayerEntity).GetField(
                "m_flip",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly PlayerEntity player;
        private readonly JKRuntime.Particles.ParticleSystem<Particle> particles;
        private readonly JKRuntime.Particles.ParticleCollisionFrame collision = new JKRuntime.Particles.ParticleCollisionFrame();
        private readonly JKRuntime.Particles.ParticleBatch drawBatch = new JKRuntime.Particles.ParticleBatch(MaximumParticles * 3);
        private float windAcceleration;
        private readonly Random random = new Random(73421);

        private bool enabled;
        private bool disposed;
        private bool hasEmitter;
        private Vector2 previousEmitter;
        private int previousDirection;
        private int sequence;

        internal JetpackFireTrail(PlayerEntity playerEntity)
        {
            player = playerEntity;
            particles = new JKRuntime.Particles.ParticleSystem<Particle>(MaximumParticles, StepParticle);
        }

        internal void SetEnabled(bool value)
        {
            enabled = value;
            if (!enabled)
            {
                particles.Clear();
                hasEmitter = false;
                collision.Clear();
                drawBatch.Clear();
            }
        }
        public void Dispose(){if(disposed)return;SetEnabled(false);particles.Dispose();collision.Dispose();drawBatch.Dispose();disposed=true;}

        internal void Update(bool active)
        {
            if (!enabled)
            {
                return;
            }

            UpdateParticles();
            if (!active)
            {
                hasEmitter = false;
                return;
            }

            BodyComp body = player.m_body;
            int direction = GetDirection();
            Vector2 emitter = GetEmitter(body, direction);
            Vector2 inheritedVelocity = body.Velocity * 25f;
            bool continuous = hasEmitter && direction == previousDirection;
            int count = continuous
                ? Math.Min(
                    8,
                    2 + (int)Math.Ceiling(
                        Vector2.Distance(previousEmitter, emitter) / 2.5f))
                : 3;
            for (int index = 0; index < count; index++)
            {
                float progress = continuous
                    ? (index + 1f) / count
                    : 1f;
                Vector2 origin = continuous
                    ? Vector2.Lerp(previousEmitter, emitter, progress)
                    : emitter;
                Spawn(origin, inheritedVelocity);
            }
            previousEmitter = emitter;
            previousDirection = direction;
            hasEmitter = true;

        }

        internal void Draw()
        {
            if (!enabled)
            {
                return;
            }
            drawBatch.Clear();
            for (int index = 0; index < particles.Count; index++) DrawParticle(particles[index]);
            drawBatch.Flush(Game1.spriteBatch);
        }

        private void UpdateParticles()
        {
            if(particles.Count==0)return;
            windAcceleration=WindManager.CurrentVelocity*10f;
            float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
            for(int i=0;i<particles.Count;i++)
            {
                var p=particles[i];float marginX=(Math.Abs(p.Velocity.X)+Math.Abs(windAcceleration)+1)*Step+4;
                float marginY=(Math.Abs(p.Velocity.Y)+1)*Step+4;
                minX=Math.Min(minX,p.Position.X-marginX);maxX=Math.Max(maxX,p.Position.X+marginX);
                minY=Math.Min(minY,p.Position.Y-marginY);maxY=Math.Max(maxY,p.Position.Y+marginY);
            }
            int left=(int)Math.Floor(minX),top=(int)Math.Floor(minY);
            collision.Begin(JKRuntime.Particles.ParticleWorlds.Current,new Rectangle(left,top,(int)Math.Ceiling(maxX)-left+1,(int)Math.Ceiling(maxY)-top+1));
            try{particles.Update(Step);}finally{collision.Clear();}
        }
        private void StepParticle(Particle particle,float delta)
        {
            float turbulence=(float)Math.Sin(particle.Phase+particle.Age*particle.Frequency);
            particle.Velocity.X+=windAcceleration+turbulence*.12f;
            particle.Velocity.Y-=(particle.Age<.18f?7f:28f)*delta;
            particle.Velocity.X*=.986f;particle.Velocity.Y*=.991f;
            MoveParticle(particle);
        }

        private void MoveParticle(Particle particle)
        {
            Vector2 movement = particle.Velocity * Step;
            bool blockedX = false;
            bool blockedY = false;

            if (Math.Abs(movement.X) > 0.0001f)
            {
                Vector2 horizontal = new Vector2(
                    particle.Position.X + movement.X,
                    particle.Position.Y);
                if (SweepCollides(particle.Position, horizontal))
                {
                    particle.Velocity.X *= -0.28f;
                    particle.Velocity.Y *= 0.9f;
                    blockedX = true;
                }
                else
                {
                    particle.Position.X = horizontal.X;
                }
            }

            if (Math.Abs(movement.Y) > 0.0001f)
            {
                Vector2 vertical = new Vector2(
                    particle.Position.X,
                    particle.Position.Y + movement.Y);
                if (SweepCollides(particle.Position, vertical))
                {
                    particle.Velocity.Y *= -0.24f;
                    particle.Velocity.X *= 0.72f;
                    blockedY = true;
                }
                else
                {
                    particle.Position.Y = vertical.Y;
                }
            }

            if (blockedX || blockedY)
            {
                particle.Age += 0.025f;
            }
        }

        private void Spawn(Vector2 origin, Vector2 inheritedVelocity)
        {
            float horizontal = Range(-13f, 13f);
            float exhaust = Range(52f, 92f);
            var particle=particles.Spawn(true);
            particle.Position=origin+new Vector2(Range(-1.5f,1.5f),Range(-1f,1.5f));
            particle.Velocity=inheritedVelocity+new Vector2(horizontal,exhaust);
            particle.Life=Range(.68f,1.08f);particle.Phase=Range(0f,MathHelper.TwoPi);
            particle.Frequency=Range(8f,15f);particle.Size=random.Next(0,3);particle.Sequence=sequence++;
        }

        private static Vector2 GetEmitter(BodyComp body, int direction)
        {
            bool morphed = JKRuntime.Gameplay.GameFeatures.IsMorphed;
            int pose = morphed
                ? 4
                : body.Velocity.Y <= 0f ? 5 : 6;
            int nozzleX;
            int nozzleY;
            bool rotated;
            JetpackAtlasData.GetNozzle(
                pose,
                out nozzleX,
                out nozzleY,
                out rotated);
            if (direction < 0)
            {
                nozzleX = 47 - nozzleX;
            }
            return body.Position
                + new Vector2(-15f, -22f)
                + new Vector2(nozzleX, nozzleY + 18f)
                + (morphed ? new Vector2(0f, -4f) : Vector2.Zero);
        }

        private int GetDirection()
        {
            if (FlipField == null)
            {
                throw new MissingFieldException(
                    typeof(PlayerEntity).FullName,
                    "m_flip");
            }
            SpriteEffects effects = (SpriteEffects)FlipField.GetValue(player);
            return (effects & SpriteEffects.FlipHorizontally) != 0
                ? -1
                : 1;
        }

        private bool SweepCollides(Vector2 start, Vector2 end)
        {
            int left = (int)Math.Floor(Math.Min(start.X, end.X)) - 2;
            int top = (int)Math.Floor(Math.Min(start.Y, end.Y)) - 2;
            int right = (int)Math.Ceiling(Math.Max(start.X, end.X)) + 2;
            int bottom = (int)Math.Ceiling(Math.Max(start.Y, end.Y)) + 2;
            return collision.Intersects(
                new Rectangle(
                    left,
                    top,
                    Math.Max(1, right - left + 1),
                    Math.Max(1, bottom - top + 1)));
        }

        private void DrawParticle(Particle particle)
        {
            float progress = particle.Age / particle.Life;
            float fade = 1f - MathHelper.Clamp(
                (progress - 0.68f) / 0.32f,
                0f,
                1f);
            int flicker = (particle.Sequence
                + (int)(particle.Age * 60f)) % 3;
            if (progress < 0.22f)
            {
                DrawSquare(
                    particle.Position,
                    4 + particle.Size / 2,
                    Orange,
                    fade * 0.62f);
                DrawSquare(particle.Position, 3, Yellow, fade);
                DrawSquare(particle.Position, 2, White, fade);
            }
            else if (progress < 0.48f)
            {
                DrawSquare(
                    particle.Position,
                    3 + particle.Size / 2,
                    DeepRed,
                    fade * 0.76f);
                DrawSquare(
                    particle.Position,
                    2 + flicker / 2,
                    Orange,
                    fade);
                DrawSquare(particle.Position, 1, Yellow, fade);
            }
            else if (progress < 0.76f)
            {
                DrawSquare(
                    particle.Position,
                    2 + particle.Size / 2,
                    DeepRed,
                    fade * 0.8f);
                DrawSquare(particle.Position, 1 + flicker / 2, Red, fade);
                if ((particle.Sequence + flicker) % 4 == 0)
                {
                    DrawSquare(particle.Position, 1, Yellow, fade * 0.8f);
                }
            }
            else
            {
                DrawSquare(
                    particle.Position,
                    1 + particle.Size / 2,
                    DeepRed,
                    fade * 0.72f);
            }
        }

        private void DrawSquare(
            Vector2 worldPosition,
            int size,
            Color color,
            float opacity)
        {
            Vector2 screen = Camera.TransformVector2(worldPosition);
            if (screen.X < -8f
                || screen.X > 488f
                || screen.Y < -8f
                || screen.Y > 368f)
            {
                return;
            }
            var texture=Game1.instance.contentManager.Pixel.texture;
            drawBatch.Add(texture,new Vector2((int)Math.Round(screen.X)-size/2,(int)Math.Round(screen.Y)-size/2),
                new Rectangle(0,0,texture.Width,texture.Height),color*MathHelper.Clamp(opacity,0f,1f),0,Vector2.Zero,
                new Vector2(size/(float)texture.Width,size/(float)texture.Height));
        }

        private float Range(float minimum, float maximum)
        {
            return minimum
                + (float)random.NextDouble() * (maximum - minimum);
        }

        private sealed class Particle : JKRuntime.Particles.ParticleState
        {
            internal float Phase;
            internal float Frequency;
            internal int Size;
            internal int Sequence;
            public override void Reset(){base.Reset();Phase=Frequency=0;Size=Sequence=0;}
        }
    }
}
