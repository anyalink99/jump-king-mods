using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.Particles
{
    /// <summary>bounded ordered sprite commands. Flush inside the caller's active SpriteBatch, never begins, ends or changes its pass</summary>
    public sealed class ParticleBatch : IDisposable
    {
        private struct Command
        {internal Texture2D Texture;internal Vector2 Position,Origin,Scale;internal Rectangle Source;internal Color Color;internal float Rotation;internal SpriteEffects Flip;}
        private readonly Command[] commands;
        private int count;
        private bool disposed;
        public int Count {get{return count;}}
        public long DroppedCount {get;private set;}
        public ParticleBatch(int capacity)
        {if(capacity<1||capacity>65536)throw new ArgumentOutOfRangeException("capacity");commands=new Command[capacity];}
        public bool Add(Texture2D texture,Vector2 position,Rectangle source,Color color,float rotation,Vector2 origin,Vector2 scale,SpriteEffects flip=SpriteEffects.None)
        {
            if(disposed)throw new ObjectDisposedException("ParticleBatch");if(texture==null)throw new ArgumentNullException("texture");
            if(count==commands.Length){DroppedCount++;return false;}
            commands[count++]=new Command{Texture=texture,Position=position,Source=source,Color=color,Rotation=rotation,Origin=origin,Scale=scale,Flip=flip};return true;
        }
        public void Flush(SpriteBatch batch)
        {
            RuntimeApi.Kernel.CheckThread();if(disposed)throw new ObjectDisposedException("ParticleBatch");if(batch==null)throw new ArgumentNullException("batch");
            try{for(int i=0;i<count;i++){var c=commands[i];batch.Draw(c.Texture,c.Position,c.Source,c.Color,c.Rotation,c.Origin,c.Scale,c.Flip,0);}}
            finally{Clear();}
        }
        /// <summary>Discard commands and their borrowed textures. Doesn't dispose textures</summary>
        public void Clear(){Array.Clear(commands,0,count);count=0;}
        public void Dispose(){Clear();disposed=true;}
    }
}
